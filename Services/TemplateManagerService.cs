using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using EvoDesigner.Models;

namespace EvoDesigner.Services
{
    public class TemplateManagerService
    {
        private static readonly string ConfigFileName = "flow-templates.json";
        private static readonly string ConfigDirectory = AppDomain.CurrentDomain.BaseDirectory;
        private static readonly string ConfigFilePath = Path.Combine(ConfigDirectory, ConfigFileName);

        private List<LoadedTemplate> _loadedTemplates = new();
        
        public event EventHandler<TemplateManagerEventArgs>? TemplateAdded;
        public event EventHandler<TemplateManagerEventArgs>? TemplateRemoved;
        public event EventHandler<string>? ErrorOccurred;

        public TemplateManagerService()
        {
            // Log the config file path for debugging
            try
            {
                File.AppendAllText("template-manager-debug.log", 
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] TemplateManagerService initialized{Environment.NewLine}");
                File.AppendAllText("template-manager-debug.log", 
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ConfigDirectory: {ConfigDirectory}{Environment.NewLine}");
                File.AppendAllText("template-manager-debug.log", 
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ConfigFilePath: {ConfigFilePath}{Environment.NewLine}");
            }
            catch { }
            
            LoadTemplatesFromStorage();
        }

        /// <summary>
        /// Gets all loaded templates
        /// </summary>
        public List<LoadedTemplate> GetLoadedTemplates()
        {
            return _loadedTemplates.ToList();
        }

        /// <summary>
        /// Gets all enabled templates
        /// </summary>
        public List<LoadedTemplate> GetEnabledTemplates()
        {
            return _loadedTemplates.Where(t => t.Enabled).ToList();
        }

        /// <summary>
        /// Adds a template from file path
        /// </summary>
        /// <param name="filePath">Path to template file</param>
        /// <returns>True if successfully added</returns>
        public bool AddTemplate(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    ErrorOccurred?.Invoke(this, $"Template file not found: {filePath}");
                    return false;
                }

                // Check if already loaded
                if (_loadedTemplates.Any(t => string.Equals(t.Path, filePath, StringComparison.OrdinalIgnoreCase)))
                {
                    ErrorOccurred?.Invoke(this, $"Template already loaded: {Path.GetFileName(filePath)}");
                    return false;
                }

                var template = new LoadedTemplate
                {
                    Name = Path.GetFileNameWithoutExtension(filePath),
                    Path = filePath,
                    Enabled = true,
                    DateAdded = DateTime.Now,
                    LastPrint = DateTime.MinValue
                };

                // Validate template file
                if (!ValidateTemplateFile(filePath))
                {
                    ErrorOccurred?.Invoke(this, $"Invalid template file: {Path.GetFileName(filePath)}");
                    return false;
                }

                _loadedTemplates.Add(template);
                SaveTemplatesToStorage();
                
                TemplateAdded?.Invoke(this, new TemplateManagerEventArgs(template));
                return true;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Error adding template: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Removes a template by path
        /// </summary>
        /// <param name="filePath">Path to template file</param>
        /// <returns>True if successfully removed</returns>
        public bool RemoveTemplate(string filePath)
        {
            try
            {
                var template = _loadedTemplates.FirstOrDefault(t => 
                    string.Equals(t.Path, filePath, StringComparison.OrdinalIgnoreCase));

                if (template == null)
                {
                    ErrorOccurred?.Invoke(this, $"Template not found: {Path.GetFileName(filePath)}");
                    return false;
                }

                _loadedTemplates.Remove(template);
                SaveTemplatesToStorage();
                
                TemplateRemoved?.Invoke(this, new TemplateManagerEventArgs(template));
                return true;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Error removing template: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Updates the last print time for a template
        /// </summary>
        /// <param name="filePath">Template file path</param>
        public void UpdateLastPrintTime(string filePath)
        {
            var template = _loadedTemplates.FirstOrDefault(t => 
                string.Equals(t.Path, filePath, StringComparison.OrdinalIgnoreCase));

            if (template != null)
            {
                template.LastPrint = DateTime.Now;
                SaveTemplatesToStorage();
            }
        }

        /// <summary>
        /// Enables or disables a template
        /// </summary>
        /// <param name="filePath">Template file path</param>
        /// <param name="enabled">Enable state</param>
        public void SetTemplateEnabled(string filePath, bool enabled)
        {
            var template = _loadedTemplates.FirstOrDefault(t => 
                string.Equals(t.Path, filePath, StringComparison.OrdinalIgnoreCase));

            if (template != null)
            {
                template.Enabled = enabled;
                SaveTemplatesToStorage();
            }
        }

        /// <summary>
        /// Validates that all loaded templates still exist
        /// </summary>
        public void ValidateTemplates()
        {
            var toRemove = new List<LoadedTemplate>();

            foreach (var template in _loadedTemplates)
            {
                if (!File.Exists(template.Path))
                {
                    toRemove.Add(template);
                    ErrorOccurred?.Invoke(this, $"Template file no longer exists: {template.Name}");
                }
            }

            foreach (var template in toRemove)
            {
                _loadedTemplates.Remove(template);
                TemplateRemoved?.Invoke(this, new TemplateManagerEventArgs(template));
            }

            if (toRemove.Count > 0)
            {
                SaveTemplatesToStorage();
            }
        }

        private bool ValidateTemplateFile(string filePath)
        {
            try
            {
                // Basic validation - check if file can be read as XML
                var content = File.ReadAllText(filePath);
                return !string.IsNullOrWhiteSpace(content) && content.TrimStart().StartsWith("<");
            }
            catch
            {
                return false;
            }
        }

        private void LoadTemplatesFromStorage()
        {
            try
            {
                if (!File.Exists(ConfigFilePath))
                {
                    _loadedTemplates = new List<LoadedTemplate>();
                    return;
                }

                var json = File.ReadAllText(ConfigFilePath);
                var templates = JsonSerializer.Deserialize<List<LoadedTemplate>>(json);
                _loadedTemplates = templates ?? new List<LoadedTemplate>();

                // Validate templates on load
                ValidateTemplates();
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Error loading templates from storage: {ex.Message}");
                _loadedTemplates = new List<LoadedTemplate>();
            }
        }

        private void SaveTemplatesToStorage()
        {
            try
            {
                // Log save attempt
                File.AppendAllText("template-manager-debug.log", 
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Saving templates to: {ConfigFilePath}{Environment.NewLine}");
                
                // Ensure directory exists
                Directory.CreateDirectory(ConfigDirectory);

                var json = JsonSerializer.Serialize(_loadedTemplates, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(ConfigFilePath, json);
                
                File.AppendAllText("template-manager-debug.log", 
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Successfully saved {_loadedTemplates.Count} template(s){Environment.NewLine}");
            }
            catch (Exception ex)
            {
                File.AppendAllText("template-manager-debug.log", 
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ERROR saving: {ex.Message}{Environment.NewLine}");
                ErrorOccurred?.Invoke(this, $"Error saving templates to storage: {ex.Message}");
            }
        }
    }

    public class TemplateManagerEventArgs : EventArgs
    {
        public LoadedTemplate Template { get; }

        public TemplateManagerEventArgs(LoadedTemplate template)
        {
            Template = template;
        }
    }
}