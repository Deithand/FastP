using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using FastP.Services;

namespace FastP.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly FileSorterService _fileSorterService;
        private readonly ConfigManager _configManager;
        private readonly System.Windows.Forms.NotifyIcon _notifyIcon;
        private bool _isPaused;
        private int _filesProcessedToday;
        private bool _canUndo;

        public MainViewModel(FileSorterService fileSorterService, ConfigManager configManager)
        {
            _fileSorterService = fileSorterService;
            _configManager = configManager;
            Logs = new ObservableCollection<string>();
            _isPaused = false;
            _filesProcessedToday = _fileSorterService.FilesProcessedToday;
            
            // Initialize NotifyIcon once
            _notifyIcon = new System.Windows.Forms.NotifyIcon
            {
                Icon = System.Drawing.SystemIcons.Information,
                Visible = true
            };

            // Commands
            UndoCommand = new RelayCommand(_ => Undo(), _ => CanUndo);

            // Events
            _fileSorterService.LogMessage += OnLogMessage;
            _fileSorterService.FilesProcessedChanged += OnFilesProcessedChanged;
            _fileSorterService.FileMoved += OnFileMoved;
            _fileSorterService.UndoAvailabilityChanged += OnUndoAvailabilityChanged;
        }

        public ObservableCollection<string> Logs { get; }

        public ICommand UndoCommand { get; }

        public bool CanUndo
        {
            get => _canUndo;
            set
            {
                if (_canUndo != value)
                {
                    _canUndo = value;
                    OnPropertyChanged();
                    // Refresh command status
                    (UndoCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

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

        private void Undo()
        {
            _fileSorterService.UndoLastAction();
        }

        private void OnUndoAvailabilityChanged(bool isAvailable)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                CanUndo = isAvailable;
            });
        }

        private void OnLogMessage(string message)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                Logs.Insert(0, message);
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
            ShowNotification(fileName, targetFolder);
        }

        private void ShowNotification(string fileName, string targetFolder)
        {
            try
            {
                if (_notifyIcon != null)
                {
                    _notifyIcon.BalloonTipTitle = "FastP - Файл отсортирован";
                    _notifyIcon.BalloonTipText = $"{fileName}\nПеремещен в {targetFolder}";
                    _notifyIcon.ShowBalloonTip(3000);
                }
            }
            catch
            {
                // Ignore notification errors
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // Simple RelayCommand implementation
    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Predicate<object?>? _canExecute;

        public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter)
        {
            return _canExecute == null || _canExecute(parameter);
        }

        public void Execute(object? parameter)
        {
            _execute(parameter);
        }

        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
