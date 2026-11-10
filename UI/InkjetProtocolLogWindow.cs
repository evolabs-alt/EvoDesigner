using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using TaggJack.Services;

namespace TaggJack.UI;

public partial class InkjetProtocolLogWindow : Window {


	public InkjetProtocolLogWindow()
	{
		InitializeComponent();
		WindowIconHelper.TrySetWindowIcon(this);
	}

	public InkjetProtocolLogWindow(string title, string content)
		: this()
	{
		SetVendorTitle(title);
		SetContent(content);
	}

	public void SetVendorTitle(string vendorDisplayName)
	{
		string text = (base.Title = (string.IsNullOrWhiteSpace(vendorDisplayName) ? "Protocol Log" : (vendorDisplayName + " Protocol Log")));
		if (HeaderText != null)
		{
			HeaderText.Text = text;
		}
	}

	public void Append(string text)
	{
		if (!string.IsNullOrEmpty(text))
		{
			if (LogTextBox.Text.Length > 0 && !LogTextBox.Text.EndsWith("\n") && !text.StartsWith("\n"))
			{
				LogTextBox.AppendText(Environment.NewLine);
			}
			LogTextBox.AppendText(text);
			if (!text.EndsWith("\n") && !text.EndsWith("\r\n"))
			{
				LogTextBox.AppendText(Environment.NewLine);
			}
			LogTextBox.CaretIndex = LogTextBox.Text.Length;
			LogTextBox.ScrollToEnd();
		}
	}

	public void SetContent(string text)
	{
		LogTextBox.Text = text ?? string.Empty;
		LogTextBox.CaretIndex = LogTextBox.Text.Length;
		LogTextBox.ScrollToEnd();
	}

	private void Copy_Click(object sender, RoutedEventArgs e)
	{
		if (!string.IsNullOrEmpty(LogTextBox.Text))
		{
			Clipboard.SetText(LogTextBox.Text);
		}
	}

	private void Clear_Click(object sender, RoutedEventArgs e)
	{
		LogTextBox.Clear();
	}

	private void Close_Click(object sender, RoutedEventArgs e)
	{
		Close();
	}
}
