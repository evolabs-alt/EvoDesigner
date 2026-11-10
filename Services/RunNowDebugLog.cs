using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;

namespace TaggJack.Services;

public static class RunNowDebugLog
{
	private static readonly ConcurrentDictionary<string, object> FileLocks = new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase);

	public static void WriteLine(string? printerName, string message)
	{
		Append(printerName, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
	}

	public static void Append(string? printerName, string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return;
		}
		try
		{
			string logPath = GetLogPath(printerName);
			lock (FileLocks.GetOrAdd(logPath, (string _) => new object()))
			{
				File.AppendAllText(logPath, text);
			}
		}
		catch
		{
		}
	}

	public static void WriteCycle(string message)
	{
		WriteLine("cycle", message);
	}

	public static string GetLogPath(string? printerName)
	{
		return Path.Combine(Directory.GetCurrentDirectory(), "run-now-" + SanitizePrinterName(printerName) + ".txt");
	}

	public static string SanitizePrinterName(string? printerName)
	{
		if (string.IsNullOrWhiteSpace(printerName))
		{
			return "unknown";
		}
		char[] invalidFileNameChars = Path.GetInvalidFileNameChars();
		StringBuilder stringBuilder = new StringBuilder(printerName.Length);
		string text = printerName.Trim();
		foreach (char c in text)
		{
			if (Array.IndexOf(invalidFileNameChars, c) >= 0 || c == ':' || c == '/' || c == '\\' || c == ' ')
			{
				stringBuilder.Append('_');
			}
			else
			{
				stringBuilder.Append(c);
			}
		}
		string text2 = stringBuilder.ToString().Trim('_', '.');
		while (text2.Contains("__", StringComparison.Ordinal))
		{
			text2 = text2.Replace("__", "_", StringComparison.Ordinal);
		}
		if (!string.IsNullOrEmpty(text2))
		{
			return text2;
		}
		return "unknown";
	}
}
