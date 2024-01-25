using System.Data.SQLite;
using System.Reflection.Metadata.Ecma335;

public class Database
{
    SQLiteConnection dbConnection;

    PasswordHasher passwordHasher = new PasswordHasher();
    public Database()
    {

        SQLiteConnectionStringBuilder builder = new SQLiteConnectionStringBuilder();
        builder.DataSource = "database.db";

        string connectionString = builder.ConnectionString;

        dbConnection = new SQLiteConnection(connectionString);

        // Open the connection
        dbConnection.Open();

        // Create Player table if it doesnt exist yet
        CreatePlayersTable();

        // Insert some data into the table
        //NewUser("user", "password");

        // Query and display data
        //QueryAndDisplayData();
    }
    void CreatePlayersTable()
    {
        using (SQLiteCommand command = new SQLiteCommand("CREATE TABLE IF NOT EXISTS Players (ID INTEGER PRIMARY KEY AUTOINCREMENT, Username TEXT, Password TEXT, LastIP TEXT, Wage INTEGER, Money INTEGER);", dbConnection))
        {
            command.ExecuteNonQuery();
        }
    }
    public bool RegisterUser(string username, string hashedPassword, string LastIPAddress)
    {
        if (!SearchIfUserExists(username)) // Runs if the chosen username isnt taken yet
        {
            using (SQLiteCommand command = new SQLiteCommand("INSERT INTO Players (Username, Password, LastIP, Wage, Money) VALUES (@username, @password, @lastip, @wage, @money);", dbConnection))
            {
                command.Parameters.AddWithValue("@username", username);
                command.Parameters.AddWithValue("@password", passwordHasher.EncryptPassword(hashedPassword)); // encrypts password using bcrypt
                command.Parameters.AddWithValue("@lastip", LastIPAddress);
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
        using (SQLiteCommand command = new SQLiteCommand("SELECT * FROM Players WHERE Username = @username", dbConnection))
        {
            command.Parameters.AddWithValue("@username", username);

            using (SQLiteDataReader reader = command.ExecuteReader())
            {
                if (reader.Read()) // User found
                {
                    if (passwordHasher.VerifyPassword(hashedPassword, $"{reader["Password"]}")) // Checks if passwords matches using bcrypt
                    {
                        Console.WriteLine("Login was correct");
                        return true;
                    }
                    else
                    {
                        Console.WriteLine("Login was fail");
                        return false;
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
    public bool SearchIfUserExists(string username)
    {
        using (SQLiteCommand command = new SQLiteCommand("SELECT * FROM Players WHERE Username = @username", dbConnection))
        {
            command.Parameters.AddWithValue("@username", username);

            using (SQLiteDataReader reader = command.ExecuteReader())
            {
                if (reader.Read()) { return true; } // User found
                else { return false; } // User not found
            }
        }
    }
    public void QueryAndDisplayData()
    {
        using (SQLiteCommand command = new SQLiteCommand("SELECT * FROM Players;", dbConnection))
        {
            using (SQLiteDataReader reader = command.ExecuteReader())
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