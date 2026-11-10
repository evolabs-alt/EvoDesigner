using System.Data;
using MySql.Data.MySqlClient;

namespace TaggJack.Services;

public class MySqlService
{
	public string ConnectionString { get; set; }

	public DataTable ExecuteQuery(string query)
	{
		using MySqlConnection mySqlConnection = new MySqlConnection(ConnectionString);
		using MySqlCommand mySqlCommand = new MySqlCommand(query, mySqlConnection);
		DataTable dataTable = new DataTable();
		mySqlConnection.Open();
		using MySqlDataReader reader = mySqlCommand.ExecuteReader();
		dataTable.Load(reader);
		return dataTable;
	}

	public int ExecuteNonQuery(string query)
	{
		using MySqlConnection mySqlConnection = new MySqlConnection(ConnectionString);
		using MySqlCommand mySqlCommand = new MySqlCommand(query, mySqlConnection);
		mySqlConnection.Open();
		return mySqlCommand.ExecuteNonQuery();
	}
}
