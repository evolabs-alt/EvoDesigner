using System;
using System.CodeDom.Compiler;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using TaggJack.Models;
using TaggJack.Services;
using TaggJack.Services.License;
using TaggJack.UI;

namespace TaggJack;

public partial class App : Application
{
	private const string SingleInstanceMutexName = "Global\\TaggJackSingleInstance";

	private SystemTrayService? _systemTrayService;

	private ScheduledPrintingService? _scheduledPrintingService;

	private TemplateManagerService? _templateManagerService;

	private TaggJack.UI.SplashScreen? _splashScreen;

	private Mutex? _singleInstanceMutex;

	private bool _ownsSingleInstanceMutex;

	private bool _startInTrayMode;

	protected override void OnStartup(StartupEventArgs e)
	{
		base.DispatcherUnhandledException += delegate(object _, DispatcherUnhandledExceptionEventArgs args)
		{
			try
			{
				File.AppendAllText("startup-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Unhandled UI exception: {args.Exception}" + Environment.NewLine);
			}
			catch
			{
			}
			MessageBox.Show("An unexpected error occurred:\n\n" + args.Exception.Message, "TaggJack - Error", MessageBoxButton.OK, MessageBoxImage.Hand);
			args.Handled = true;
		};
		_singleInstanceMutex = new Mutex(initiallyOwned: true, "Global\\TaggJackSingleInstance", out var createdNew);
		_ownsSingleInstanceMutex = createdNew;
		if (!createdNew)
		{
			Shutdown();
			return;
		}
		_startInTrayMode = e.Args.Any((string arg) => arg.Equals("--tray", StringComparison.OrdinalIgnoreCase) || arg.Equals("--minimized", StringComparison.OrdinalIgnoreCase));
		base.ShutdownMode = ShutdownMode.OnExplicitShutdown;
		base.OnStartup(e);
		_splashScreen = new TaggJack.UI.SplashScreen();
		_splashScreen.ShowSplash();
		try
		{
			File.AppendAllText("startup-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] OnStartup called" + Environment.NewLine);
		}
		catch
		{
		}
		_splashScreen.UpdateStatus("Checking license...");
		if (!CheckLicense())
		{
			_splashScreen.CloseSplash();
			Shutdown();
			return;
		}
		_splashScreen.UpdateStatus("Loading configuration...");
		AppConfig appConfig = ConfigService.LoadConfig();
		try
		{
			File.AppendAllText("startup-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Config loaded, mode: {appConfig.Mode}" + Environment.NewLine);
		}
		catch
		{
		}
		if (appConfig.Mode == "flow")
		{
			try
			{
				File.AppendAllText("startup-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Calling InitializeFlowMode" + Environment.NewLine);
			}
			catch
			{
			}
			_splashScreen.UpdateStatus("Initializing flow mode...");
			InitializeFlowMode(appConfig);
		}
		if (!(base.MainWindow is MainWindow))
		{
			try
			{
				MainWindow mainWindow = (MainWindow)(base.MainWindow = new MainWindow());
				mainWindow.Show();
				base.ShutdownMode = ShutdownMode.OnMainWindowClose;
				if (appConfig.Mode == "flow" && (_startInTrayMode || appConfig.FlowMode.StartMinimizedToTray))
				{
					mainWindow.Hide();
				}
			}
			catch (Exception ex)
			{
				File.AppendAllText("startup-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] MainWindow startup failed: {ex}" + Environment.NewLine);
				_splashScreen?.CloseSplash();
				MessageBox.Show("Failed to open the main window:\n\n" + ex.Message, "TaggJack - Startup Error", MessageBoxButton.OK, MessageBoxImage.Hand);
				Shutdown();
				return;
			}
		}
		_splashScreen.UpdateStatus("Starting application...");
		Task.Delay(500).ContinueWith(delegate
		{
			base.Dispatcher.Invoke(delegate
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
			_splashScreen?.UpdateStatus("Initializing services...");
			_templateManagerService = new TemplateManagerService();
			_scheduledPrintingService = new ScheduledPrintingService(_templateManagerService, config.FlowMode.PrintingIntervalSeconds, new DesktopPrintExecutor());
			_systemTrayService = new SystemTrayService(_templateManagerService, _scheduledPrintingService, config.FlowMode.UseWindowsServiceControl, config.FlowMode.WindowsServiceName);
			if (config.FlowMode.SystemTrayEnabled)
			{
				Console.WriteLine("DEBUG: System tray enabled, initializing immediately");
				File.AppendAllText("app-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] System tray enabled, initializing immediately" + Environment.NewLine);
				_splashScreen?.UpdateStatus("Initializing system tray...");
				_systemTrayService.Initialize(null);
				DispatcherTimer timer = new DispatcherTimer
				{
					Interval = TimeSpan.FromMilliseconds(100)
				};
				timer.Tick += delegate
				{
					if (base.MainWindow is MainWindow mainWindow)
					{
						_systemTrayService.SetMainWindow(mainWindow);
						timer.Stop();
					}
				};
				timer.Start();
			}
			else
			{
				Console.WriteLine("DEBUG: System tray disabled in config");
			}
			if (config.FlowMode.ScheduledPrintingEnabled && !config.FlowMode.UseWindowsServiceControl)
			{
				_splashScreen?.UpdateStatus("Starting scheduler...");
				_scheduledPrintingService.Start();
			}
			_scheduledPrintingService.LogMessage += OnScheduledPrintingLog;
			_splashScreen?.UpdateStatus("Flow mode ready!");
			Console.WriteLine("DEBUG: Flow mode initialized successfully");
		}
		catch (Exception ex)
		{
			Console.WriteLine("DEBUG: Error initializing flow mode: " + ex.Message);
		}
	}

	private void OnScheduledPrintingLog(object? sender, string message)
	{
	}

	private bool CheckLicense()
	{
		try
		{
			LicenseEvaluation eval = PortalLicenseService.Instance.GetStatus();

			// Always try portal refresh when we already have a local lease so renewals
			// (new expiresAt) replace the cached JWT claims immediately.
			if (eval.Allowed || eval.Status is LicenseStatus.Expiring or LicenseStatus.Grace or LicenseStatus.Pending)
			{
				try
				{
					_splashScreen?.UpdateStatus("Refreshing license with Evolabs...");
					PortalLicenseService.Instance.CheckWithPortalAsync().GetAwaiter().GetResult();
					eval = PortalLicenseService.Instance.GetStatus();
					File.AppendAllText("startup-debug.log",
						$"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Portal refresh: {eval.Status} allowed={eval.Allowed} exp={eval.LicenseExpiresAt}" + Environment.NewLine);
				}
				catch (Exception refreshEx)
				{
					File.AppendAllText("startup-debug.log",
						$"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Portal refresh failed (using cache): {refreshEx.Message}" + Environment.NewLine);
					eval = PortalLicenseService.Instance.GetStatus();
				}
			}

			if (eval.Allowed)
			{
				File.AppendAllText("startup-debug.log",
					$"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] License allowed: {eval.Status}" + Environment.NewLine);
				return true;
			}
			_splashScreen?.CloseSplash();
			var licenseActivationWindow = new LicenseActivationWindow();
			if (licenseActivationWindow.ShowDialog() == true && licenseActivationWindow.IsActivated)
			{
				File.AppendAllText("startup-debug.log",
					$"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] License activated via portal" + Environment.NewLine);
				_splashScreen = new TaggJack.UI.SplashScreen();
				_splashScreen.ShowSplash();
				return true;
			}
			File.AppendAllText("startup-debug.log",
				$"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] License activation cancelled, shutting down" + Environment.NewLine);
			return false;
		}
		catch (Exception ex)
		{
			File.AppendAllText("startup-debug.log",
				$"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] License check error: {ex.Message}" + Environment.NewLine);
			MessageBox.Show(
				"License verification failed. TaggJack cannot start without a valid Evolabs license.\n\n" + ex.Message,
				"License Error",
				MessageBoxButton.OK,
				MessageBoxImage.Hand);
			return false;
		}
	}

	protected override void OnExit(ExitEventArgs e)
	{
		_systemTrayService?.Dispose();
		_scheduledPrintingService?.Stop();
		if (_singleInstanceMutex != null)
		{
			if (_ownsSingleInstanceMutex)
			{
				try
				{
					_singleInstanceMutex.ReleaseMutex();
				}
				catch (ApplicationException)
				{
				}
			}
			_singleInstanceMutex.Dispose();
			_singleInstanceMutex = null;
		}
		base.OnExit(e);
	}
}
