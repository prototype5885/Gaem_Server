using Microsoft.Data.Sqlite;
using System.Linq;
using System.Reflection.Metadata.Ecma335;

public class Database
{
    private SqliteConnection dbConnection;



    private static readonly PasswordHasher passwordHasher = new PasswordHasher();
    public Database()
    {
        // Open the connection
        SqliteConnectionStringBuilder builder = new SqliteConnectionStringBuilder();
        builder.DataSource = "database.db";

        string connectionString = builder.ConnectionString;



        dbConnection = new SqliteConnection(connectionString);


        dbConnection.Open();

        // Create Player table if it doesnt exist yet
        CreatePlayersTable();

        // tests
        //UpdateLastLoginIP("proto", "xd");

        //DeleteUser("user2");

    }
    void CreatePlayersTable()
    {

        using (SqliteCommand command = new SqliteCommand("CREATE TABLE IF NOT EXISTS Players (ID INTEGER PRIMARY KEY AUTOINCREMENT, Username TEXT, Password TEXT, LastLoginIP TEXT, Wage INTEGER, Money INTEGER);", dbConnection))
        {
            command.ExecuteNonQuery();
        }
    }
    public bool RegisterUser(string username, string hashedPassword, string LastLoginIPAddress)
    {
        if (!CheckIfUserExists(username)) // Runs if the chosen username isnt taken yet
        {
            using (SqliteCommand command = new SqliteCommand("INSERT INTO Players (Username, Password, LastLoginIP, Wage, Money) VALUES (@username, @password, @lastloginip, @wage, @money);", dbConnection))
            {
                command.Parameters.AddWithValue("@username", username);
                command.Parameters.AddWithValue("@password", passwordHasher.EncryptPassword(hashedPassword)); // encrypts password using bcrypt
                command.Parameters.AddWithValue("@lastloginip", LastLoginIPAddress);
                command.Parameters.AddWithValue("@wage", 1);
                command.Parameters.AddWithValue("@money", 1000);

                command.ExecuteNonQuery();

                return true;
            }
        }
        else
        {
            return false;
        }
    }
    public bool LoginUser(string username, string hashedPassword)
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
                        return true; // Login was correct
                    }
                    else
                    {
                        return false; // Login failed
                    }
                }
                else // No user registered with this username
                {
                    Console.WriteLine("User not found.");
                    return false;
                }
            }
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
    public void UpdateLastLoginIP(string username, string LastLoginIP)
    {
        using (SqliteCommand command = new SqliteCommand($"UPDATE Players SET LastLoginIP = @lastloginip WHERE Username = @username", dbConnection))
        {
            command.Parameters.AddWithValue("@username", username);
            command.Parameters.AddWithValue("@lastloginip", LastLoginIP);
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
    public int GetUserID(string username)
    {
        using (SqliteCommand command = new SqliteCommand("SELECT * FROM Players WHERE Username = @username", dbConnection))
        {
            command.Parameters.AddWithValue("@username", username);

            using (SqliteDataReader reader = command.ExecuteReader())
            {
                if (reader.Read())
                {
                    return reader.GetInt32(0);
                }
                else
                {
                    return -1;
                }
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

    internal void UpdateLastLoginIP(string username, object clientIpAddress)
    {
        throw new NotImplementedException();
    }
}