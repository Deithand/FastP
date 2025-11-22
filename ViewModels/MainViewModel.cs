using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using FastP.Services;

namespace FastP.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly FileSorterService _fileSorterService;
        private readonly ConfigManager _configManager;
        private bool _isPaused;
        private int _filesProcessedToday;

        public MainViewModel(FileSorterService fileSorterService, ConfigManager configManager)
        {
            _fileSorterService = fileSorterService;
            _configManager = configManager;
            Logs = new ObservableCollection<string>();
            _isPaused = false;
            _filesProcessedToday = _fileSorterService.FilesProcessedToday;
            
            _fileSorterService.LogMessage += OnLogMessage;
            _fileSorterService.FilesProcessedChanged += OnFilesProcessedChanged;
            _fileSorterService.FileMoved += OnFileMoved;
        }

        public ObservableCollection<string> Logs { get; }

        public bool IsPaused
        {
            get => _isPaused;
            set
            {
                if (_isPaused != value)
                {
                    _isPaused = value;
                    if (_isPaused)
                        _fileSorterService.Pause();
                    else
                        _fileSorterService.Resume();
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(PauseButtonText));
                }
            }
        }

        public string PauseButtonText => _isPaused ? "▶ Возобновить" : "⏸ Пауза";

        public int FilesProcessedToday
        {
            get => _filesProcessedToday;
            set
            {
                if (_filesProcessedToday != value)
                {
                    _filesProcessedToday = value;
                    OnPropertyChanged();
                }
            }
        }

        public int TotalFilesProcessed => _fileSorterService.TotalFilesProcessed;

        private void OnLogMessage(string message)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                Logs.Insert(0, message);
                // Ограничиваем количество логов (оставляем последние 1000)
                if (Logs.Count > 1000)
                {
                    Logs.RemoveAt(Logs.Count - 1);
                }
            });
        }

        private void OnFilesProcessedChanged(int count)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                FilesProcessedToday = count;
                OnPropertyChanged(nameof(TotalFilesProcessed));
            });
        }

        private void OnFileMoved(string fileName, string targetFolder)
        {
            // Показываем уведомление Windows
            ShowNotification(fileName, targetFolder);
        }

        private void ShowNotification(string fileName, string targetFolder)
        {
            try
            {
                var notification = new System.Windows.Forms.NotifyIcon
                {
                    Icon = System.Drawing.SystemIcons.Information,
                    BalloonTipTitle = "FastP - Файл отсортирован",
                    BalloonTipText = $"{fileName}\nПеремещен в {targetFolder}",
                    BalloonTipIcon = System.Windows.Forms.ToolTipIcon.Info,
                    Visible = true
                };
                
                notification.ShowBalloonTip(3000);
                
                // Удаляем уведомление после показа
                System.Threading.Tasks.Task.Delay(3500).ContinueWith(_ =>
                {
                    notification.Dispose();
                });
            }
            catch
            {
                // Игнорируем ошибки уведомлений
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

