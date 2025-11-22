using System;
using System.Windows;
using System.Windows.Forms;
using FastP.Services;
using FastP.ViewModels;
using System.Linq;

namespace FastP.Views
{
    public partial class MainWindow : Window
    {
        private NotifyIcon? _notifyIcon;
        private readonly FileSorterService _fileSorterService;
        private readonly ConfigManager _configManager;
        private readonly MainViewModel _viewModel;

        public MainWindow()
        {
            InitializeComponent();

            // Инициализация сервисов
            _configManager = new ConfigManager();
            _fileSorterService = new FileSorterService(_configManager);
            _viewModel = new MainViewModel(_fileSorterService, _configManager);
            DataContext = _viewModel;

            // Настройка системного трея
            SetupSystemTray();

            // Запуск сервиса сортировки
            _fileSorterService.Start();

            // Проверка автозапуска и обновлений
            Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Проверка аргументов запуска
            var args = Environment.GetCommandLineArgs();
            if (args.Contains("--autostart"))
            {
                Hide();
            }

            // Тихая проверка обновлений
            try
            {
                var updateService = new UpdateService();
                var update = await updateService.CheckForUpdatesAsync();
                if (update != null)
                {
                    // Показываем уведомление в трее, если есть обновление
                    _notifyIcon?.ShowBalloonTip(5000, 
                        "Доступно обновление FastP", 
                        $"Версия {update.Version} доступна для скачивания. Нажмите, чтобы обновить.", 
                        ToolTipIcon.Info);
                    
                    // Добавляем обработчик клика по баллуну для обновления
                    if (_notifyIcon != null)
                    {
                        _notifyIcon.BalloonTipClicked += (s, args) =>
                        {
                            if (!string.IsNullOrEmpty(update.DownloadUrl))
                            {
                                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                                {
                                    FileName = update.DownloadUrl,
                                    UseShellExecute = true
                                });
                            }
                        };
                    }
                }
            }
            catch
            {
                // Игнорируем ошибки автообновления
            }
        }

        private void SetupSystemTray()
        {
            _notifyIcon = new NotifyIcon
            {
                Icon = System.Drawing.SystemIcons.Application,
                Text = "FastP - Fast Packer",
                Visible = true
            };

            _notifyIcon.DoubleClick += (s, e) => ShowWindow();

            // Создаем контекстное меню
            var contextMenu = new ContextMenuStrip();
            
            var showMenuItem = new ToolStripMenuItem("Открыть настройки");
            showMenuItem.Click += (s, e) => ShowWindow();
            contextMenu.Items.Add(showMenuItem);

            contextMenu.Items.Add(new ToolStripSeparator());

            var pauseMenuItem = new ToolStripMenuItem("⏸ Пауза");
            pauseMenuItem.Click += (s, e) => 
            {
                _viewModel.IsPaused = !_viewModel.IsPaused;
                pauseMenuItem.Text = _viewModel.IsPaused ? "▶ Возобновить" : "⏸ Пауза";
            };
            contextMenu.Items.Add(pauseMenuItem);

            contextMenu.Items.Add(new ToolStripSeparator());

            var exitMenuItem = new ToolStripMenuItem("Выход");
            exitMenuItem.Click += (s, e) => ExitApplication();
            contextMenu.Items.Add(exitMenuItem);

            _notifyIcon.ContextMenuStrip = contextMenu;
        }

        private void ShowWindow()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Вместо закрытия скрываем окно в трей
            e.Cancel = true;
            Hide();
        }

        private void HideToTray_Click(object sender, RoutedEventArgs e)
        {
            Hide();
        }

        private void PauseResume_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.IsPaused = !_viewModel.IsPaused;
        }

        private void OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new SettingsWindow(_configManager);
            settingsWindow.Owner = this;
            if (settingsWindow.ShowDialog() == true)
            {
                _fileSorterService.UpdateSettings();
                
                System.Windows.MessageBox.Show(
                    "Настройки сохранены и применены!",
                    "FastP",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            }
        }

        private void ReloadRules_Click(object sender, RoutedEventArgs e)
        {
            _configManager.LoadRules();
            System.Windows.MessageBox.Show(
                "Правила сортировки перезагружены!",
                "FastP",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
        }

        private void ExitApplication()
        {
            _fileSorterService.Stop();
            _notifyIcon?.Dispose();
            System.Windows.Application.Current.Shutdown();
        }

        protected override void OnClosed(EventArgs e)
        {
            _notifyIcon?.Dispose();
            _fileSorterService.Stop();
            base.OnClosed(e);
        }
    }
}
