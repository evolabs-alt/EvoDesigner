using MySql.Data.MySqlClient;
using System.Data;

namespace EvoDesigner.Services
{
    public class MySqlService
    {
        public string ConnectionString { get; set; }

        public DataTable ExecuteQuery(string query)
        {
            using var conn = new MySqlConnection(ConnectionString);
            using var cmd = new MySqlCommand(query, conn);
            var dt = new DataTable();
            conn.Open();
            using var reader = cmd.ExecuteReader();
            dt.Load(reader);
            return dt;
        }

        public int ExecuteNonQuery(string query)
        {
            using var conn = new MySqlConnection(ConnectionString);
            using var cmd = new MySqlCommand(query, conn);
            conn.Open();
            return cmd.ExecuteNonQuery();
        }
    }
}
