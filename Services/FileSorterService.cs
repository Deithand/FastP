using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FastP.Services
{
    public class FileSorterService
    {
        private readonly ConfigManager _configManager;
        private FileSystemWatcher? _watcher;
        private string _downloadsPath;
        private bool _isRunning;
        private bool _isPaused;
        
        // Стек для хранения истории перемещений: (Текущий путь, Исходный путь)
        private readonly Stack<(string CurrentPath, string OriginalPath)> _undoStack = new();

        public event Action<string>? LogMessage;
        public event Action<int>? FilesProcessedChanged;
        public event Action<string, string>? FileMoved; // fileName, targetFolder
        public event Action<bool>? UndoAvailabilityChanged;

        public bool IsRunning => _isRunning;
        public bool IsPaused => _isPaused;
        public bool CanUndo => _undoStack.Count > 0;
        public int FilesProcessedToday { get; private set; }
        public int TotalFilesProcessed { get; private set; }

        public FileSorterService(ConfigManager configManager)
        {
            _configManager = configManager;
            _downloadsPath = GetSourcePath();
            LoadStatistics();
        }

        private string GetSourcePath()
        {
            return !string.IsNullOrEmpty(_configManager.Settings.SourcePath) && Directory.Exists(_configManager.Settings.SourcePath)
                ? _configManager.Settings.SourcePath
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        }

        public void UpdateSettings()
        {
            bool wasRunning = _isRunning;
            Stop();
            _downloadsPath = GetSourcePath();
            if (wasRunning)
            {
                Start();
            }
        }

        public void Start()
        {
            if (_isRunning)
                return;

            _isRunning = true;
            
            if (!Directory.Exists(_downloadsPath))
            {
                Log($"Папка для сортировки не найдена: {_downloadsPath}");
                return;
            }

            Log($"Сервис сортировки запущен. Отслеживание: {_downloadsPath}");

            _watcher = new FileSystemWatcher(_downloadsPath)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                Filter = "*.*",
                EnableRaisingEvents = true
            };

            _watcher.Created += OnFileCreated;
            _watcher.Changed += OnFileChanged;

            // Обрабатываем существующие файлы асинхронно
            Task.Run(() => ProcessExistingFilesAsync());
        }

        public void Pause()
        {
            if (!_isRunning || _isPaused)
                return;

            _isPaused = true;
            if (_watcher != null)
                _watcher.EnableRaisingEvents = false;
            Log("Сервис сортировки приостановлен");
        }

        public void Resume()
        {
            if (!_isRunning || !_isPaused)
                return;

            _isPaused = false;
            if (_watcher != null)
                _watcher.EnableRaisingEvents = true;
            Log("Сервис сортировки возобновлен");
        }

        public void Stop()
        {
            if (!_isRunning)
                return;

            _isRunning = false;
            _isPaused = false;
            _watcher?.Dispose();
            _watcher = null;
            SaveStatistics();
            Log("Сервис сортировки остановлен");
        }

        public void UndoLastAction()
        {
            if (_undoStack.Count == 0) return;

            try
            {
                var (currentPath, originalPath) = _undoStack.Pop();
                UndoAvailabilityChanged?.Invoke(_undoStack.Count > 0);

                if (File.Exists(currentPath))
                {
                    // Проверяем, свободен ли исходный путь, если нет - переименовываем
                    string targetPath = originalPath;
                    if (File.Exists(targetPath))
                    {
                        targetPath = GenerateUniquePath(targetPath);
                    }

                    File.Move(currentPath, targetPath);
                    Log($"Отменено: {Path.GetFileName(currentPath)} -> {Path.GetFileName(targetPath)}");
                    
                    // Уменьшаем статистику
                    if (FilesProcessedToday > 0) FilesProcessedToday--;
                    FilesProcessedChanged?.Invoke(FilesProcessedToday);
                }
                else
                {
                    Log($"Не удалось отменить: файл {Path.GetFileName(currentPath)} не найден");
                }
            }
            catch (Exception ex)
            {
                Log($"Ошибка при отмене действия: {ex.Message}");
            }
        }

        private async void OnFileCreated(object sender, FileSystemEventArgs e)
        {
            await Task.Delay(1000); // Non-blocking delay
            await TrySortFileAsync(e.FullPath);
        }

        private async void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            await Task.Delay(500); // Non-blocking delay
            await TrySortFileAsync(e.FullPath);
        }

        private async Task TrySortFileAsync(string filePath, int retryCount = 0)
        {
            if (!File.Exists(filePath) || _isPaused)
                return;

            if (IsFileLocked(filePath))
            {
                if (retryCount < 3) // Max 3 retries
                {
                    await Task.Delay(2000);
                    await TrySortFileAsync(filePath, retryCount + 1);
                }
                else
                {
                    Log($"Не удалось обработать файл (занят процессом): {Path.GetFileName(filePath)}");
                }
                return;
            }

            SortFile(filePath);
        }

        private bool IsFileLocked(string filePath)
        {
            try
            {
                using (var stream = File.Open(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    return false;
                }
            }
            catch (IOException) { return true; }
            catch (UnauthorizedAccessException) { return true; }
        }

        private void SortFile(string filePath)
        {
            try
            {
                var fileInfo = new FileInfo(filePath);
                
                if (fileInfo.Length < _configManager.Settings.MinFileSize)
                    return;

                var fileName = fileInfo.Name;
                var extension = fileInfo.Extension;
                var targetCategory = _configManager.GetTargetFolder(extension);

                if (string.IsNullOrEmpty(targetCategory))
                    return; // Нет правила

                // Определяем путь назначения
                var targetPath = Path.Combine(_downloadsPath, targetCategory);

                // Feature: Sort by Date
                if (_configManager.Settings.OrganizeByDate)
                {
                    var dateFolder = fileInfo.LastWriteTime.ToString("yyyy-MM-dd");
                    targetPath = Path.Combine(targetPath, dateFolder);
                }
                
                // Игнорируем, если файл уже там
                var fileDirectory = Path.GetDirectoryName(filePath);
                if (string.Equals(fileDirectory, targetPath, StringComparison.OrdinalIgnoreCase))
                    return;
                
                if (!Directory.Exists(targetPath))
                {
                    Directory.CreateDirectory(targetPath);
                }

                var destinationFile = Path.Combine(targetPath, fileName);
                
                // Обработка дубликатов имен
                if (File.Exists(destinationFile))
                {
                    destinationFile = GenerateUniquePath(destinationFile);
                }

                // Перемещение
                File.Move(filePath, destinationFile);
                
                // Добавляем в стек Undo
                _undoStack.Push((destinationFile, filePath));
                UndoAvailabilityChanged?.Invoke(true);
                
                // Обновляем статистику
                FilesProcessedToday++;
                TotalFilesProcessed++;
                FilesProcessedChanged?.Invoke(FilesProcessedToday);
                SaveStatistics();
                
                if (_configManager.Settings.EnableNotifications)
                {
                    FileMoved?.Invoke(fileName, targetCategory);
                }
                
                Log($"Файл {fileName} перемещен в {targetCategory}");
            }
            catch (Exception ex)
            {
                Log($"Ошибка при обработке {Path.GetFileName(filePath)}: {ex.Message}");
            }
        }

        private string GenerateUniquePath(string fullPath)
        {
            if (!File.Exists(fullPath)) return fullPath;

            var folder = Path.GetDirectoryName(fullPath);
            var nameWithoutExt = Path.GetFileNameWithoutExtension(fullPath);
            var ext = Path.GetExtension(fullPath);
            int counter = 1;
            string newPath;

            do
            {
                newPath = Path.Combine(folder!, $"{nameWithoutExt} ({counter}){ext}");
                counter++;
            } while (File.Exists(newPath));

            return newPath;
        }

        private async Task ProcessExistingFilesAsync()
        {
            try
            {
                if (!Directory.Exists(_downloadsPath)) return;

                var files = Directory.GetFiles(_downloadsPath)
                    .Where(f => string.Equals(Path.GetDirectoryName(f), _downloadsPath, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (files.Count > 0)
                    Log($"Найдено {files.Count} файлов для обработки");

                foreach (var file in files)
                {
                    await TrySortFileAsync(file);
                    await Task.Delay(50); // Throttle slightly
                }
            }
            catch (Exception ex)
            {
                Log($"Ошибка при сканировании папки: {ex.Message}");
            }
        }

        private void Log(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            LogMessage?.Invoke($"[{timestamp}] {message}");
        }

        private void LoadStatistics()
        {
            try
            {
                var statsFile = "statistics.json";
                if (File.Exists(statsFile))
                {
                    var json = File.ReadAllText(statsFile);
                    var stats = System.Text.Json.JsonSerializer.Deserialize<Statistics>(json);
                    if (stats != null)
                    {
                        TotalFilesProcessed = stats.TotalFilesProcessed;
                        var lastDate = DateTime.Parse(stats.LastProcessedDate);
                        if (lastDate.Date == DateTime.Today)
                            FilesProcessedToday = stats.FilesProcessedToday;
                        else
                            FilesProcessedToday = 0;
                    }
                }
            }
            catch { /* Ignore */ }
        }

        private void SaveStatistics()
        {
            try
            {
                var stats = new Statistics
                {
                    TotalFilesProcessed = TotalFilesProcessed,
                    FilesProcessedToday = FilesProcessedToday,
                    LastProcessedDate = DateTime.Now.ToString("yyyy-MM-dd")
                };
                var json = System.Text.Json.JsonSerializer.Serialize(stats, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText("statistics.json", json);
            }
            catch { /* Ignore */ }
        }

        private class Statistics
        {
            public int TotalFilesProcessed { get; set; }
            public int FilesProcessedToday { get; set; }
            public string LastProcessedDate { get; set; } = string.Empty;
        }
    }
}