using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using TaggJack.Models;

namespace TaggJack.Services;

public class TemplateManagerService
{
	private static readonly string ConfigFileName = "flow-templates.json";

	private static readonly string ConfigDirectory = AppPaths.SharedDataDirectory;

	private static readonly string ConfigFilePath = Path.Combine(ConfigDirectory, ConfigFileName);

	private static readonly string DebugLogPath = AppPaths.GetSharedDataPath("template-manager-debug.log");

	private readonly object _lockObject = new object();

	private List<LoadedTemplate> _loadedTemplates = new List<LoadedTemplate>();

	public event EventHandler<TemplateManagerEventArgs>? TemplateAdded;

	public event EventHandler<TemplateManagerEventArgs>? TemplateRemoved;

	public event EventHandler<string>? ErrorOccurred;

	public TemplateManagerService()
	{
		try
		{
			File.AppendAllText(DebugLogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] TemplateManagerService initialized{Environment.NewLine}");
			File.AppendAllText(DebugLogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ConfigDirectory: {ConfigDirectory}{Environment.NewLine}");
			File.AppendAllText(DebugLogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ConfigFilePath: {ConfigFilePath}{Environment.NewLine}");
		}
		catch
		{
		}
		LoadTemplatesFromStorage();
	}

	public List<LoadedTemplate> GetLoadedTemplates()
	{
		lock (_lockObject)
		{
			return _loadedTemplates.ToList();
		}
	}

	public List<LoadedTemplate> GetEnabledTemplates()
	{
		lock (_lockObject)
		{
			return _loadedTemplates.Where((LoadedTemplate t) => t.Enabled).ToList();
		}
	}

	public bool AddTemplate(string filePath)
	{
		try
		{
			if (!File.Exists(filePath))
			{
				this.ErrorOccurred?.Invoke(this, "Template file not found: " + filePath);
				return false;
			}
			LoadedTemplate loadedTemplate;
			lock (_lockObject)
			{
				if (_loadedTemplates.Any((LoadedTemplate t) => string.Equals(t.Path, filePath, StringComparison.OrdinalIgnoreCase)))
				{
					this.ErrorOccurred?.Invoke(this, "Template already loaded: " + Path.GetFileName(filePath));
					return false;
				}
				loadedTemplate = new LoadedTemplate
				{
					Name = Path.GetFileNameWithoutExtension(filePath),
					Path = filePath,
					Enabled = true,
					DateAdded = DateTime.Now,
					LastPrint = DateTime.MinValue
				};
				if (!ValidateTemplateFile(filePath))
				{
					this.ErrorOccurred?.Invoke(this, "Invalid template file: " + Path.GetFileName(filePath));
					return false;
				}
				_loadedTemplates.Add(loadedTemplate);
				SaveTemplatesToStorage();
			}
			this.TemplateAdded?.Invoke(this, new TemplateManagerEventArgs(loadedTemplate));
			return true;
		}
		catch (Exception ex)
		{
			this.ErrorOccurred?.Invoke(this, "Error adding template: " + ex.Message);
			return false;
		}
	}

	public bool RemoveTemplate(string filePath)
	{
		try
		{
			LoadedTemplate loadedTemplate;
			lock (_lockObject)
			{
				loadedTemplate = _loadedTemplates.FirstOrDefault((LoadedTemplate t) => string.Equals(t.Path, filePath, StringComparison.OrdinalIgnoreCase));
				if (loadedTemplate == null)
				{
					this.ErrorOccurred?.Invoke(this, "Template not found: " + Path.GetFileName(filePath));
					return false;
				}
				_loadedTemplates.Remove(loadedTemplate);
				SaveTemplatesToStorage();
			}
			this.TemplateRemoved?.Invoke(this, new TemplateManagerEventArgs(loadedTemplate));
			return true;
		}
		catch (Exception ex)
		{
			this.ErrorOccurred?.Invoke(this, "Error removing template: " + ex.Message);
			return false;
		}
	}

	public void UpdateLastPrintTime(string filePath)
	{
		lock (_lockObject)
		{
			LoadedTemplate loadedTemplate = _loadedTemplates.FirstOrDefault((LoadedTemplate t) => string.Equals(t.Path, filePath, StringComparison.OrdinalIgnoreCase));
			if (loadedTemplate != null)
			{
				loadedTemplate.LastPrint = DateTime.Now;
				SaveTemplatesToStorage();
			}
		}
	}

	public void SetTemplateEnabled(string filePath, bool enabled)
	{
		lock (_lockObject)
		{
			LoadedTemplate loadedTemplate = _loadedTemplates.FirstOrDefault((LoadedTemplate t) => string.Equals(t.Path, filePath, StringComparison.OrdinalIgnoreCase));
			if (loadedTemplate != null)
			{
				loadedTemplate.Enabled = enabled;
				SaveTemplatesToStorage();
			}
		}
	}

	public void ValidateTemplates()
	{
		List<LoadedTemplate> list;
		lock (_lockObject)
		{
			list = _loadedTemplates.Where((LoadedTemplate t) => !File.Exists(t.Path)).ToList();
			foreach (LoadedTemplate item in list)
			{
				_loadedTemplates.Remove(item);
				this.ErrorOccurred?.Invoke(this, "Template file no longer exists: " + item.Name);
			}
			if (list.Count > 0)
			{
				SaveTemplatesToStorage();
			}
		}
		foreach (LoadedTemplate item2 in list)
		{
			this.TemplateRemoved?.Invoke(this, new TemplateManagerEventArgs(item2));
		}
	}

	private bool ValidateTemplateFile(string filePath)
	{
		try
		{
			string text = File.ReadAllText(filePath);
			return !string.IsNullOrWhiteSpace(text) && text.TrimStart().StartsWith("<");
		}
		catch
		{
			return false;
		}
	}

	private void LoadTemplatesFromStorage()
	{
		lock (_lockObject)
		{
			try
			{
				if (!File.Exists(ConfigFilePath))
				{
					_loadedTemplates = new List<LoadedTemplate>();
					return;
				}
				List<LoadedTemplate> list = JsonSerializer.Deserialize<List<LoadedTemplate>>(File.ReadAllText(ConfigFilePath));
				_loadedTemplates = list ?? new List<LoadedTemplate>();
			}
			catch (Exception ex)
			{
				this.ErrorOccurred?.Invoke(this, "Error loading templates from storage: " + ex.Message);
				_loadedTemplates = new List<LoadedTemplate>();
				return;
			}
		}
		ValidateTemplates();
	}

	private void SaveTemplatesToStorage()
	{
		try
		{
			File.AppendAllText(DebugLogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Saving templates to: {ConfigFilePath}{Environment.NewLine}");
			Directory.CreateDirectory(ConfigDirectory);
			string contents = JsonSerializer.Serialize(_loadedTemplates, new JsonSerializerOptions
			{
				WriteIndented = true
			});
			File.WriteAllText(ConfigFilePath, contents);
			File.AppendAllText(DebugLogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Successfully saved {_loadedTemplates.Count} template(s){Environment.NewLine}");
		}
		catch (Exception ex)
		{
			File.AppendAllText(DebugLogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ERROR saving: {ex.Message}{Environment.NewLine}");
			this.ErrorOccurred?.Invoke(this, "Error saving templates to storage: " + ex.Message);
		}
	}
}
