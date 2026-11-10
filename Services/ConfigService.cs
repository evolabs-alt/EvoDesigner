using System;
using System.IO;
using System.Text.Json;
using TaggJack.Models;

namespace TaggJack.Services;

public static class ConfigService
{
	private static string? _activeConfigFilePath;

	private static readonly JsonSerializerOptions ReadOptions = new JsonSerializerOptions
	{
		PropertyNameCaseInsensitive = true
	};

	private static readonly JsonSerializerOptions WriteOptions = new JsonSerializerOptions
	{
		WriteIndented = true
	};

	public static string GetConfigFilePath()
	{
		if (!string.IsNullOrEmpty(_activeConfigFilePath))
		{
			return _activeConfigFilePath;
		}
		string text = Path.Combine(AppContext.BaseDirectory, "config.json");
		if (File.Exists(text))
		{
			return text;
		}
		return AppPaths.GetSharedDataPath("config.json");
	}

	public static AppConfig LoadConfig()
	{
		string path = (_activeConfigFilePath = GetConfigFilePath());
		try
		{
			if (File.Exists(path))
			{
				return JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(path), ReadOptions) ?? new AppConfig();
			}
		}
		catch (Exception)
		{
		}
		return new AppConfig();
	}

	public static void SaveConfig(AppConfig config)
	{
		string path = (_activeConfigFilePath = GetConfigFilePath());
		try
		{
			string directoryName = Path.GetDirectoryName(path);
			if (directoryName != null && !Directory.Exists(directoryName))
			{
				Directory.CreateDirectory(directoryName);
			}
			string contents = JsonSerializer.Serialize(config, WriteOptions);
			File.WriteAllText(path, contents);
		}
		catch (Exception)
		{
		}
	}
}
