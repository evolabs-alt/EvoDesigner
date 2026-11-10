using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using TaggJack.Models;
using TaggJack.UI;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Win32;

namespace TaggJack.Services;

public class SystemTrayService : IDisposable
{
	private TaskbarIcon? _notifyIcon;

	private readonly TemplateManagerService _templateManager;

	private readonly ScheduledPrintingService _scheduledPrinting;

	private readonly bool _useWindowsServiceControl;

	private readonly string _serviceName;

	private Window? _mainWindow;

	private Action? _pendingTrayAction;

	public SystemTrayService(TemplateManagerService templateManager, ScheduledPrintingService scheduledPrinting, bool useWindowsServiceControl = false, string serviceName = "TaggJackPrintService")
	{
		_templateManager = templateManager;
		_scheduledPrinting = scheduledPrinting;
		_useWindowsServiceControl = useWindowsServiceControl;
		_serviceName = serviceName;
		_templateManager.TemplateAdded += OnTemplateAdded;
		_templateManager.TemplateRemoved += OnTemplateRemoved;
		_scheduledPrinting.PrintingStateChanged += OnPrintingStateChanged;
	}

	public void Initialize(Window? mainWindow)
	{
		try
		{
			string text = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] SystemTrayService initialization starting...";
			File.AppendAllText("tray-debug.log", text + Environment.NewLine);
			Console.WriteLine("DEBUG: SystemTrayService initialization starting...");
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
				ToolTipText = "TaggJack - Flow Mode"
			};
			File.AppendAllText("tray-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] TaskbarIcon created without icon first" + Environment.NewLine);
			try
			{
				string text2 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.png");
				File.AppendAllText("tray-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Looking for icon at: {text2}" + Environment.NewLine);
				if (File.Exists(text2))
				{
					_notifyIcon.IconSource = new BitmapImage(new Uri(text2, UriKind.Absolute));
					Console.WriteLine("DEBUG: Icon loaded from path: " + text2);
					File.AppendAllText("tray-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Icon loaded from: {text2}" + Environment.NewLine);
				}
				else
				{
					Console.WriteLine("DEBUG: Icon file not found at: " + text2);
					File.AppendAllText("tray-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Icon file not found at: {text2}" + Environment.NewLine);
					_notifyIcon.IconSource = new BitmapImage(new Uri("pack://application:,,,/icon.png"));
					Console.WriteLine("DEBUG: Using pack URI for icon");
					File.AppendAllText("tray-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Using pack URI for icon" + Environment.NewLine);
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine("DEBUG: Icon loading error: " + ex.Message);
				File.AppendAllText("tray-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Icon error: {ex.Message}" + Environment.NewLine);
			}
			Console.WriteLine("DEBUG: TaskbarIcon created with logo");
			_notifyIcon.ContextMenu = new ContextMenu();
			_notifyIcon.TrayContextMenuOpen += delegate
			{
				RefreshContextMenu();
			};
			_notifyIcon.TrayMouseDoubleClick += OnTrayIconDoubleClick;
			_notifyIcon.Visibility = Visibility.Visible;
			Console.WriteLine("DEBUG: SystemTrayService initialization completed - tray icon should be visible");
			File.AppendAllText("tray-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] SystemTrayService initialization completed - tray icon should be visible" + Environment.NewLine);
		}
		catch (Exception ex2)
		{
			Console.WriteLine("DEBUG: SystemTrayService error: " + ex2.Message);
			File.AppendAllText("tray-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ERROR: {ex2.Message}" + Environment.NewLine);
			throw;
		}
	}

	private ContextMenu CreateContextMenu()
	{
		ContextMenu contextMenu = new ContextMenu();
		MenuItem menuItem = new MenuItem
		{
			Header = "Open Designer",
			FontWeight = FontWeights.Bold
		};
		menuItem.Click += delegate
		{
			ShowMainWindow();
		};
		contextMenu.Items.Add(menuItem);
		contextMenu.Items.Add(new Separator());
		MenuItem menuItem2 = new MenuItem
		{
			Header = "Add Template..."
		};
		menuItem2.Click += delegate
		{
			AddTemplate();
		};
		contextMenu.Items.Add(menuItem2);
		MenuItem menuItem3 = new MenuItem
		{
			Header = "Loaded Templates"
		};
		UpdateTemplateMenuItems(menuItem3);
		contextMenu.Items.Add(menuItem3);
		MenuItem menuItem4 = new MenuItem
		{
			Header = "Remove Template"
		};
		UpdateRemoveTemplateMenuItems(menuItem4);
		contextMenu.Items.Add(menuItem4);
		contextMenu.Items.Add(new Separator());
		MenuItem menuItem5 = new MenuItem
		{
			Header = "Run Now"
		};
		menuItem5.IsEnabled = !_scheduledPrinting.IsPrinting && !_useWindowsServiceControl;
		menuItem5.Click += async delegate
		{
			await RunNowClicked();
		};
		contextMenu.Items.Add(menuItem5);
		bool flag = (_useWindowsServiceControl ? (WindowsServiceControl.GetStatus(_serviceName) == ServiceControllerStatus.Running) : _scheduledPrinting.IsSchedulerActive);
		if (flag)
		{
			MenuItem menuItem6 = new MenuItem
			{
				Header = "Stop Scheduler"
			};
			menuItem6.Click += delegate
			{
				StopScheduler();
			};
			contextMenu.Items.Add(menuItem6);
		}
		else
		{
			MenuItem menuItem7 = new MenuItem
			{
				Header = "Start Scheduler"
			};
			menuItem7.Click += delegate
			{
				StartScheduler();
			};
			contextMenu.Items.Add(menuItem7);
		}
		MenuItem newItem = new MenuItem
		{
			Header = "Status: " + (flag ? "Active" : "Stopped"),
			IsEnabled = false
		};
		contextMenu.Items.Add(newItem);
		MenuItem newItem2 = new MenuItem
		{
			Header = "Printing: " + (_scheduledPrinting.IsPrinting ? "In Progress" : "Idle"),
			IsEnabled = false
		};
		contextMenu.Items.Add(newItem2);
		MenuItem newItem3 = new MenuItem
		{
			Header = "Service control: " + (_useWindowsServiceControl ? "Enabled" : "Disabled"),
			IsEnabled = false
		};
		contextMenu.Items.Add(newItem3);
		if (_useWindowsServiceControl)
		{
			bool flag2 = WindowsServiceControl.IsServiceInstalled(_serviceName);
			MenuItem newItem4 = new MenuItem
			{
				Header = "Service installed: " + (flag2 ? "Yes" : "No"),
				IsEnabled = false
			};
			contextMenu.Items.Add(newItem4);
			string text = WindowsServiceControl.GetStatus(_serviceName)?.ToString() ?? "Not Installed";
			MenuItem newItem5 = new MenuItem
			{
				Header = "Service state: " + text,
				IsEnabled = false
			};
			contextMenu.Items.Add(newItem5);
		}
		contextMenu.Items.Add(new Separator());
		MenuItem menuItem8 = new MenuItem
		{
			Header = "Install Tray Autostart Task"
		};
		menuItem8.Click += delegate
		{
			InstallTrayAutostartTask();
		};
		contextMenu.Items.Add(menuItem8);
		MenuItem menuItem9 = new MenuItem
		{
			Header = "Remove Tray Autostart Task"
		};
		menuItem9.Click += delegate
		{
			RemoveTrayAutostartTask();
		};
		contextMenu.Items.Add(menuItem9);
		contextMenu.Items.Add(new Separator());
		MenuItem menuItem10 = new MenuItem
		{
			Header = "View Logs..."
		};
		menuItem10.Click += delegate
		{
			ShowLogs();
		};
		contextMenu.Items.Add(menuItem10);
		contextMenu.Items.Add(new Separator());
		MenuItem menuItem11 = new MenuItem
		{
			Header = "Exit"
		};
		menuItem11.Click += delegate
		{
			QueueTrayAction(ExitApplication);
		};
		contextMenu.Items.Add(menuItem11);
		contextMenu.Closed += OnTrayContextMenuClosed;
		return contextMenu;
	}

	private void UpdateTemplateMenuItems(MenuItem loadedTemplatesMenuItem)
	{
		loadedTemplatesMenuItem.Items.Clear();
		List<LoadedTemplate> loadedTemplates = _templateManager.GetLoadedTemplates();
		if (!loadedTemplates.Any())
		{
			MenuItem newItem = new MenuItem
			{
				Header = "No templates loaded",
				IsEnabled = false
			};
			loadedTemplatesMenuItem.Items.Add(newItem);
			return;
		}
		foreach (LoadedTemplate item in loadedTemplates)
		{
			MenuItem newItem2 = new MenuItem
			{
				Header = item.Name + " (" + item.Path + ")",
				IsEnabled = false
			};
			loadedTemplatesMenuItem.Items.Add(newItem2);
		}
	}

	private void UpdateRemoveTemplateMenuItems(MenuItem removeTemplateMenuItem)
	{
		removeTemplateMenuItem.Items.Clear();
		List<LoadedTemplate> loadedTemplates = _templateManager.GetLoadedTemplates();
		if (!loadedTemplates.Any())
		{
			MenuItem newItem = new MenuItem
			{
				Header = "No templates to remove",
				IsEnabled = false
			};
			removeTemplateMenuItem.Items.Add(newItem);
			return;
		}
		foreach (LoadedTemplate template in loadedTemplates)
		{
			MenuItem menuItem = new MenuItem
			{
				Header = template.Name
			};
			menuItem.Click += delegate
			{
				RemoveTemplate(template.Path);
			};
			removeTemplateMenuItem.Items.Add(menuItem);
		}
	}

	private void QueueTrayAction(Action action)
	{
		_pendingTrayAction = action;
	}

	private void OnTrayContextMenuClosed(object? sender, EventArgs e)
	{
		Action? pendingTrayAction = _pendingTrayAction;
		_pendingTrayAction = null;
		pendingTrayAction?.Invoke();
	}

	private static Window CreateDialogHelperWindow()
	{
		Window window = new Window();
		window.Width = 1.0;
		window.Height = 1.0;
		window.Left = -20000.0;
		window.Top = -20000.0;
		window.WindowStyle = WindowStyle.None;
		window.ShowInTaskbar = false;
		window.ShowActivated = true;
		window.Topmost = true;
		window.Show();
		return window;
	}

	private static bool? ShowOpenFileDialog(OpenFileDialog dialog)
	{
		Window window = CreateDialogHelperWindow();
		try
		{
			return dialog.ShowDialog(window);
		}
		finally
		{
			window.Close();
		}
	}

	private static MessageBoxResult ShowTrayMessageBox(string message, string title, MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.None)
	{
		return DialogHelper.ShowMessageBox(message, title, buttons, icon);
	}

	private void AddTemplate()
	{
		QueueTrayAction(ShowAddTemplateDialog);
	}

	private void ShowAddTemplateDialog()
	{
		OpenFileDialog openFileDialog = new OpenFileDialog
		{
			Filter = "XML Templates (*.xml)|*.xml|All files (*.*)|*.*",
			Title = "Select Template File"
		};
		if (ShowOpenFileDialog(openFileDialog) == true)
		{
			try
			{
				_templateManager.AddTemplate(openFileDialog.FileName);
				ShowTrayMessageBox("Template '" + Path.GetFileNameWithoutExtension(openFileDialog.FileName) + "' added successfully.", "Template Added", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			}
			catch (Exception ex)
			{
				ShowTrayMessageBox("Failed to add template: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Hand);
			}
		}
	}

	private void RemoveTemplate(string filePath)
	{
		QueueTrayAction(delegate
		{
			ShowRemoveTemplateDialog(filePath);
		});
	}

	private void ShowRemoveTemplateDialog(string filePath)
	{
		try
		{
			string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
			if (ShowTrayMessageBox("Are you sure you want to remove template '" + fileNameWithoutExtension + "'?", "Confirm Removal", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
			{
				_templateManager.RemoveTemplate(filePath);
				ShowTrayMessageBox("Template '" + fileNameWithoutExtension + "' removed successfully.", "Template Removed", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			}
		}
		catch (Exception ex)
		{
			ShowTrayMessageBox("Failed to remove template: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private void OnTrayIconDoubleClick(object sender, RoutedEventArgs e)
	{
		ShowMainWindow();
	}

	private void ShowMainWindow()
	{
		try
		{
			File.AppendAllText("tray-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ShowMainWindow called{Environment.NewLine}");
			Application current = Application.Current;
			Window window = null;
			File.AppendAllText("tray-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Total windows in app: {(current?.Windows?.Count).GetValueOrDefault()}{Environment.NewLine}");
			if (current?.Windows != null && current.Windows.Count > 0)
			{
				int num = 0;
				foreach (Window window4 in current.Windows)
				{
					File.AppendAllText("tray-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Window {num}: Type={window4.GetType().Name}, Title={window4.Title ?? "(no title)"}{Environment.NewLine}");
					num++;
				}
				foreach (Window window5 in current.Windows)
				{
					if (window5.GetType().Name == "MainWindow")
					{
						try
						{
							bool isVisible = window5.IsVisible;
							File.AppendAllText("tray-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Found MainWindow - IsVisible: {isVisible}{Environment.NewLine}");
							window = window5;
						}
						catch (InvalidOperationException ex)
						{
							File.AppendAllText("tray-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] MainWindow was closed: {ex.Message}{Environment.NewLine}");
							continue;
						}
						break;
					}
				}
			}
			if (window != null)
			{
				File.AppendAllText("tray-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Using existing window{Environment.NewLine}");
				_mainWindow = window;
				if (_mainWindow.IsVisible)
				{
					File.AppendAllText("tray-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Window already visible, bringing to front{Environment.NewLine}");
					if (_mainWindow.WindowState == WindowState.Minimized)
					{
						_mainWindow.WindowState = WindowState.Normal;
					}
					_mainWindow.Activate();
					_mainWindow.Focus();
				}
				else
				{
					File.AppendAllText("tray-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Window hidden, showing it{Environment.NewLine}");
					try
					{
						_mainWindow.Show();
						_mainWindow.WindowState = WindowState.Normal;
						_mainWindow.Activate();
					}
					catch (InvalidOperationException exShow)
					{
						File.AppendAllText("tray-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Show failed (window closed): {exShow.Message}{Environment.NewLine}");
						_mainWindow = null;
						CreateNewMainWindow();
					}
				}
				return;
			}
			File.AppendAllText("tray-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] No existing window found in app.Windows{Environment.NewLine}");
			if (_mainWindow != null)
			{
				try
				{
					_ = _mainWindow.IsVisible;
					File.AppendAllText("tray-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Stored _mainWindow reference is still valid{Environment.NewLine}");
					if (_mainWindow.IsVisible)
					{
						if (_mainWindow.WindowState == WindowState.Minimized)
						{
							_mainWindow.WindowState = WindowState.Normal;
						}
						_mainWindow.Activate();
						_mainWindow.Focus();
					}
					else
					{
						try
						{
							_mainWindow.Show();
							_mainWindow.WindowState = WindowState.Normal;
							_mainWindow.Activate();
						}
						catch (InvalidOperationException)
						{
							_mainWindow = null;
							CreateNewMainWindow();
							return;
						}
					}
					return;
				}
				catch (InvalidOperationException ex2)
				{
					File.AppendAllText("tray-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Stored _mainWindow was closed: {ex2.Message}{Environment.NewLine}");
					_mainWindow = null;
				}
			}
			File.AppendAllText("tray-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Creating new MainWindow{Environment.NewLine}");
			CreateNewMainWindow();
		}
		catch (Exception ex3)
		{
			File.AppendAllText("tray-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ERROR in ShowMainWindow: {ex3.Message}{Environment.NewLine}{ex3.StackTrace}{Environment.NewLine}");
			MessageBox.Show("An error occurred while trying to show the designer window: " + ex3.Message, "TaggJack - Error", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private void CreateNewMainWindow()
	{
		try
		{
			File.AppendAllText("tray-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] CreateNewMainWindow() called{Environment.NewLine}");
			File.AppendAllText("tray-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Stack trace:{Environment.NewLine}{Environment.StackTrace}{Environment.NewLine}");
			_mainWindow = new MainWindow();
			if (Application.Current != null)
			{
				Application.Current.MainWindow = _mainWindow;
			}
			_mainWindow.Show();
			_mainWindow.WindowState = WindowState.Normal;
			_mainWindow.Activate();
			File.AppendAllText("tray-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Created new MainWindow instance{Environment.NewLine}");
		}
		catch (Exception ex)
		{
			File.AppendAllText("tray-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ERROR creating new MainWindow: {ex.Message}{Environment.NewLine}{ex.StackTrace}{Environment.NewLine}");
			MessageBox.Show("Failed to create designer window: " + ex.Message, "TaggJack - Error", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	public void HideToTray()
	{
		_mainWindow?.Hide();
	}

	public void SetMainWindow(Window mainWindow)
	{
		_mainWindow = mainWindow;
	}

	private void ExitApplication()
	{
		if (!_scheduledPrinting.IsSchedulerActive || ShowTrayMessageBox("The scheduled printing service is currently active. Exiting the application will stop all scheduled print jobs.\n\nAre you sure you want to exit?", "TaggJack - Confirm Exit", MessageBoxButton.YesNo, MessageBoxImage.Exclamation) == MessageBoxResult.Yes)
		{
			_scheduledPrinting.Stop();
			MainWindow mainWindow = (Application.Current.MainWindow as MainWindow) ?? Application.Current.Windows.OfType<MainWindow>().FirstOrDefault() ?? (_mainWindow as MainWindow);
			if (mainWindow != null)
			{
				mainWindow.ExplicitExit();
			}
			else
			{
				Environment.Exit(0);
			}
		}
	}

	private async Task RunNowClicked()
	{
		try
		{
			RunNowDebugLog.WriteCycle("Run Now clicked");
			List<LoadedTemplate> enabledTemplates = _templateManager.GetEnabledTemplates();
			RunNowDebugLog.WriteCycle($"Found {enabledTemplates.Count} enabled templates");
			if (enabledTemplates.Count == 0)
			{
				_notifyIcon?.ShowBalloonTip("TaggJack - Run Now", "No templates are loaded or enabled for printing. Please add templates using 'Add Template' from the tray menu.", BalloonIcon.Info);
				RunNowDebugLog.WriteCycle("No templates available - showing balloon tip");
				return;
			}
			_notifyIcon?.ShowBalloonTip("TaggJack - Run Now", $"Starting print cycle for {enabledTemplates.Count} template(s)...", BalloonIcon.Info);
			RunNowDebugLog.WriteCycle($"Starting print cycle for {enabledTemplates.Count} templates");
			await _scheduledPrinting.RunNow();
			RunNowDebugLog.WriteCycle("Print cycle completed");
			_notifyIcon?.ShowBalloonTip("TaggJack - Run Now", "Print cycle completed successfully.", BalloonIcon.Info);
		}
		catch (Exception ex)
		{
			RunNowDebugLog.WriteCycle("ERROR: " + ex.Message);
			_notifyIcon?.ShowBalloonTip("TaggJack - Run Now", "Error during print cycle: " + ex.Message, BalloonIcon.Error);
		}
	}

	private void StartScheduler()
	{
		try
		{
			if (_useWindowsServiceControl)
			{
				WindowsServiceControl.StartService(_serviceName, TimeSpan.FromSeconds(10L));
			}
			else
			{
				_scheduledPrinting.Start();
			}
			_notifyIcon?.ShowBalloonTip("TaggJack - Scheduler", "Scheduler started successfully.", BalloonIcon.Info);
			RefreshContextMenu();
		}
		catch (Exception ex)
		{
			MessageBox.Show("Failed to start scheduler: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private void StopScheduler()
	{
		try
		{
			if (_useWindowsServiceControl)
			{
				WindowsServiceControl.StopService(_serviceName, TimeSpan.FromSeconds(10L));
			}
			else
			{
				_scheduledPrinting.Stop();
			}
			_notifyIcon?.ShowBalloonTip("TaggJack - Scheduler", "Scheduler stopped successfully.", BalloonIcon.Info);
			RefreshContextMenu();
		}
		catch (Exception ex)
		{
			MessageBox.Show("Failed to stop scheduler: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private void ShowLogs()
	{
		QueueTrayAction(ShowLogsWindow);
	}

	private void ShowLogsWindow()
	{
		try
		{
			new LogsWindow(_scheduledPrinting).Show();
		}
		catch (Exception ex)
		{
			ShowTrayMessageBox("Failed to open logs window: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private void InstallTrayAutostartTask()
	{
		try
		{
			string text = Environment.ProcessPath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TaggJack.exe");
			if (!File.Exists(text))
			{
				throw new FileNotFoundException("Could not locate TaggJack executable for autostart task.", text);
			}
			string value = "TaggJack Tray Autostart";
			string value2 = "\"" + text + "\" --tray --minimized";
			string arguments = $"/Create /TN \"{value}\" /SC ONLOGON /TR \"{value2}\" /F /RL LIMITED /DELAY 0000:05";
			RunProcessChecked("schtasks.exe", arguments);
			_notifyIcon?.ShowBalloonTip("TaggJack - Tray Autostart", "Tray autostart task installed successfully.", BalloonIcon.Info);
		}
		catch (Exception ex)
		{
			MessageBox.Show("Failed to install tray autostart task: " + ex.Message, "Autostart Error", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private void RemoveTrayAutostartTask()
	{
		try
		{
			string text = "TaggJack Tray Autostart";
			string arguments = "/Delete /TN \"" + text + "\" /F";
			RunProcessChecked("schtasks.exe", arguments);
			_notifyIcon?.ShowBalloonTip("TaggJack - Tray Autostart", "Tray autostart task removed successfully.", BalloonIcon.Info);
		}
		catch (Exception ex)
		{
			MessageBox.Show("Failed to remove tray autostart task: " + ex.Message, "Autostart Error", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private static void RunProcessChecked(string fileName, string arguments)
	{
		using Process process = Process.Start(new ProcessStartInfo
		{
			FileName = fileName,
			Arguments = arguments,
			UseShellExecute = false,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			CreateNoWindow = true
		}) ?? throw new InvalidOperationException("Failed to start process: " + fileName);
		string text = process.StandardOutput.ReadToEnd();
		string text2 = process.StandardError.ReadToEnd();
		process.WaitForExit();
		if (process.ExitCode != 0)
		{
			string value = (string.IsNullOrWhiteSpace(text2) ? text : text2);
			throw new InvalidOperationException($"{fileName} failed ({process.ExitCode}): {value}".Trim());
		}
	}

	private void OnTemplateAdded(object? sender, TemplateManagerEventArgs e)
	{
		RefreshContextMenu();
	}

	private void OnTemplateRemoved(object? sender, TemplateManagerEventArgs e)
	{
		RefreshContextMenu();
	}

	private void OnPrintingStateChanged(object? sender, bool isPrinting)
	{
		RefreshContextMenu();
	}

	private void RefreshContextMenu()
	{
		try
		{
			Application current = Application.Current;
			if (current?.Dispatcher == null)
			{
				return;
			}
			if (!current.Dispatcher.CheckAccess())
			{
				current.Dispatcher.InvokeAsync(RefreshContextMenu);
				return;
			}
			if (_notifyIcon == null)
			{
				return;
			}
			_notifyIcon.ContextMenu = CreateContextMenu();
		}
		catch (InvalidOperationException)
		{
			// Ignore if the tray icon or a window is already closed/disposed (e.g. during shutdown or background Linx completion).
		}
	}

	public void Dispose()
	{
		_templateManager.TemplateAdded -= OnTemplateAdded;
		_templateManager.TemplateRemoved -= OnTemplateRemoved;
		_scheduledPrinting.PrintingStateChanged -= OnPrintingStateChanged;
		_notifyIcon?.Dispose();
		_notifyIcon = null;
	}
}
