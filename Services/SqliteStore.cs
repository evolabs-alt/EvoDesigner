using System;
using System.Collections.Generic;
using System.Linq;
using TaggJack.Models;
using Microsoft.Data.Sqlite;

namespace TaggJack.Services;

public class SqliteStore
{
	private readonly string _dbPath;

	public SqliteStore()
	{
		_dbPath = AppPaths.GetSharedDataPath("taggjack.db");
	}

	public void EnsureInitialized()
	{
		using SqliteConnection sqliteConnection = OpenConnection();
		using SqliteCommand sqliteCommand = sqliteConnection.CreateCommand();
		sqliteCommand.CommandText = "CREATE TABLE IF NOT EXISTS items (\r\n    item_code TEXT PRIMARY KEY,\r\n    gtin TEXT NOT NULL DEFAULT '',\r\n    description TEXT NOT NULL DEFAULT '',\r\n    level1 INTEGER NOT NULL DEFAULT 0,\r\n    level2 INTEGER NOT NULL DEFAULT 0,\r\n    level3 INTEGER NOT NULL DEFAULT 0,\r\n    updated_at TEXT NOT NULL\r\n);\r\n\r\nCREATE TABLE IF NOT EXISTS gs1_jobs (\r\n    id INTEGER PRIMARY KEY AUTOINCREMENT,\r\n    item_code TEXT NOT NULL,\r\n    description TEXT NOT NULL DEFAULT '',\r\n    gtin TEXT NOT NULL DEFAULT '',\r\n    quantity INTEGER NOT NULL,\r\n    level1_count INTEGER NOT NULL,\r\n    level2_count INTEGER NOT NULL,\r\n    level3_count INTEGER NOT NULL,\r\n    batch TEXT NOT NULL DEFAULT '',\r\n    production TEXT NOT NULL DEFAULT '',\r\n    expiry TEXT NOT NULL DEFAULT '',\r\n    level1_template_path TEXT NOT NULL DEFAULT '',\r\n    level2_template_path TEXT NOT NULL DEFAULT '',\r\n    level3_template_path TEXT NOT NULL DEFAULT '',\r\n    level1_printer TEXT NOT NULL DEFAULT '',\r\n    level2_printer TEXT NOT NULL DEFAULT '',\r\n    level3_printer TEXT NOT NULL DEFAULT '',\r\n    raw_response_json TEXT NOT NULL DEFAULT '',\r\n    created_at TEXT NOT NULL\r\n);\r\n\r\nCREATE TABLE IF NOT EXISTS gs1_serials (\r\n    id INTEGER PRIMARY KEY AUTOINCREMENT,\r\n    job_id INTEGER NOT NULL,\r\n    level INTEGER NOT NULL,\r\n    serial TEXT NOT NULL,\r\n    parent_serial TEXT NOT NULL DEFAULT '',\r\n    sscc TEXT NOT NULL DEFAULT '',\r\n    FOREIGN KEY(job_id) REFERENCES gs1_jobs(id)\r\n);\r\n\r\nCREATE INDEX IF NOT EXISTS idx_gs1_serials_job ON gs1_serials(job_id);";
		sqliteCommand.ExecuteNonQuery();
	}

	public List<ItemRecord> GetAllItems(string? search = null)
	{
		EnsureInitialized();
		using SqliteConnection sqliteConnection = OpenConnection();
		using SqliteCommand sqliteCommand = sqliteConnection.CreateCommand();
		if (string.IsNullOrWhiteSpace(search))
		{
			sqliteCommand.CommandText = "SELECT item_code, gtin, description, level1, level2, level3, updated_at FROM items ORDER BY item_code";
		}
		else
		{
			sqliteCommand.CommandText = "SELECT item_code, gtin, description, level1, level2, level3, updated_at\r\nFROM items\r\nWHERE item_code LIKE @s OR gtin LIKE @s OR description LIKE @s\r\nORDER BY item_code";
			sqliteCommand.Parameters.AddWithValue("@s", "%" + search + "%");
		}
		return ReadItems(sqliteCommand);
	}

	public ItemRecord? GetItem(string itemCode)
	{
		EnsureInitialized();
		using SqliteConnection sqliteConnection = OpenConnection();
		using SqliteCommand sqliteCommand = sqliteConnection.CreateCommand();
		sqliteCommand.CommandText = "SELECT item_code, gtin, description, level1, level2, level3, updated_at FROM items WHERE item_code = @code";
		sqliteCommand.Parameters.AddWithValue("@code", itemCode);
		return ReadItems(sqliteCommand).FirstOrDefault();
	}

	public void UpsertItem(ItemRecord item)
	{
		EnsureInitialized();
		item.UpdatedAt = DateTime.UtcNow;
		using SqliteConnection sqliteConnection = OpenConnection();
		using SqliteCommand sqliteCommand = sqliteConnection.CreateCommand();
		sqliteCommand.CommandText = "INSERT INTO items (item_code, gtin, description, level1, level2, level3, updated_at)\r\nVALUES (@code, @gtin, @desc, @l1, @l2, @l3, @updated)\r\nON CONFLICT(item_code) DO UPDATE SET\r\n    gtin = excluded.gtin,\r\n    description = excluded.description,\r\n    level1 = excluded.level1,\r\n    level2 = excluded.level2,\r\n    level3 = excluded.level3,\r\n    updated_at = excluded.updated_at";
		BindItem(sqliteCommand, item);
		sqliteCommand.ExecuteNonQuery();
	}

	public void DeleteItem(string itemCode)
	{
		EnsureInitialized();
		using SqliteConnection sqliteConnection = OpenConnection();
		using SqliteCommand sqliteCommand = sqliteConnection.CreateCommand();
		sqliteCommand.CommandText = "DELETE FROM items WHERE item_code = @code";
		sqliteCommand.Parameters.AddWithValue("@code", itemCode);
		sqliteCommand.ExecuteNonQuery();
	}

	public long SaveJob(Gs1JobRecord job, List<Gs1SerialRecord> serials)
	{
		EnsureInitialized();
		using SqliteConnection sqliteConnection = OpenConnection();
		using SqliteTransaction sqliteTransaction = sqliteConnection.BeginTransaction();
		using SqliteCommand sqliteCommand = sqliteConnection.CreateCommand();
		sqliteCommand.Transaction = sqliteTransaction;
		sqliteCommand.CommandText = "INSERT INTO gs1_jobs (\r\n    item_code, description, gtin, quantity, level1_count, level2_count, level3_count,\r\n    batch, production, expiry,\r\n    level1_template_path, level2_template_path, level3_template_path,\r\n    level1_printer, level2_printer, level3_printer,\r\n    raw_response_json, created_at\r\n) VALUES (\r\n    @item_code, @description, @gtin, @quantity, @l1c, @l2c, @l3c,\r\n    @batch, @production, @expiry,\r\n    @l1t, @l2t, @l3t,\r\n    @l1p, @l2p, @l3p,\r\n    @raw, @created\r\n) RETURNING id;";
		sqliteCommand.Parameters.AddWithValue("@item_code", job.ItemCode);
		sqliteCommand.Parameters.AddWithValue("@description", job.Description);
		sqliteCommand.Parameters.AddWithValue("@gtin", job.Gtin);
		sqliteCommand.Parameters.AddWithValue("@quantity", job.Quantity);
		sqliteCommand.Parameters.AddWithValue("@l1c", job.Level1Count);
		sqliteCommand.Parameters.AddWithValue("@l2c", job.Level2Count);
		sqliteCommand.Parameters.AddWithValue("@l3c", job.Level3Count);
		sqliteCommand.Parameters.AddWithValue("@batch", job.Batch);
		sqliteCommand.Parameters.AddWithValue("@production", job.Production);
		sqliteCommand.Parameters.AddWithValue("@expiry", job.Expiry);
		sqliteCommand.Parameters.AddWithValue("@l1t", job.Level1TemplatePath);
		sqliteCommand.Parameters.AddWithValue("@l2t", job.Level2TemplatePath);
		sqliteCommand.Parameters.AddWithValue("@l3t", job.Level3TemplatePath);
		sqliteCommand.Parameters.AddWithValue("@l1p", job.Level1Printer);
		sqliteCommand.Parameters.AddWithValue("@l2p", job.Level2Printer);
		sqliteCommand.Parameters.AddWithValue("@l3p", job.Level3Printer);
		sqliteCommand.Parameters.AddWithValue("@raw", job.RawResponseJson);
		sqliteCommand.Parameters.AddWithValue("@created", job.CreatedAt.ToString("o"));
		long num = (long)(sqliteCommand.ExecuteScalar() ?? ((object)0L));
		foreach (Gs1SerialRecord serial in serials)
		{
			using SqliteCommand sqliteCommand2 = sqliteConnection.CreateCommand();
			sqliteCommand2.Transaction = sqliteTransaction;
			sqliteCommand2.CommandText = "INSERT INTO gs1_serials (job_id, level, serial, parent_serial, sscc)\r\nVALUES (@job_id, @level, @serial, @parent, @sscc)";
			sqliteCommand2.Parameters.AddWithValue("@job_id", num);
			sqliteCommand2.Parameters.AddWithValue("@level", serial.Level);
			sqliteCommand2.Parameters.AddWithValue("@serial", serial.Serial);
			sqliteCommand2.Parameters.AddWithValue("@parent", serial.ParentSerial);
			sqliteCommand2.Parameters.AddWithValue("@sscc", serial.Sscc);
			sqliteCommand2.ExecuteNonQuery();
		}
		sqliteTransaction.Commit();
		return num;
	}

	public List<Gs1JobRecord> GetJobs(int limit = 200)
	{
		EnsureInitialized();
		using SqliteConnection sqliteConnection = OpenConnection();
		using SqliteCommand sqliteCommand = sqliteConnection.CreateCommand();
		sqliteCommand.CommandText = "SELECT id, item_code, description, gtin, quantity, level1_count, level2_count, level3_count,\r\n       batch, production, expiry,\r\n       level1_template_path, level2_template_path, level3_template_path,\r\n       level1_printer, level2_printer, level3_printer,\r\n       raw_response_json, created_at\r\nFROM gs1_jobs ORDER BY created_at DESC LIMIT @limit";
		sqliteCommand.Parameters.AddWithValue("@limit", limit);
		return ReadJobs(sqliteCommand);
	}

	public Gs1JobRecord? GetJob(long jobId)
	{
		EnsureInitialized();
		using SqliteConnection sqliteConnection = OpenConnection();
		using SqliteCommand sqliteCommand = sqliteConnection.CreateCommand();
		sqliteCommand.CommandText = "SELECT id, item_code, description, gtin, quantity, level1_count, level2_count, level3_count,\r\n       batch, production, expiry,\r\n       level1_template_path, level2_template_path, level3_template_path,\r\n       level1_printer, level2_printer, level3_printer,\r\n       raw_response_json, created_at\r\nFROM gs1_jobs WHERE id = @id";
		sqliteCommand.Parameters.AddWithValue("@id", jobId);
		return ReadJobs(sqliteCommand).FirstOrDefault();
	}

	public List<Gs1SerialRecord> GetSerialsForJob(long jobId, int? level = null)
	{
		EnsureInitialized();
		using SqliteConnection sqliteConnection = OpenConnection();
		using SqliteCommand sqliteCommand = sqliteConnection.CreateCommand();
		if (level.HasValue)
		{
			sqliteCommand.CommandText = "SELECT id, job_id, level, serial, parent_serial, sscc FROM gs1_serials WHERE job_id = @job AND level = @level ORDER BY id";
			sqliteCommand.Parameters.AddWithValue("@level", level.Value);
		}
		else
		{
			sqliteCommand.CommandText = "SELECT id, job_id, level, serial, parent_serial, sscc FROM gs1_serials WHERE job_id = @job ORDER BY level, id";
		}
		sqliteCommand.Parameters.AddWithValue("@job", jobId);
		return ReadSerials(sqliteCommand);
	}

	private SqliteConnection OpenConnection()
	{
		SqliteConnection sqliteConnection = new SqliteConnection("Data Source=" + _dbPath);
		sqliteConnection.Open();
		return sqliteConnection;
	}

	private static List<ItemRecord> ReadItems(SqliteCommand cmd)
	{
		List<ItemRecord> list = new List<ItemRecord>();
		using SqliteDataReader sqliteDataReader = cmd.ExecuteReader();
		while (sqliteDataReader.Read())
		{
			list.Add(new ItemRecord
			{
				ItemCode = sqliteDataReader.GetString(0),
				Gtin = sqliteDataReader.GetString(1),
				Description = sqliteDataReader.GetString(2),
				Level1 = sqliteDataReader.GetInt32(3),
				Level2 = sqliteDataReader.GetInt32(4),
				Level3 = sqliteDataReader.GetInt32(5),
				UpdatedAt = DateTime.Parse(sqliteDataReader.GetString(6))
			});
		}
		return list;
	}

	private static List<Gs1JobRecord> ReadJobs(SqliteCommand cmd)
	{
		List<Gs1JobRecord> list = new List<Gs1JobRecord>();
		using SqliteDataReader sqliteDataReader = cmd.ExecuteReader();
		while (sqliteDataReader.Read())
		{
			list.Add(new Gs1JobRecord
			{
				Id = sqliteDataReader.GetInt64(0),
				ItemCode = sqliteDataReader.GetString(1),
				Description = sqliteDataReader.GetString(2),
				Gtin = sqliteDataReader.GetString(3),
				Quantity = sqliteDataReader.GetInt32(4),
				Level1Count = sqliteDataReader.GetInt32(5),
				Level2Count = sqliteDataReader.GetInt32(6),
				Level3Count = sqliteDataReader.GetInt32(7),
				Batch = sqliteDataReader.GetString(8),
				Production = sqliteDataReader.GetString(9),
				Expiry = sqliteDataReader.GetString(10),
				Level1TemplatePath = sqliteDataReader.GetString(11),
				Level2TemplatePath = sqliteDataReader.GetString(12),
				Level3TemplatePath = sqliteDataReader.GetString(13),
				Level1Printer = sqliteDataReader.GetString(14),
				Level2Printer = sqliteDataReader.GetString(15),
				Level3Printer = sqliteDataReader.GetString(16),
				RawResponseJson = sqliteDataReader.GetString(17),
				CreatedAt = DateTime.Parse(sqliteDataReader.GetString(18))
			});
		}
		return list;
	}

	private static List<Gs1SerialRecord> ReadSerials(SqliteCommand cmd)
	{
		List<Gs1SerialRecord> list = new List<Gs1SerialRecord>();
		using SqliteDataReader sqliteDataReader = cmd.ExecuteReader();
		while (sqliteDataReader.Read())
		{
			list.Add(new Gs1SerialRecord
			{
				Id = sqliteDataReader.GetInt64(0),
				JobId = sqliteDataReader.GetInt64(1),
				Level = sqliteDataReader.GetInt32(2),
				Serial = sqliteDataReader.GetString(3),
				ParentSerial = sqliteDataReader.GetString(4),
				Sscc = sqliteDataReader.GetString(5)
			});
		}
		return list;
	}

	private static void BindItem(SqliteCommand cmd, ItemRecord item)
	{
		cmd.Parameters.AddWithValue("@code", item.ItemCode);
		cmd.Parameters.AddWithValue("@gtin", item.Gtin);
		cmd.Parameters.AddWithValue("@desc", item.Description);
		cmd.Parameters.AddWithValue("@l1", item.Level1);
		cmd.Parameters.AddWithValue("@l2", item.Level2);
		cmd.Parameters.AddWithValue("@l3", item.Level3);
		cmd.Parameters.AddWithValue("@updated", item.UpdatedAt.ToString("o"));
	}
}
