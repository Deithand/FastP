using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Forms;
using FastP.Services;

namespace FastP.Views
{
    public partial class SettingsWindow : Window
    {
        private readonly ConfigManager _configManager;
        private ObservableCollection<RuleItem> _rules;
        private readonly AutoStartService _autoStartService;

        public SettingsWindow(ConfigManager configManager)
        {
            InitializeComponent();
            _configManager = configManager;
            _autoStartService = new AutoStartService();
            LoadSettings();
        }

        private void LoadSettings()
        {
            SourcePathTextBox.Text = _configManager.Settings.SourcePath ?? "";
            NotificationsCheckBox.IsChecked = _configManager.Settings.EnableNotifications;
            DateSortingCheckBox.IsChecked = _configManager.Settings.OrganizeByDate;
            MinSizeTextBox.Text = _configManager.Settings.MinFileSize.ToString();
            
            // Load Autostart state from Registry, fallback to config
            AutostartCheckBox.IsChecked = _autoStartService.IsAutoStartEnabled();

            _rules = new ObservableCollection<RuleItem>(
                _configManager.Rules.Select(r => new RuleItem { Extension = r.Key, Folder = r.Value })
            );
            RulesListView.ItemsSource = _rules;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            // Save General Settings
            _configManager.Settings.SourcePath = SourcePathTextBox.Text;
            _configManager.Settings.EnableNotifications = NotificationsCheckBox.IsChecked ?? true;
            _configManager.Settings.OrganizeByDate = DateSortingCheckBox.IsChecked ?? false;
            _configManager.Settings.EnableAutostart = AutostartCheckBox.IsChecked ?? false;
            
            if (long.TryParse(MinSizeTextBox.Text, out long size))
            {
                _configManager.Settings.MinFileSize = size;
            }

            _configManager.SaveSettings();
            
            // Apply Autostart
            _autoStartService.SetAutoStart(_configManager.Settings.EnableAutostart);

            // Save Rules
            _configManager.Rules.Clear();
            foreach (var item in _rules)
            {
                if (!_configManager.Rules.ContainsKey(item.Extension))
                {
                    _configManager.Rules.Add(item.Extension, item.Folder);
                }
            }
            _configManager.SaveRules();

            DialogResult = true;
            Close();
        }

        private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as System.Windows.Controls.Button;
            if (btn != null) btn.IsEnabled = false;

            try
            {
                var updateService = new UpdateService();
                var update = await updateService.CheckForUpdatesAsync();

                if (update != null)
                {
                    var result = System.Windows.MessageBox.Show(
                        $"Доступна новая версия: {update.Version}\n\n{update.Description}\n\nОткрыть страницу загрузки?",
                        "Обновление FastP",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);

                    if (result == MessageBoxResult.Yes && !string.IsNullOrEmpty(update.DownloadUrl))
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = update.DownloadUrl,
                            UseShellExecute = true
                        });
                    }
                }
                else
                {
                    System.Windows.MessageBox.Show("У вас установлена последняя версия.", "FastP", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Ошибка проверки обновлений: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (btn != null) btn.IsEnabled = true;
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void AddRule_Click(object sender, RoutedEventArgs e)
        {
            var ext = NewExtTextBox.Text.Trim();
            var folder = NewFolderTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(ext) || string.IsNullOrWhiteSpace(folder))
            {
                System.Windows.MessageBox.Show("Введите расширение и папку.");
                return;
            }

            if (!ext.StartsWith("."))
                ext = "." + ext;

            if (_rules.Any(r => r.Extension.Equals(ext, StringComparison.OrdinalIgnoreCase)))
            {
                System.Windows.MessageBox.Show("Такое расширение уже есть.");
                return;
            }

            _rules.Add(new RuleItem { Extension = ext, Folder = folder });
            NewExtTextBox.Clear();
            NewFolderTextBox.Clear();
        }

        private void RemoveRule_Click(object sender, RoutedEventArgs e)
        {
            if (RulesListView.SelectedItem is RuleItem selected)
            {
                _rules.Remove(selected);
            }
        }

        private void BrowseSourcePath_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    SourcePathTextBox.Text = dialog.SelectedPath;
                }
            }
        }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }
    }

    public class RuleItem
    {
        public string Extension { get; set; } = "";
        public string Folder { get; set; } = "";
    }
}
