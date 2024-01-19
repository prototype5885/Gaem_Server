using System.Data.SQLite;

public class Database
{
    SQLiteConnection dbConnection;

    public Database()
    {

        SQLiteConnectionStringBuilder builder = new SQLiteConnectionStringBuilder();
        builder.DataSource = "database.db";
        //builder.DataSource = "database.db";

        string connectionString = builder.ConnectionString;

        dbConnection = new SQLiteConnection(connectionString);

        // Open the connection
        dbConnection.Open();

        // Create a table
        CreatePlayersTable();


        // Insert some data into the table
        //InsertData(dbConnection, "Attila", 2, 1000);
        //InsertData(dbConnection, "Yadana", 3, 1256);
        //NewUser(dbConnection, "testuser", "secret");

        // Query and display data
        //QueryAndDisplayData();
    }
    void CreatePlayersTable()
    {
        using (SQLiteCommand command = new SQLiteCommand("CREATE TABLE IF NOT EXISTS Players (ID INTEGER PRIMARY KEY AUTOINCREMENT, Username TEXT, Password TEXT, Wage INTEGER, Money INTEGER);", dbConnection))
        {
            command.ExecuteNonQuery();
        }
    }
    public void NewUser(string username, string hashedPassword)
    {
        string encryptedPassword = BCrypt.Net.BCrypt.HashPassword(hashedPassword);

        using (SQLiteCommand command = new SQLiteCommand("INSERT INTO Players (Username, Password, Wage, Money) VALUES (@username, @password, @wage, @money);", dbConnection))
        {
            //command.Parameters.AddWithValue("@id", id);
            command.Parameters.AddWithValue("@username", username);
            command.Parameters.AddWithValue("@password", encryptedPassword);
            command.Parameters.AddWithValue("@wage", 1);
            command.Parameters.AddWithValue("@money", 1000);

            command.ExecuteNonQuery();
        }
    }
    public void LoginUser(string username, string hashedPassword)
    {
        using (SQLiteCommand command = new SQLiteCommand("SELECT * FROM Players WHERE Username = @username", dbConnection))
        {
            command.Parameters.AddWithValue("@username", username);

            using (SQLiteDataReader reader = command.ExecuteReader())
            {
                if (reader.Read())
                {
                    // User found
                    Console.WriteLine(BCrypt.Net.BCrypt.Verify(hashedPassword, $"{reader["Password"]}"));
                }
                else
                {
                    Console.WriteLine("User not found.");
                }
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