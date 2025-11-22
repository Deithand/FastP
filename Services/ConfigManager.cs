using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace FastP.Services
{
    public class AppSettings
    {
        public string? SourcePath { get; set; }
        public long MinFileSize { get; set; } = 0; // in bytes
        public bool EnableNotifications { get; set; } = true;
        public bool EnableAutostart { get; set; } = false;
    }

    public class ConfigManager
    {
        private const string ConfigFileName = "rules.json";
        private const string SettingsFileName = "settings.json";
        
        private Dictionary<string, string> _rules;
        private AppSettings _settings;

        public ConfigManager()
        {
            _rules = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _settings = new AppSettings();
            LoadRules();
            LoadSettings();
        }

        public Dictionary<string, string> Rules => _rules;
        public AppSettings Settings => _settings;

        public void LoadRules()
        {
            try
            {
                if (File.Exists(ConfigFileName))
                {
                    var json = File.ReadAllText(ConfigFileName);
                    _rules = JsonSerializer.Deserialize<Dictionary<string, string>>(json) 
                        ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                }
                else
                {
                    // Создаем файл с правилами по умолчанию
                    CreateDefaultRules();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки правил: {ex.Message}");
                CreateDefaultRules();
            }
        }

        public void LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsFileName))
                {
                    var json = File.ReadAllText(SettingsFileName);
                    _settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
                else
                {
                    _settings = new AppSettings();
                    SaveSettings();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки настроек: {ex.Message}");
                _settings = new AppSettings();
            }
        }

        private void CreateDefaultRules()
        {
            _rules = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { ".jpg", "Images" },
                { ".jpeg", "Images" },
                { ".png", "Images" },
                { ".gif", "Images" },
                { ".bmp", "Images" },
                { ".webp", "Images" },
                { ".svg", "Images" },
                { ".ico", "Images" },
                { ".doc", "Documents" },
                { ".docx", "Documents" },
                { ".pdf", "Documents" },
                { ".txt", "Documents" },
                { ".rtf", "Documents" },
                { ".xls", "Documents" },
                { ".xlsx", "Documents" },
                { ".ppt", "Documents" },
                { ".pptx", "Documents" },
                { ".exe", "Installers" },
                { ".msi", "Installers" },
                { ".msix", "Installers" },
                { ".zip", "Archives" },
                { ".rar", "Archives" },
                { ".7z", "Archives" },
                { ".tar", "Archives" },
                { ".gz", "Archives" },
                // Audio
                { ".mp3", "Audio" },
                { ".wav", "Audio" },
                { ".flac", "Audio" },
                { ".ogg", "Audio" },
                { ".aac", "Audio" },
                { ".wma", "Audio" },
                { ".m4a", "Audio" }
            };

            SaveRules();
        }

        public void SaveRules()
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(_rules, options);
                File.WriteAllText(ConfigFileName, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка сохранения правил: {ex.Message}");
            }
        }

        public void SaveSettings()
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(_settings, options);
                File.WriteAllText(SettingsFileName, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка сохранения настроек: {ex.Message}");
            }
        }

        public string? GetTargetFolder(string extension)
        {
            if (string.IsNullOrEmpty(extension))
                return null;

            // Нормализуем расширение (добавляем точку если её нет)
            if (!extension.StartsWith("."))
                extension = "." + extension;

            return _rules.TryGetValue(extension, out var folder) ? folder : null;
        }
    }
}
