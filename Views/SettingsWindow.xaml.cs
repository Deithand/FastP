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

        public SettingsWindow(ConfigManager configManager)
        {
            InitializeComponent();
            _configManager = configManager;
            LoadSettings();
        }

        private void LoadSettings()
        {
            SourcePathTextBox.Text = _configManager.Settings.SourcePath ?? "";
            NotificationsCheckBox.IsChecked = _configManager.Settings.EnableNotifications;
            MinSizeTextBox.Text = _configManager.Settings.MinFileSize.ToString();

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
            
            if (long.TryParse(MinSizeTextBox.Text, out long size))
            {
                _configManager.Settings.MinFileSize = size;
            }

            _configManager.SaveSettings();

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

