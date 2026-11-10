using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using TaggJack.Models;
using TaggJack.Services;
using TaggJack.Services.License;

namespace TaggJack.UI;

public partial class LicenseActivationWindow : Window
{
	private readonly LicenseService _licenseService = new();
	private bool _busy;

	public bool IsActivated { get; private set; }

	public LicenseActivationWindow()
	{
		InitializeComponent();
		Loaded += async (_, _) => await RefreshStatusAsync();
	}

	private async Task RefreshStatusAsync()
	{
		try
		{
			LicenseEvaluation status = _licenseService.GetStatus();
			HardwareKeyTextBox.Text = status.HardwareKey ?? _licenseService.GetHardwareKey();
			ApplyStatus(status);
			if (status.Allowed)
			{
				IsActivated = true;
				base.DialogResult = true;
				Close();
			}
		}
		catch (Exception ex)
		{
			ShowStatus(ex.Message, error: true);
		}
		await Task.CompletedTask;
	}

	private void ApplyStatus(LicenseEvaluation status)
	{
		string label = status.Status.ToString().ToUpperInvariant();
		string msg = status.Message ?? label;
		if (status.Allowed)
		{
			ShowStatus($"{label}: {msg}", error: false);
		}
		else if (status.Status == LicenseStatus.Pending)
		{
			ShowStatus("PENDING: Waiting for Evolabs approval at admin.evolabs.co", warning: true);
		}
		else
		{
			ShowStatus($"{label}: {msg}", error: status.Status == LicenseStatus.Invalid);
		}
		InfoText.Text = $"Product: {LicenseConstants.ProductCode}  ·  Portal: {LicenseConstants.PortalBaseUrl}";
	}

	private void ShowStatus(string text, bool error = false, bool warning = false)
	{
		StatusPanel.Visibility = Visibility.Visible;
		StatusText.Text = text;
		if (error)
		{
			StatusPanel.Background = new SolidColorBrush(Color.FromRgb(248, 215, 218));
			StatusPanel.BorderBrush = new SolidColorBrush(Color.FromRgb(220, 53, 69));
			StatusText.Foreground = new SolidColorBrush(Color.FromRgb(114, 28, 36));
		}
		else if (warning)
		{
			StatusPanel.Background = new SolidColorBrush(Color.FromRgb(255, 243, 205));
			StatusPanel.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 193, 7));
			StatusText.Foreground = new SolidColorBrush(Color.FromRgb(133, 100, 4));
		}
		else
		{
			StatusPanel.Background = new SolidColorBrush(Color.FromRgb(212, 237, 218));
			StatusPanel.BorderBrush = new SolidColorBrush(Color.FromRgb(40, 167, 69));
			StatusText.Foreground = new SolidColorBrush(Color.FromRgb(21, 87, 36));
		}
	}

	private void CopyKey_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			Clipboard.SetText(HardwareKeyTextBox.Text ?? "");
			InfoText.Text = "Hardware key copied to clipboard.";
		}
		catch
		{
			InfoText.Text = "Copy failed — select the key manually.";
		}
	}

	private async void Request_Click(object sender, RoutedEventArgs e)
	{
		if (_busy) return;
		_busy = true;
		SetBusy(true);
		try
		{
			Dictionary<string, object?> res = await _licenseService.RequestActivationAsync(
				CustomerHintTextBox.Text?.Trim(),
				ContactTextBox.Text?.Trim());
			string status = res.TryGetValue("status", out object? s) ? s?.ToString() ?? "" : "";
			if (string.Equals(status, "PENDING", StringComparison.OrdinalIgnoreCase))
			{
				ShowStatus("Activation request sent. Waiting for Evolabs approval.", warning: true);
			}
			else
			{
				string msg = res.TryGetValue("message", out object? m) ? m?.ToString() ?? status : status;
				ShowStatus(msg, error: true);
			}
			await RefreshStatusAsync();
		}
		catch (Exception ex)
		{
			ShowStatus(ex.Message, error: true);
		}
		finally
		{
			_busy = false;
			SetBusy(false);
		}
	}

	private async void Check_Click(object sender, RoutedEventArgs e)
	{
		if (_busy) return;
		_busy = true;
		SetBusy(true);
		try
		{
			Dictionary<string, object?> res = await _licenseService.CheckWithPortalAsync();
			string status = res.TryGetValue("status", out object? s) ? s?.ToString() ?? "" : "";
			if (string.Equals(status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
			{
				ShowStatus("License active.", error: false);
				IsActivated = true;
				base.DialogResult = true;
				Close();
				return;
			}
			if (string.Equals(status, "UNREACHABLE", StringComparison.OrdinalIgnoreCase))
			{
				ShowStatus("Portal unreachable. If previously licensed, offline grace may apply.", warning: true);
			}
			else
			{
				string reason = res.TryGetValue("reason", out object? r) ? r?.ToString() : null;
				string message = res.TryGetValue("message", out object? m) ? m?.ToString() : null;
				ShowStatus(reason ?? message ?? status, error: true);
			}
			await RefreshStatusAsync();
		}
		catch (Exception ex)
		{
			ShowStatus(ex.Message, error: true);
		}
		finally
		{
			_busy = false;
			SetBusy(false);
		}
	}

	private void SetBusy(bool busy)
	{
		RequestButton.IsEnabled = !busy;
		CheckButton.IsEnabled = !busy;
	}

	private void ExitButton_Click(object sender, RoutedEventArgs e)
	{
		if (IsActivated ||
		    MessageBox.Show(
			    "Exit without activating?\n\nYou won't be able to use TaggJack without a valid license.",
			    "Confirm Exit",
			    MessageBoxButton.YesNo,
			    MessageBoxImage.Exclamation) != MessageBoxResult.No)
		{
			base.DialogResult = IsActivated;
			Close();
		}
	}
}
