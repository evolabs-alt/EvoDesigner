using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace TaggJack.Services;

public static class WindowIconHelper
{
	public static void TrySetWindowIcon(Window window)
	{
		try
		{
			string text = Path.Combine(AppContext.BaseDirectory, "icon.png");
			if (File.Exists(text))
			{
				window.Icon = BitmapFrame.Create(new Uri(text, UriKind.Absolute));
			}
			else
			{
				window.Icon = BitmapFrame.Create(new Uri("pack://application:,,,/icon.png"));
			}
		}
		catch
		{
		}
	}
}
