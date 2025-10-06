using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Hardcodet.Wpf.TaskbarNotification;
using EvoDesigner.Services;
using Microsoft.Win32;

namespace EvoDesigner.Services
{
    public class SystemTrayService : IDisposable
    {
        private TaskbarIcon? _notifyIcon;
        private readonly TemplateManagerService _templateManager;
        private readonly ScheduledPrintingService _scheduledPrinting;
        private Window? _mainWindow;

        public SystemTrayService(TemplateManagerService templateManager, ScheduledPrintingService scheduledPrinting)
        {
            _templateManager = templateManager;
            _scheduledPrinting = scheduledPrinting;
            
            // Subscribe to template manager events
            _templateManager.TemplateAdded += OnTemplateAdded;
            _templateManager.TemplateRemoved += OnTemplateRemoved;
        }

        /// <summary>
        /// Initialize the system tray icon and context menu
        /// </summary>
        public void Initialize(Window? mainWindow)
        {
            try
            {
                var logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] SystemTrayService initialization starting...";
                File.AppendAllText("tray-debug.log", logMessage + Environment.NewLine);
                
                System.Diagnostics.Debug.WriteLine("SystemTrayService: Starting initialization");
                Console.WriteLine("DEBUG: SystemTrayService initialization starting...");
                
                // Check if system tray is available
                if (!SystemParameters.IsTabletPC)
                {
                    Console.WriteLine("DEBUG: System tray should be available (not tablet PC)");
                    File.AppendAllText("tray-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] System tray should be available" + Environment.NewLine);
                }
                else
                {
                    Console.WriteLine("DEBUG: WARNING: Running on tablet PC - system tray may not be available");
                    File.AppendAllText("tray-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] WARNING: Tablet PC detected" + Environment.NewLine);
                }
                
                _mainWindow = mainWindow;
                
                _notifyIcon = new TaskbarIcon
                {
                    ToolTipText = "EvoDesigner - Flow Mode"
                };
                
                File.AppendAllText("tray-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] TaskbarIcon created without icon first" + Environment.NewLine);
                
                // Try to load icon from file path
                try
                {
                    var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.png");
                    File.AppendAllText("tray-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Looking for icon at: {iconPath}" + Environment.NewLine);
                    
                    if (File.Exists(iconPath))
                    {
                        _notifyIcon.IconSource = new BitmapImage(new Uri(iconPath, UriKind.Absolute));
                        Console.WriteLine($"DEBUG: Icon loaded from path: {iconPath}");
                        File.AppendAllText("tray-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Icon loaded from: {iconPath}" + Environment.NewLine);
                    }
                    else
                    {
                        Console.WriteLine($"DEBUG: Icon file not found at: {iconPath}");
                        File.AppendAllText("tray-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Icon file not found at: {iconPath}" + Environment.NewLine);
                        // Try pack URI as fallback
                        _notifyIcon.IconSource = new BitmapImage(new Uri("pack://application:,,,/icon.png"));
                        Console.WriteLine("DEBUG: Using pack URI for icon");
                        File.AppendAllText("tray-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Using pack URI for icon" + Environment.NewLine);
                    }
                }
                catch (Exception iconEx)
                {
                    Console.WriteLine($"DEBUG: Icon loading error: {iconEx.Message}");
                    File.AppendAllText("tray-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Icon error: {iconEx.Message}" + Environment.NewLine);
                }
                System.Diagnostics.Debug.WriteLine("SystemTrayService: TaskbarIcon created");
                Console.WriteLine("DEBUG: TaskbarIcon created with logo");

                // Create context menu
                var contextMenu = CreateContextMenu();
                _notifyIcon.ContextMenu = contextMenu;
                System.Diagnostics.Debug.WriteLine("SystemTrayService: Context menu created");
                Console.WriteLine("DEBUG: Context menu created");

                // Refresh menu before it opens to update scheduler status
                _notifyIcon.TrayContextMenuOpen += (s, e) => RefreshContextMenu();

                // Handle double-click to show main window
                _notifyIcon.TrayMouseDoubleClick += OnTrayIconDoubleClick;

                _notifyIcon.Visibility = Visibility.Visible;
                System.Diagnostics.Debug.WriteLine("SystemTrayService: Initialization completed successfully");
                Console.WriteLine("DEBUG: SystemTrayService initialization completed - tray icon should be visible");
                File.AppendAllText("tray-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] SystemTrayService initialization completed - tray icon should be visible" + Environment.NewLine);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SystemTrayService: Error during initialization: {ex.Message}");
                Console.WriteLine($"DEBUG: SystemTrayService error: {ex.Message}");
                File.AppendAllText("tray-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ERROR: {ex.Message}" + Environment.NewLine);
                System.Diagnostics.Debug.WriteLine($"SystemTrayService: Stack trace: {ex.StackTrace}");
                throw; // Re-throw to see the full error
            }
        }

        private ContextMenu CreateContextMenu()
        {
            var contextMenu = new ContextMenu();

            // Open Designer
            var openDesigner = new MenuItem 
            { 
                Header = "Open Designer",
                FontWeight = FontWeights.Bold
            };
            openDesigner.Click += (s, e) => ShowMainWindow();
            contextMenu.Items.Add(openDesigner);

            contextMenu.Items.Add(new Separator());

            // Add Template
            var addTemplate = new MenuItem { Header = "Add Template..." };
            addTemplate.Click += (s, e) => AddTemplate();
            contextMenu.Items.Add(addTemplate);

            // Loaded Templates
            var loadedTemplates = new MenuItem { Header = "Loaded Templates" };
            UpdateTemplateMenuItems(loadedTemplates);
            contextMenu.Items.Add(loadedTemplates);

            // Remove Template
            var removeTemplate = new MenuItem { Header = "Remove Template" };
            UpdateRemoveTemplateMenuItems(removeTemplate);
            contextMenu.Items.Add(removeTemplate);

            contextMenu.Items.Add(new Separator());

            // Run Now
            var runNow = new MenuItem { Header = "Run Now" };
            runNow.Click += async (s, e) => await RunNowClicked();
            contextMenu.Items.Add(runNow);

            // Start/Stop Scheduler (toggle based on current state)
            if (_scheduledPrinting.IsSchedulerActive)
            {
                var stopScheduler = new MenuItem { Header = "Stop Scheduler" };
                stopScheduler.Click += (s, e) => StopScheduler();
                contextMenu.Items.Add(stopScheduler);
            }
            else
            {
                var startScheduler = new MenuItem { Header = "Start Scheduler" };
                startScheduler.Click += (s, e) => StartScheduler();
                contextMenu.Items.Add(startScheduler);
            }

            // Scheduler Status (read-only indicator)
            var schedulerStatus = new MenuItem 
            { 
                Header = $"Status: {(_scheduledPrinting.IsSchedulerActive ? "Active" : "Stopped")}",
                IsEnabled = false
            };
            contextMenu.Items.Add(schedulerStatus);

            contextMenu.Items.Add(new Separator());

            // Exit
            var exit = new MenuItem { Header = "Exit" };
            exit.Click += (s, e) => ExitApplication();
            contextMenu.Items.Add(exit);

            return contextMenu;
        }

        private void UpdateTemplateMenuItems(MenuItem loadedTemplatesMenuItem)
        {
            loadedTemplatesMenuItem.Items.Clear();

            var templates = _templateManager.GetLoadedTemplates();
            if (!templates.Any())
            {
                var noTemplates = new MenuItem 
                { 
                    Header = "No templates loaded",
                    IsEnabled = false
                };
                loadedTemplatesMenuItem.Items.Add(noTemplates);
                return;
            }

            foreach (var template in templates)
            {
                var templateItem = new MenuItem 
                { 
                    Header = $"{template.Name} ({template.Path})",
                    IsEnabled = false
                };
                loadedTemplatesMenuItem.Items.Add(templateItem);
            }
        }

        private void UpdateRemoveTemplateMenuItems(MenuItem removeTemplateMenuItem)
        {
            removeTemplateMenuItem.Items.Clear();

            var templates = _templateManager.GetLoadedTemplates();
            if (!templates.Any())
            {
                var noTemplates = new MenuItem 
                { 
                    Header = "No templates to remove",
                    IsEnabled = false
                };
                removeTemplateMenuItem.Items.Add(noTemplates);
                return;
            }

            foreach (var template in templates)
            {
                var templateItem = new MenuItem { Header = template.Name };
                templateItem.Click += (s, e) => RemoveTemplate(template.Path);
                removeTemplateMenuItem.Items.Add(templateItem);
            }
        }

        private void AddTemplate()
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "XML Templates (*.xml)|*.xml|All files (*.*)|*.*",
                Title = "Select Template File"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    _templateManager.AddTemplate(openFileDialog.FileName);
                    MessageBox.Show($"Template '{Path.GetFileNameWithoutExtension(openFileDialog.FileName)}' added successfully.", 
                        "Template Added", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to add template: {ex.Message}", 
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void RemoveTemplate(string filePath)
        {
            try
            {
                var templateName = Path.GetFileNameWithoutExtension(filePath);
                var result = MessageBox.Show($"Are you sure you want to remove template '{templateName}'?", 
                    "Confirm Removal", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    _templateManager.RemoveTemplate(filePath);
                    MessageBox.Show($"Template '{templateName}' removed successfully.", 
                        "Template Removed", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to remove template: {ex.Message}", 
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnTrayIconDoubleClick(object sender, RoutedEventArgs e)
        {
            ShowMainWindow();
        }

        private void ShowMainWindow()
        {
            if (_mainWindow != null)
            {
                _mainWindow.Show();
                _mainWindow.WindowState = WindowState.Normal;
                _mainWindow.Activate();
            }
            else
            {
                // Try to find the main window from the application
                var app = System.Windows.Application.Current;
                if (app?.MainWindow != null)
                {
                    _mainWindow = app.MainWindow;
                    _mainWindow.Show();
                    _mainWindow.WindowState = WindowState.Normal;
                    _mainWindow.Activate();
                }
                else
                {
                    // If still no main window, try to find any window
                    var windows = app?.Windows;
                    if (windows != null && windows.Count > 0)
                    {
                        foreach (Window window in windows)
                        {
                            if (window.GetType().Name == "MainWindow")
                            {
                                _mainWindow = window;
                                window.Show();
                                window.WindowState = WindowState.Normal;
                                window.Activate();
                                break;
                            }
                        }
                    }
                }
            }
        }

        public void HideToTray()
        {
            _mainWindow?.Hide();
        }

        /// <summary>
        /// Set the main window reference if it wasn't available during initialization
        /// </summary>
        public void SetMainWindow(Window mainWindow)
        {
            _mainWindow = mainWindow;
        }

        private void ExitApplication()
        {
            // Check if scheduler is active and warn user
            if (_scheduledPrinting.IsSchedulerActive)
            {
                var result = MessageBox.Show(
                    "The scheduled printing service is currently active. Exiting the application will stop all scheduled print jobs.\n\n" +
                    "Are you sure you want to exit?",
                    "EvoDesigner - Confirm Exit",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning,
                    MessageBoxResult.No);

                if (result != MessageBoxResult.Yes)
                {
                    return; // User cancelled exit
                }
            }

            // Try to call the MainWindow's ExplicitExit method for proper save prompts
            if (_mainWindow is MainWindow mainWindow)
            {
                mainWindow.ExplicitExit();
            }
            else
            {
                // If we can't find the MainWindow, search for it
                var app = System.Windows.Application.Current;
                if (app?.MainWindow is MainWindow mainWin)
                {
                    mainWin.ExplicitExit();
                }
                else
                {
                    // Try to find MainWindow in all windows
                    var windows = app?.Windows;
                    if (windows != null)
                    {
                        foreach (Window window in windows)
                        {
                            if (window is MainWindow mw)
                            {
                                mw.ExplicitExit();
                                return;
                            }
                        }
                    }
                    // Fallback to immediate shutdown if no MainWindow found
                    Application.Current.Shutdown();
                }
            }
        }

        private async Task RunNowClicked()
        {
            try
            {
                File.AppendAllText("run-now-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Run Now clicked" + Environment.NewLine);
                
                // Check if there are any enabled templates
                var enabledTemplates = _templateManager.GetEnabledTemplates();
                File.AppendAllText("run-now-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Found {enabledTemplates.Count} enabled templates" + Environment.NewLine);
                
                if (enabledTemplates.Count == 0)
                {
                    // Show notification that no templates are available
                    _notifyIcon?.ShowBalloonTip("EvoDesigner - Run Now", 
                        "No templates are loaded or enabled for printing. Please add templates using 'Add Template' from the tray menu.", 
                        Hardcodet.Wpf.TaskbarNotification.BalloonIcon.Info);
                    File.AppendAllText("run-now-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] No templates available - showing balloon tip" + Environment.NewLine);
                    return;
                }

                // Show notification that printing has started
                _notifyIcon?.ShowBalloonTip("EvoDesigner - Run Now", 
                    $"Starting print cycle for {enabledTemplates.Count} template(s)...", 
                    Hardcodet.Wpf.TaskbarNotification.BalloonIcon.Info);
                File.AppendAllText("run-now-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Starting print cycle for {enabledTemplates.Count} templates" + Environment.NewLine);

                // Run the print cycle
                await _scheduledPrinting.RunNow();
                File.AppendAllText("run-now-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Print cycle completed" + Environment.NewLine);

                // Show completion notification
                _notifyIcon?.ShowBalloonTip("EvoDesigner - Run Now", 
                    "Print cycle completed successfully.", 
                    Hardcodet.Wpf.TaskbarNotification.BalloonIcon.Info);
            }
            catch (Exception ex)
            {
                File.AppendAllText("run-now-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ERROR: {ex.Message}" + Environment.NewLine);
                // Show error notification
                _notifyIcon?.ShowBalloonTip("EvoDesigner - Run Now", 
                    $"Error during print cycle: {ex.Message}", 
                    Hardcodet.Wpf.TaskbarNotification.BalloonIcon.Error);
            }
        }

        private void StartScheduler()
        {
            try
            {
                _scheduledPrinting.Start();
                
                // Show notification
                _notifyIcon?.ShowBalloonTip("EvoDesigner - Scheduler", 
                    "Scheduler started successfully.", 
                    Hardcodet.Wpf.TaskbarNotification.BalloonIcon.Info);
                
                // Refresh menu to update status
                RefreshContextMenu();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to start scheduler: {ex.Message}", 
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void StopScheduler()
        {
            try
            {
                _scheduledPrinting.Stop();
                
                // Show notification
                _notifyIcon?.ShowBalloonTip("EvoDesigner - Scheduler", 
                    "Scheduler stopped successfully.", 
                    Hardcodet.Wpf.TaskbarNotification.BalloonIcon.Info);
                
                // Refresh menu to update status
                RefreshContextMenu();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to stop scheduler: {ex.Message}", 
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnTemplateAdded(object? sender, TemplateManagerEventArgs e)
        {
            // Refresh context menu
            RefreshContextMenu();
        }

        private void OnTemplateRemoved(object? sender, TemplateManagerEventArgs e)
        {
            // Refresh context menu
            RefreshContextMenu();
        }

        private void RefreshContextMenu()
        {
            if (_notifyIcon?.ContextMenu != null)
            {
                _notifyIcon.ContextMenu = CreateContextMenu();
            }
        }

        public void Dispose()
        {
            // Unsubscribe from events
            _templateManager.TemplateAdded -= OnTemplateAdded;
            _templateManager.TemplateRemoved -= OnTemplateRemoved;

            // Dispose NotifyIcon
            _notifyIcon?.Dispose();
            _notifyIcon = null;
        }
    }
}