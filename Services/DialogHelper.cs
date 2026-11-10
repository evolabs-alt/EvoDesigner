using System.Windows;

namespace TaggJack.Services;

public static class DialogHelper
{
	public static Window CreateHelperWindow()
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

	public static MessageBoxResult ShowMessageBox(string message, string title, MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.None, Window? owner = null)
	{
		if (owner != null && owner.IsVisible)
		{
			return MessageBox.Show(owner, message, title, buttons, icon);
		}
		Window window = CreateHelperWindow();
		try
		{
			return MessageBox.Show(window, message, title, buttons, icon);
		}
		finally
		{
			window.Close();
		}
	}
}
