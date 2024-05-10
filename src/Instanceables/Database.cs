using Gaem_server.Instanceables;
using Gaem_server.src.Classes;
using log4net;
using Microsoft.Data.Sqlite;

namespace Gaem_server.Instanceables;

public class Database
{
    private static readonly ILog logger = LogManager.GetLogger(typeof(Database));

    private SqliteConnection dbConnection;
    public void ConnectToDatabase(ConfigFile configFile)
    {
        string dbType;
        string dbName;
        string dbUrl;
        string query;

        string ipAddress;
        string port;

        string username;
        string password;

        switch (configFile.DatabaseType)
        {
            case "sqlite":
                logger.Debug("Connecting to SQLite database...");
                dbConnection = new SqliteConnection("Data Source=database.db;Mode=ReadWriteCreate");
                dbConnection.Open();
                query = InitialQuery(configFile.DatabaseType);
                using (SqliteCommand command = new SqliteCommand(query, dbConnection))
                {
                    command.ExecuteNonQuery();
                }
                break;
        }
    }

    private string InitialQuery(string dbType)
    {
        logger.Debug($"Initial query to database, database type: {dbType}");

        string autoIncrement = "AUTOINCREMENT";
        
        if (!dbType.Equals("sqlite")) {
            autoIncrement = "AUTO_INCREMENT";
        }

        return "CREATE TABLE IF NOT EXISTS Players " +
               "(ID INTEGER PRIMARY KEY " + autoIncrement + "," +
               "PlayerName TEXT," +
               "Password CHAR(64)," +
               "Wage INTEGER," +
               "Money INTEGER," +
               "LastLoginIp TEXT," +
               "LastPosition TEXT)";
    }

    public void RegisterPlayer(string playerName, string hashedPassword)
    {
        logger.Debug($"Adding player {playerName} into the database...");

        using SqliteCommand command = 
            new SqliteCommand("INSERT INTO Players (PlayerName, Password, Wage, Money) VALUES (@playerName, @password, @wage, @money);", dbConnection);
        command.Parameters.AddWithValue("@playerName", playerName);
        command.Parameters.AddWithValue("@password", hashedPassword); // encrypts password using bcrypt
        command.Parameters.AddWithValue("@wage", 3);
        command.Parameters.AddWithValue("@money", 1010);
        command.ExecuteNonQuery();

        logger.Info($"Player {playerName} has been added to the database");
    }

    public DatabasePlayer SearchForPlayerInDatabase(string playerName)
    {
        logger.Debug($"Searching for player {playerName} in the database...");
        using SqliteCommand command =
            new SqliteCommand("SELECT * FROM Players WHERE PlayerName = @playerName", dbConnection);
        command.Parameters.AddWithValue("@playerName", playerName);

        using (SqliteDataReader reader = command.ExecuteReader())
        {
            if (reader.Read()) // User found
            {
                logger.Debug($"Found player {playerName} in the database");

                DatabasePlayer databasePlayer = new DatabasePlayer
                {
                    id = Convert.ToInt32(reader["id"]),
                    name = reader["PlayerName"].ToString(),
                    password = reader["Password"].ToString(),
                    wage = Convert.ToInt32(reader["Wage"]),
                    money = Convert.ToInt32(reader["Money"]),
                    lastLoginIp = reader["LastLoginIp"].ToString(),
                    lastPosition = reader["LastPosition"].ToString(),
                };
                logger.Debug("Returning...");
                return databasePlayer;
            }
        }
        logger.Debug($"Player {playerName} was not found in the database");
        return null;
    }

    // public void UpdatePlayerPosition(string playerName, string jsonPlayerPosition)
    // {
    //     using SqliteCommand command =
    //         new SqliteCommand("UPDATE Players SET LastPosition @playerPosition WHERE PlayerName = @playerName", dbConnection);
    //     command.Parameters.AddWithValue("@playerPosition", jsonPlayerPosition);
    //     command.Parameters.AddWithValue("@playerName", playerName);
    //     command.ExecuteNonQuery();
    // }
}