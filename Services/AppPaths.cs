using System;
using System.IO;

namespace TaggJack.Services;

public static class AppPaths
{
	private const string AppFolderName = "TaggJack";

	public static string SharedDataDirectory
	{
		get
		{
			string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "TaggJack");
			Directory.CreateDirectory(text);
			return text;
		}
	}

	public static string GetSharedDataPath(string fileName)
	{
		return Path.Combine(SharedDataDirectory, fileName);
	}
}
