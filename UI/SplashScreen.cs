using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Threading;

namespace TaggJack.UI;

public partial class SplashScreen : Window {
	private DispatcherTimer? _timer;

	private int _minimumDisplayTime = 2000;

	private bool _isClosed;

	private bool _closeRequested;

	public SplashScreen()
	{
		InitializeComponent();
		base.Closed += delegate
		{
			_isClosed = true;
		};
	}

	public void UpdateStatus(string status)
	{
		if (_isClosed)
		{
			return;
		}
		base.Dispatcher.Invoke(delegate
		{
			if (!_isClosed && StatusText != null)
			{
				StatusText.Text = status;
			}
		});
	}

	public void ShowSplash()
	{
		if (_isClosed)
		{
			return;
		}
		Show();
		_timer = new DispatcherTimer
		{
			Interval = TimeSpan.FromMilliseconds(_minimumDisplayTime)
		};
		_timer.Tick += OnMinimumDisplayElapsed;
		_timer.Start();
	}

	private void OnMinimumDisplayElapsed(object? sender, EventArgs e)
	{
		_timer?.Stop();
		_timer = null;
		if (_closeRequested && !_isClosed)
		{
			Close();
		}
	}

	public void CloseSplash()
	{
		if (_isClosed)
		{
			return;
		}
		_closeRequested = true;
		void DoClose()
		{
			if (!_isClosed)
			{
				Close();
			}
		}
		if (_timer != null && _timer.IsEnabled)
		{
			// Wait for minimum display time; OnMinimumDisplayElapsed will close.
			return;
		}
		if (base.Dispatcher.CheckAccess())
		{
			DoClose();
		}
		else
		{
			base.Dispatcher.Invoke(DoClose);
		}
	}
}
