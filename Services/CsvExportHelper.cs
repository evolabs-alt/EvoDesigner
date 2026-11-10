using System.Collections.Generic;
using System.IO;
using System.Text;
using TaggJack.Models;

namespace TaggJack.Services;

public static class CsvExportHelper
{
	public static void ExportItems(IEnumerable<ItemRecord> items, string filePath)
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine("ItemCode,Gtin,Description,Level1,Level2,Level3");
		foreach (ItemRecord item in items)
		{
			stringBuilder.AppendLine(string.Join(",", new object[]
			{
				Escape(item.ItemCode),
				Escape(item.Gtin),
				Escape(item.Description),
				item.Level1,
				item.Level2,
				item.Level3
			}));
		}
		File.WriteAllText(filePath, stringBuilder.ToString(), Encoding.UTF8);
	}

	private static string Escape(string value)
	{
		if (value == null)
		{
			value = "";
		}
		if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
		{
			return "\"" + value.Replace("\"", "\"\"") + "\"";
		}
		return value;
	}
}
