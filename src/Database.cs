using Microsoft.Data.Sqlite;
using System.Linq;
using System.Net;
using System.Reflection.Metadata.Ecma335;

public class Database
{
    private SqliteConnection dbConnection;

    // private List<int> loggedInIds = new List<int>();
    public Dictionary<string, int> loggedInIds = new Dictionary<string, int>();

    private static readonly PasswordHasher passwordHasher = new PasswordHasher();
    public Database()
    {
        // Open the connection
        SqliteConnectionStringBuilder builder = new SqliteConnectionStringBuilder();
        builder.DataSource = "database.db";
        builder.Mode = SqliteOpenMode.ReadWriteCreate;

        string connectionString = builder.ConnectionString;
        dbConnection = new SqliteConnection(connectionString);
        dbConnection.Open();

        // Create Player table if it doesnt exist yet
        CreatePlayersTable();
    }
    void CreatePlayersTable()
    {
        using (SqliteCommand command = new SqliteCommand("CREATE TABLE IF NOT EXISTS Players (ID INTEGER PRIMARY KEY AUTOINCREMENT, Username TEXT, Password TEXT, LastLoginIP TEXT, Wage INTEGER, Money INTEGER);", dbConnection))
        {
            command.ExecuteNonQuery();
        }
    }
    public bool RegisterUser(string username, string hashedPassword, string clientAddress)
    {
        if (!CheckIfUserExists(username)) // Runs if the chosen username isnt taken yet
        {
            using (SqliteCommand command = new SqliteCommand("INSERT INTO Players (Username, Password, LastLoginIP, Wage, Money) VALUES (@username, @password, @lastloginip, @wage, @money);", dbConnection))
            {
                command.Parameters.AddWithValue("@username", username);
                command.Parameters.AddWithValue("@password", passwordHasher.EncryptPassword(hashedPassword)); // encrypts password using bcrypt
                command.Parameters.AddWithValue("@lastloginip", clientAddress);
                command.Parameters.AddWithValue("@wage", 1);
                command.Parameters.AddWithValue("@money", 1000);
                command.ExecuteNonQuery();

                UpdateLastIpAddress(username, clientAddress);

                int databaseID = GetDatabaseID(clientAddress);
                loggedInIds.Add(clientAddress, databaseID);

                return true;
            }
        }
        else
        {
            return false;
        }
    }
    public byte LoginUser(string username, string hashedPassword, string clientAddress)
    {
        using (SqliteCommand command = new SqliteCommand("SELECT * FROM Players WHERE Username = @username", dbConnection))
        {
            command.Parameters.AddWithValue("@username", username);

            using (SqliteDataReader reader = command.ExecuteReader())
            {
                if (reader.Read()) // User found
                {
                    if (passwordHasher.VerifyPassword(hashedPassword, $"{reader["Password"]}")) // Checks if passwords matches using bcrypt
                    {
                        foreach (int id in loggedInIds.Values) // checks if the user is already logged in
                        {
                            if (reader.GetInt32(0) == id)
                            {
                                System.Console.WriteLine("user is already logged in");
                                return 4; // user is already logged in
                            }
                        }
                        // runs if user isnt logged in yet
                        UpdateLastIpAddress(username, clientAddress);

                        int databaseID = GetDatabaseID(clientAddress);
                        loggedInIds.Add(clientAddress, databaseID);

                        return 1; // Login success
                    }
                    else
                    {
                        return 2; // wrong password
                    }
                }
                else // No user registered with this username
                {
                    Console.WriteLine("User not found.");
                    return 3; // no user found with this name
                }
            }
        }
    }
    public void UpdateLastIpAddress(string username, string LastLoginIP)
    {
        using (SqliteCommand command = new SqliteCommand($"UPDATE Players SET LastLoginIP = @lastloginip WHERE Username = @username", dbConnection))
        {
            command.Parameters.AddWithValue("@username", username);
            command.Parameters.AddWithValue("@lastloginip", LastLoginIP);
            command.ExecuteNonQuery();
        }
    }
    private int GetDatabaseID(string clientAddress)
    {
        using (SqliteCommand command = new SqliteCommand("SELECT ID FROM Players WHERE LastLoginIP = @lastloginip", dbConnection))
        {
            command.Parameters.AddWithValue("@lastloginip", clientAddress);

            object result = command.ExecuteScalar();
            return Convert.ToInt32(result);
        }
    }
    public string GetUsername(int databaseID)
    {
        using (SqliteCommand command = new SqliteCommand("SELECT Username FROM Players WHERE ID = @databaseID", dbConnection))
        {
            command.Parameters.AddWithValue("@databaseID", databaseID);

            object result = command.ExecuteScalar();
            return result.ToString();

        }
    }
    public void DeleteUser(string username)
    {
        using (SqliteCommand command = new SqliteCommand("DELETE FROM Players WHERE Username = @username", dbConnection))
        {
            command.Parameters.AddWithValue("@username", username);
            command.ExecuteNonQuery();
        }
    }
    private bool CheckIfUserExists(string username)
    {
        using (SqliteCommand command = new SqliteCommand("SELECT * FROM Players WHERE Username = @username", dbConnection))
        {
            command.Parameters.AddWithValue("@username", username);

            using (SqliteDataReader reader = command.ExecuteReader())
            {
                if (reader.Read()) { return true; } // User found
                else { return false; } // User not found
            }
        }
    }
    public void QueryAndDisplayData()
    {
        using (SqliteCommand command = new SqliteCommand("SELECT * FROM Players;", dbConnection))
        {
            using (SqliteDataReader reader = command.ExecuteReader())
            {
                string text = "";
                while (reader.Read())
                {
                    text = text + $"ID: {reader["ID"]}, Username: {reader["Username"]}, Password: {reader["Password"]}, Wage: {reader["Wage"]}, Money: {reader["Money"]}\n";
                    //GD.Print(text);
                    //Label label = GetNode<Label>("/root/Map/Player/HUD/Info2");
                    //label.Text = text;

                }
            }
        }
    }
}