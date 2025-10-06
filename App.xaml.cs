using System;
using System.Configuration;
using System.Data;
using System.IO;
using System.Windows;
using System.Threading.Tasks;
using EvoDesigner.Services;
using EvoDesigner.Models;
using EvoDesigner.UI;

namespace EvoDesigner;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    private SystemTrayService? _systemTrayService;
    private ScheduledPrintingService? _scheduledPrintingService;
    private TemplateManagerService? _templateManagerService;
    private UI.SplashScreen? _splashScreen;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        
        // Show splash screen
        _splashScreen = new UI.SplashScreen();
        _splashScreen.ShowSplash();
        
        // Add immediate debug output
        try
        {
            File.AppendAllText("startup-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] OnStartup called" + Environment.NewLine);
        }
        catch { }
        
        // Load configuration to determine mode
        _splashScreen.UpdateStatus("Loading configuration...");
        var config = ConfigService.LoadConfig();
        
        try
        {
            File.AppendAllText("startup-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Config loaded, mode: {config.Mode}" + Environment.NewLine);
        }
        catch { }
        
        if (config.Mode == "flow")
        {
            try
            {
                File.AppendAllText("startup-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Calling InitializeFlowMode" + Environment.NewLine);
            }
            catch { }
            _splashScreen.UpdateStatus("Initializing flow mode...");
            InitializeFlowMode(config);
        }
        
        // Test LinkedImage functionality
        // NOTE: Commented out to prevent creating duplicate MainWindow instances during startup
        // _splashScreen.UpdateStatus("Running tests...");
        // TestLinkedImage.TestCreateLinkedImageElement();
        
        // Close splash screen
        _splashScreen.UpdateStatus("Starting application...");
        Task.Delay(500).ContinueWith(_ =>
        {
            Dispatcher.Invoke(() =>
            {
                _splashScreen.CloseSplash();
            });
        });
    }

    private void InitializeFlowMode(AppConfig config)
    {
        try
        {
            Console.WriteLine($"DEBUG: InitializeFlowMode called, SystemTrayEnabled: {config.FlowMode.SystemTrayEnabled}");
            File.AppendAllText("app-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] InitializeFlowMode called" + Environment.NewLine);
            
            // Initialize flow mode services
            _splashScreen?.UpdateStatus("Initializing services...");
            _templateManagerService = new TemplateManagerService();
            _scheduledPrintingService = new ScheduledPrintingService(_templateManagerService, config.FlowMode.PrintingIntervalSeconds);
            _systemTrayService = new SystemTrayService(_templateManagerService, _scheduledPrintingService);

            // Initialize system tray
            if (config.FlowMode.SystemTrayEnabled)
            {
                Console.WriteLine("DEBUG: System tray enabled, initializing immediately");
                File.AppendAllText("app-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] System tray enabled, initializing immediately" + Environment.NewLine);
                
                _splashScreen?.UpdateStatus("Initializing system tray...");
                // Initialize system tray immediately - it doesn't need MainWindow
                _systemTrayService.Initialize(null!); // We'll modify the Initialize method to handle null MainWindow
                
                // Set up a timer to set the MainWindow reference once it's available
                var timer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(100)
                };
                timer.Tick += (s, e) =>
                {
                    if (this.MainWindow != null)
                    {
                        _systemTrayService.SetMainWindow(this.MainWindow);
                        timer.Stop();
                    }
                };
                timer.Start();
            }
            else
            {
                Console.WriteLine("DEBUG: System tray disabled in config");
            }

            // Start scheduled printing service
            if (config.FlowMode.ScheduledPrintingEnabled)
            {
                _splashScreen?.UpdateStatus("Starting scheduler...");
                _scheduledPrintingService.Start();
            }

            // Set up event handlers
            _scheduledPrintingService.LogMessage += OnScheduledPrintingLog;
            
            _splashScreen?.UpdateStatus("Flow mode ready!");
            System.Diagnostics.Debug.WriteLine("Flow mode initialized successfully");
            Console.WriteLine("DEBUG: Flow mode initialized successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error initializing flow mode: {ex.Message}");
            Console.WriteLine($"DEBUG: Error initializing flow mode: {ex.Message}");
            // Continue with normal startup even if flow mode fails
        }
    }

    private void OnScheduledPrintingLog(object? sender, string message)
    {
        System.Diagnostics.Debug.WriteLine($"[Scheduled Printing] {message}");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Clean up flow mode services
        _systemTrayService?.Dispose();
        _scheduledPrintingService?.Stop();
        
        base.OnExit(e);
    }
}

