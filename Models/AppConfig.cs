using System.Text.Json;

namespace EvoDesigner.Models
{
    public class AppConfig
    {
        public MySqlConnectionConfig MySqlConnection { get; set; } = new();
    }

    public class MySqlConnectionConfig
    {
        public string Host { get; set; } = "localhost";
        public string Port { get; set; } = "3306";
        public string Database { get; set; } = "";
        public string Username { get; set; } = "";
        public string Password { get; set; } = ""; // Note: In production, consider encrypting this
        public string LastQuery { get; set; } = "SELECT * FROM your_table LIMIT 10";
    }
}
