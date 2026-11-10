using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using TaggJack.Models;
using TaggJack.Services;
using TaggJack.Services.License;

namespace TaggJack.UI;

public partial class AboutWindow : Window
{
	public AboutWindow()
	{
		InitializeComponent();
		LoadLicenseInfo();
	}

	private void LoadLicenseInfo()
	{
		try
		{
			LicenseEvaluation eval = PortalLicenseService.Instance.GetStatus();
			LicenseTypeText.Text = eval.Status.ToString().ToUpperInvariant();
			LicensedToText.Text = string.IsNullOrWhiteSpace(eval.CustomerName) ? "-" : eval.CustomerName;
			ExpiryText.Text = string.IsNullOrWhiteSpace(eval.LicenseExpiresAt) ? "-" : eval.LicenseExpiresAt;
			LicenseKeyText.Text = string.IsNullOrWhiteSpace(eval.HardwareKey)
				? "-"
				: HardwareIdService.FormatMachineId(eval.HardwareKey);

			if (eval.Allowed)
			{
				LicenseTypeText.Foreground = new SolidColorBrush(Color.FromRgb(40, 167, 69));
			}
			else
			{
				LicenseTypeText.Foreground = new SolidColorBrush(Color.FromRgb(220, 53, 69));
			}

			if (eval.Status is LicenseStatus.Expiring or LicenseStatus.Grace)
			{
				ExpiryText.Foreground = new SolidColorBrush(Color.FromRgb(220, 53, 69));
			}
		}
		catch
		{
			LicenseTypeText.Text = "Unavailable";
			LicensedToText.Text = "-";
			ExpiryText.Text = "-";
			LicenseKeyText.Text = "-";
		}
	}

	private void OK_Click(object sender, RoutedEventArgs e) => Close();

	private void Website_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			Process.Start(new ProcessStartInfo("https://www.evolabs.co") { UseShellExecute = true });
		}
		catch
		{
		}
	}

	private void Email_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			Process.Start(new ProcessStartInfo("mailto:support@evolabs.co") { UseShellExecute = true });
		}
		catch
		{
		}
	}
}
