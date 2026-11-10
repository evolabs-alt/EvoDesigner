using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.Odbc;
using TaggJack.Models;
using Microsoft.Data.SqlClient;
using MySql.Data.MySqlClient;

namespace TaggJack.Services;

public class ErpQueryService
{
	public static List<string> GetDatabaseTypes()
	{
		return new List<string> { "MySQL", "SQL Server", "SAP HANA" };
	}

	public void TestConnection(ErpImportConfig config)
	{
		using DbConnection dbConnection = CreateConnection(config);
		dbConnection.Open();
	}

	public List<string> GetTables(ErpImportConfig config)
	{
		using DbConnection dbConnection = CreateConnection(config);
		dbConnection.Open();
		List<string> list = new List<string>();
		if (config.DatabaseType == "MySQL")
		{
			using DbCommand dbCommand = dbConnection.CreateCommand();
			dbCommand.CommandText = "SHOW TABLES";
			using DbDataReader dbDataReader = dbCommand.ExecuteReader();
			while (dbDataReader.Read())
			{
				list.Add(dbDataReader.GetString(0));
			}
		}
		else if (config.DatabaseType == "SQL Server")
		{
			using DbCommand dbCommand2 = dbConnection.CreateCommand();
			dbCommand2.CommandText = "SELECT TABLE_SCHEMA + '.' + TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' ORDER BY 1";
			using DbDataReader dbDataReader2 = dbCommand2.ExecuteReader();
			while (dbDataReader2.Read())
			{
				list.Add(dbDataReader2.GetString(0));
			}
		}
		else if (config.DatabaseType == "SAP HANA")
		{
			using DbCommand dbCommand3 = dbConnection.CreateCommand();
			dbCommand3.CommandText = "SELECT SCHEMA_NAME || '.' || TABLE_NAME FROM TABLES WHERE IS_USER_DEFINED_TYPE = 'FALSE' ORDER BY 1";
			using DbDataReader dbDataReader3 = dbCommand3.ExecuteReader();
			while (dbDataReader3.Read())
			{
				list.Add(dbDataReader3.GetString(0));
			}
		}
		return list;
	}

	public DataTable ExecuteQuery(ErpImportConfig config, string query, int? maxRows = null)
	{
		using DbConnection dbConnection = CreateConnection(config);
		dbConnection.Open();
		using DbCommand dbCommand = dbConnection.CreateCommand();
		dbCommand.CommandText = query;
		if (maxRows.HasValue && config.DatabaseType == "MySQL")
		{
			dbCommand.CommandText = query.TrimEnd(';') + $" LIMIT {maxRows.Value}";
		}
		else if (maxRows.HasValue && config.DatabaseType == "SQL Server" && !query.Contains("TOP ", StringComparison.OrdinalIgnoreCase))
		{
			dbCommand.CommandText = $"SELECT TOP {maxRows.Value} * FROM ({query.TrimEnd(';')}) AS q";
		}
		DataTable dataTable = new DataTable();
		using DbDataReader reader = dbCommand.ExecuteReader();
		dataTable.Load(reader);
		if (maxRows.HasValue && dataTable.Rows.Count > maxRows.Value)
		{
			while (dataTable.Rows.Count > maxRows.Value)
			{
				dataTable.Rows.RemoveAt(dataTable.Rows.Count - 1);
			}
		}
		return dataTable;
	}

	public List<ItemRecord> MapRowsToItems(DataTable table, ErpImportConfig config)
	{
		List<ItemRecord> list = new List<ItemRecord>();
		foreach (DataRow row in table.Rows)
		{
			ItemRecord itemRecord = new ItemRecord
			{
				ItemCode = GetMappedValue(row, config.ItemCodeField),
				Gtin = GetMappedValue(row, config.GtinField),
				Description = GetMappedValue(row, config.DescriptionField),
				Level1 = ParseInt(GetMappedValue(row, config.Level1Field)),
				Level2 = ParseInt(GetMappedValue(row, config.Level2Field)),
				Level3 = ParseInt(GetMappedValue(row, config.Level3Field))
			};
			if (!string.IsNullOrWhiteSpace(itemRecord.ItemCode))
			{
				list.Add(itemRecord);
			}
		}
		return list;
	}

	private static string GetMappedValue(DataRow row, string fieldName)
	{
		if (string.IsNullOrWhiteSpace(fieldName) || !row.Table.Columns.Contains(fieldName))
		{
			return "";
		}
		return row[fieldName]?.ToString()?.Trim() ?? "";
	}

	private static int ParseInt(string value)
	{
		if (int.TryParse(value, out var result))
		{
			return result;
		}
		if (double.TryParse(value, out var result2))
		{
			return (int)result2;
		}
		return 0;
	}

	private static DbConnection CreateConnection(ErpImportConfig config)
	{
		return config.DatabaseType switch
		{
			"MySQL" => new MySqlConnection(BuildMySqlConnectionString(config)), 
			"SQL Server" => new SqlConnection(BuildSqlServerConnectionString(config)), 
			"SAP HANA" => new OdbcConnection(BuildHanaOdbcConnectionString(config)), 
			_ => throw new NotSupportedException("Database type '" + config.DatabaseType + "' is not supported."), 
		};
	}

	public static string BuildMySqlConnectionString(ErpImportConfig config)
	{
		string value = (string.IsNullOrWhiteSpace(config.Port) ? "3306" : config.Port);
		// SslMode=None avoids TLS HandshakeFailure against hosts that advertise SSL poorly
		// (common for remote/ERP MySQL). AllowPublicKeyRetrieval helps caching_sha2_password auth.
		return $"Server={config.Host};Port={value};Database={config.Database};Uid={config.Username};Pwd={config.Password};SslMode=None;AllowPublicKeyRetrieval=True;";
	}

	public static string BuildSqlServerConnectionString(ErpImportConfig config)
	{
		string value = (string.IsNullOrWhiteSpace(config.Port) ? "1433" : config.Port);
		return $"Server={config.Host},{value};Database={config.Database};User Id={config.Username};Password={config.Password};TrustServerCertificate=True;";
	}

	public static string BuildHanaOdbcConnectionString(ErpImportConfig config)
	{
		string value = (string.IsNullOrWhiteSpace(config.Port) ? "30015" : config.Port);
		return $"DRIVER={{HDBODBC}};SERVERNODE={config.Host}:{value};UID={config.Username};PWD={config.Password};";
	}
}
