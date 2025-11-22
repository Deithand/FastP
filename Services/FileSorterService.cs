using System;
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

        public event Action<string>? LogMessage;
        public event Action<int>? FilesProcessedChanged;
        public event Action<string, string>? FileMoved; // fileName, targetFolder

        public bool IsRunning => _isRunning;
        public bool IsPaused => _isPaused;
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
                // Try to create if it's the default downloads, but usually it exists. 
                // If user specified a custom path that doesn't exist, we log it.
                return;
            }

            Log($"Сервис сортировки запущен. Отслеживание: {_downloadsPath}");

            // Создаем FileSystemWatcher для отслеживания новых файлов
            _watcher = new FileSystemWatcher(_downloadsPath)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                Filter = "*.*",
                EnableRaisingEvents = true
            };

            _watcher.Created += OnFileCreated;
            _watcher.Changed += OnFileChanged;

            // Обрабатываем существующие файлы при старте
            Task.Run(() => ProcessExistingFiles());
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

        private void OnFileCreated(object sender, FileSystemEventArgs e)
        {
            // Небольшая задержка, чтобы файл успел записаться
            Task.Delay(1000).ContinueWith(_ => TrySortFile(e.FullPath));
        }

        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            // Обрабатываем изменения файла (например, когда браузер завершает загрузку)
            Task.Delay(500).ContinueWith(_ => TrySortFile(e.FullPath));
        }

        private void TrySortFile(string filePath)
        {
            if (!File.Exists(filePath) || _isPaused)
                return;

            // Проверяем, не занят ли файл
            if (IsFileLocked(filePath))
            {
                // Повторяем попытку через некоторое время
                Task.Delay(2000).ContinueWith(_ => TrySortFile(filePath));
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
                    // Файл доступен
                }
                return false;
            }
            catch (IOException)
            {
                // Файл занят другим процессом
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                // Нет доступа к файлу
                return true;
            }
        }

        private void SortFile(string filePath)
        {
            try
            {
                var fileInfo = new FileInfo(filePath);
                
                // Проверка размера файла
                if (fileInfo.Length < _configManager.Settings.MinFileSize)
                {
                    // Log($"Файл {fileInfo.Name} пропущен (меньше минимального размера)");
                    return;
                }

                var fileName = fileInfo.Name;
                var extension = fileInfo.Extension;
                var targetFolder = _configManager.GetTargetFolder(extension);

                if (string.IsNullOrEmpty(targetFolder))
                {
                    Log($"Файл {fileName} не обработан (нет правила для расширения {extension})");
                    return;
                }

                var targetPath = Path.Combine(_downloadsPath, targetFolder);
                
                // Проверяем, не находится ли файл уже в целевой папке
                var fileDirectory = Path.GetDirectoryName(filePath);
                if (string.Equals(fileDirectory, targetPath, StringComparison.OrdinalIgnoreCase))
                {
                    return; // Файл уже в нужной папке
                }
                
                // Создаем целевую папку, если её нет
                if (!Directory.Exists(targetPath))
                {
                    Directory.CreateDirectory(targetPath);
                    Log($"Создана папка: {targetFolder}");
                }

                var destinationFile = Path.Combine(targetPath, fileName);
                
                // Если файл с таким именем уже существует, добавляем номер
                if (File.Exists(destinationFile))
                {
                    var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                    var ext = Path.GetExtension(fileName);
                    int counter = 1;
                    
                    do
                    {
                        destinationFile = Path.Combine(targetPath, $"{nameWithoutExt} ({counter}){ext}");
                        counter++;
                    } while (File.Exists(destinationFile));
                }

                // Перемещаем файл
                File.Move(filePath, destinationFile);
                
                // Обновляем статистику
                FilesProcessedToday++;
                TotalFilesProcessed++;
                FilesProcessedChanged?.Invoke(FilesProcessedToday);
                SaveStatistics();
                
                // Отправляем событие для уведомлений
                if (_configManager.Settings.EnableNotifications)
                {
                    FileMoved?.Invoke(fileName, targetFolder);
                }
                
                Log($"Файл {fileName} перемещен в {targetFolder}");
            }
            catch (Exception ex)
            {
                Log($"Ошибка при обработке файла {Path.GetFileName(filePath)}: {ex.Message}");
            }
        }

        private void ProcessExistingFiles()
        {
            try
            {
                if (!Directory.Exists(_downloadsPath))
                {
                    Log($"Папка для сортировки не найдена: {_downloadsPath}");
                    return;
                }

                // Получаем только файлы из корня папки Загрузки (не из подпапок)
                var files = Directory.GetFiles(_downloadsPath)
                    .Where(f => 
                    {
                        var dir = Path.GetDirectoryName(f);
                        return string.Equals(dir, _downloadsPath, StringComparison.OrdinalIgnoreCase);
                    })
                    .ToList();

                Log($"Найдено {files.Count} файлов для обработки");

                foreach (var file in files)
                {
                    TrySortFile(file);
                    Thread.Sleep(100); // Небольшая задержка между файлами
                }
            }
            catch (Exception ex)
            {
                Log($"Ошибка при обработке существующих файлов: {ex.Message}");
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
                        {
                            FilesProcessedToday = stats.FilesProcessedToday;
                        }
                        else
                        {
                            FilesProcessedToday = 0;
                        }
                    }
                }
            }
            catch
            {
                // Игнорируем ошибки загрузки статистики
            }
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
            catch
            {
                // Игнорируем ошибки сохранения статистики
            }
        }

        private class Statistics
        {
            public int TotalFilesProcessed { get; set; }
            public int FilesProcessedToday { get; set; }
            public string LastProcessedDate { get; set; } = string.Empty;
        }
    }
}
