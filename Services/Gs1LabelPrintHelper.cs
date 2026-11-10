using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using TaggJack.Models;

namespace TaggJack.Services;

public static class Gs1LabelPrintHelper
{
	public static LabelTemplate LoadTemplate(string path)
	{
		if (!File.Exists(path))
		{
			throw new FileNotFoundException("Template not found: " + path);
		}
		XmlSerializer xmlSerializer = new XmlSerializer(typeof(LabelTemplate));
		using StreamReader textReader = new StreamReader(path);
		return (LabelTemplate)xmlSerializer.Deserialize(textReader);
	}

	public static DataTable CreateEmptyPrintSchema()
	{
		return new DataTable
		{
			Columns = 
			{
				{
					"item_code",
					typeof(string)
				},
				{
					"description",
					typeof(string)
				},
				{
					"gtin",
					typeof(string)
				},
				{
					"serial",
					typeof(string)
				},
				{
					"parent_serial",
					typeof(string)
				},
				{
					"sscc",
					typeof(string)
				},
				{
					"batch",
					typeof(string)
				},
				{
					"production",
					typeof(string)
				},
				{
					"expiry",
					typeof(string)
				},
				{
					"quantity",
					typeof(string)
				},
				{
					"level",
					typeof(string)
				}
			}
		};
	}

	public static DataTable BuildPrintDataTable(Gs1JobRecord job, List<Gs1SerialRecord> serials, int level)
	{
		DataTable dataTable = CreateEmptyPrintSchema();
		foreach (Gs1SerialRecord item in serials.Where((Gs1SerialRecord x) => x.Level == level))
		{
			dataTable.Rows.Add(job.ItemCode, job.Description, job.Gtin, item.Serial, item.ParentSerial, item.Sscc, job.Batch, job.Production, job.Expiry, job.Quantity.ToString(), level.ToString());
		}
		return dataTable;
	}

	public static void PrintJobLevel(Gs1JobRecord job, List<Gs1SerialRecord> serials, int level, string? templatePathOverride = null, string? printerOverride = null)
	{
		string text = templatePathOverride ?? GetTemplatePath(job, level);
		string text2 = printerOverride ?? GetPrinter(job, level);
		if (!string.IsNullOrWhiteSpace(text) && !string.IsNullOrWhiteSpace(text2) && serials.Where((Gs1SerialRecord s) => s.Level == level).ToList().Count != 0)
		{
			LabelTemplate template = LoadTemplate(text);
			DataTable dataTable = BuildPrintDataTable(job, serials, level);
			new LabelPrintService().PrintAllRecords(template, text2, dataTable);
		}
	}

	public static void PrintAllLevels(Gs1JobRecord job, List<Gs1SerialRecord> serials)
	{
		for (int i = 1; i <= 3; i++)
		{
			PrintJobLevel(job, serials, i);
		}
	}

	private static string GetTemplatePath(Gs1JobRecord job, int level)
	{
		return level switch
		{
			1 => job.Level1TemplatePath, 
			2 => job.Level2TemplatePath, 
			3 => job.Level3TemplatePath, 
			_ => "", 
		};
	}

	private static string GetPrinter(Gs1JobRecord job, int level)
	{
		return level switch
		{
			1 => job.Level1Printer, 
			2 => job.Level2Printer, 
			3 => job.Level3Printer, 
			_ => "", 
		};
	}
}
