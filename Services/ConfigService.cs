using System;
using System.IO;
using System.Text.Json;
using EvoDesigner.Models;

namespace EvoDesigner.Services
{
    public static class ConfigService
    {
        private static readonly string ConfigFilePath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "config.json"
        );

        public static AppConfig LoadConfig()
        {
            try
            {
                if (File.Exists(ConfigFilePath))
                {
                    var json = File.ReadAllText(ConfigFilePath);
                    var config = JsonSerializer.Deserialize<AppConfig>(json);
                    return config ?? new AppConfig();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading config: {ex.Message}");
            }

            return new AppConfig();
        }

        public static void SaveConfig(AppConfig config)
        {
            try
            {
                var directory = Path.GetDirectoryName(ConfigFilePath);
                if (directory != null && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                var json = JsonSerializer.Serialize(config, options);
                File.WriteAllText(ConfigFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving config: {ex.Message}");
            }
        }
    }
}
