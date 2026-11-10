using System;
using System.Collections.Generic;
using System.Data;
using TaggJack.Models;

namespace TaggJack.Services;

public static class Gs1SampleDataService
{
	private const int MaxPreviewRows = 10;

	public static Gs1SampleDataResult LoadSampleData(int level, AppConfig config)
	{
		level = Math.Clamp(level, 1, 3);
		SqliteStore sqliteStore = new SqliteStore();
		try
		{
			sqliteStore.EnsureInitialized();
			List<Gs1JobRecord> jobs = sqliteStore.GetJobs(1);
			if (jobs.Count > 0)
			{
				Gs1JobRecord gs1JobRecord = jobs[0];
				List<Gs1SerialRecord> serialsForJob = sqliteStore.GetSerialsForJob(gs1JobRecord.Id);
				DataTable dataTable = Gs1LabelPrintHelper.BuildPrintDataTable(gs1JobRecord, serialsForJob, level);
				if (dataTable.Rows.Count > 0)
				{
					return new Gs1SampleDataResult
					{
						Table = LimitRows(dataTable, 10),
						IsFromLastJob = true,
						JobId = gs1JobRecord.Id,
						Level = level,
						SourceDescription = $"Last job #{gs1JobRecord.Id} · {gs1JobRecord.ItemCode} · Level {level} · {dataTable.Rows.Count} row{((dataTable.Rows.Count == 1) ? "" : "s")}"
					};
				}
			}
		}
		catch
		{
		}
		DataTable table = BuildSyntheticSample(level, config);
		return new Gs1SampleDataResult
		{
			Table = table,
			IsFromLastJob = false,
			JobId = null,
			Level = level,
			SourceDescription = $"Synthetic sample · Level {level} · no jobs in history"
		};
	}

	private unsafe static DataTable BuildSyntheticSample(int level, AppConfig config)
	{
		Gs1Config gs = config.Gs1;
		string text = (string.IsNullOrWhiteSpace(gs.DefaultBatch) ? "BATCH-001" : gs.DefaultBatch);
		string text2 = (gs.UseTodayForProduction ? DateTime.Today.ToString("yyyy-MM-dd") : (string.IsNullOrWhiteSpace(gs.DefaultProductionDate) ? DateTime.Today.ToString("yyyy-MM-dd") : gs.DefaultProductionDate));
		string text3 = (string.IsNullOrWhiteSpace(gs.DefaultExpiryDate) ? DateTime.Today.AddYears(1).ToString("yyyy-MM-dd") : gs.DefaultExpiryDate);
		object obj = level switch
		{
			1 => ("12345678901234", "", ""), 
			2 => ("CARTON001234", "12345678901234", ""), 
			_ => ("PALLET001", "CARTON001234", "123456789012345678"), 
		};
		string item = ((ValueTuple<string, string, string>*)(&obj))->Item1;
		string item2 = ((ValueTuple<string, string, string>*)(&obj))->Item2;
		string item3 = ((ValueTuple<string, string, string>*)(&obj))->Item3;
		DataTable dataTable = Gs1LabelPrintHelper.CreateEmptyPrintSchema();
		dataTable.Rows.Add("ITEM-001", "Sample Product", "06123456789012", item, item2, item3, text, text2, text3, "24", level.ToString());
		return dataTable;
	}

	private static DataTable LimitRows(DataTable source, int maxRows)
	{
		if (source.Rows.Count <= maxRows)
		{
			return source.Copy();
		}
		DataTable dataTable = source.Clone();
		for (int i = 0; i < maxRows; i++)
		{
			dataTable.ImportRow(source.Rows[i]);
		}
		return dataTable;
	}
}
