using Microsoft.Win32;
using System;
using System.Reflection;

namespace FastP.Services
{
    public class AutoStartService
    {
        private const string AppName = "FastP";
        private const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

        public void SetAutoStart(bool enable)
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    if (key == null) return;

                    if (enable)
                    {
                        var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                        if (exePath != null)
                        {
                            // Добавляем аргумент --autostart, чтобы приложение знало, как запускаться
                            key.SetValue(AppName, $"\"{exePath}\" --autostart");
                        }
                    }
                    else
                    {
                        if (key.GetValue(AppName) != null)
                        {
                            key.DeleteValue(AppName);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Логирование ошибки (можно добавить позже)
                System.Diagnostics.Debug.WriteLine($"Ошибка настройки автозагрузки: {ex.Message}");
            }
        }

        public bool IsAutoStartEnabled()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunKey, false))
                {
                    return key?.GetValue(AppName) != null;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}

