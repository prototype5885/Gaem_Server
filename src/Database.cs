using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

public static class Database
{
    static SqliteConnection dbConnection = new SqliteConnection("Data Source=database.db;Mode=ReadWriteCreate");
    public static void Initialize()
    {
        dbConnection.Open();
        using (SqliteCommand command = new SqliteCommand("CREATE TABLE IF NOT EXISTS Players (ID INTEGER PRIMARY KEY AUTOINCREMENT, Username TEXT, Password TEXT, Wage INTEGER, Money INTEGER);", dbConnection))
        {
            command.ExecuteNonQuery();
        }
    }
    public static AuthenticationResult RegisterUser(string username, string hashedPassword)
    {
        Console.WriteLine($"({DateTime.Now}) Player {username} is trying to register...");

        AuthenticationResult authenticationResult = new AuthenticationResult();

        if (username.Length < 2 || username.Length > 16) // Checks if username is longer than 16 or shorter than 2 characters
        {
            Console.WriteLine($"({DateTime.Now}) Player {username} chosen a too long or too short username");
            authenticationResult.result = 5;
            return authenticationResult; // Client's chosen username is too long or too short
        }
        using (SqliteCommand command = new SqliteCommand("SELECT * FROM Players WHERE Username = @username", dbConnection)) // checks if username is taken
        {
            command.Parameters.AddWithValue("@username", username);

            using (SqliteDataReader reader = command.ExecuteReader())
            {
                if (reader.Read())
                {
                    Console.WriteLine($"({DateTime.Now}) Player {username} is already taken");
                    authenticationResult.result = 6;
                    return authenticationResult; // Client's chosen username is already taken
                }

            }
        }
        using (SqliteCommand command = new SqliteCommand("INSERT INTO Players (Username, Password, Wage, Money) VALUES (@username, @password, @wage, @money);", dbConnection))
        {
            command.Parameters.AddWithValue("@username", username);
            command.Parameters.AddWithValue("@password", BCrypt.Net.BCrypt.HashPassword(hashedPassword, BCrypt.Net.BCrypt.GenerateSalt())); // encrypts password using bcrypt
            command.Parameters.AddWithValue("@wage", 1);
            command.Parameters.AddWithValue("@money", 1000);
            command.ExecuteNonQuery();

            Console.WriteLine($"({DateTime.Now}) Player {username} was successfully registered, logging in now...");
            authenticationResult.result = 1;
            return authenticationResult; // Registration was successful
        }
        //loginResult.loginResult = 1;

        //using (SqliteCommand command = new SqliteCommand("SELECT * FROM Players WHERE id = last_insert_rowid();", dbConnection))
        //{
        //    using (SqliteDataReader reader = command.ExecuteReader())
        //    {
        //        loginResult.loginResult = 1;
        //        loginResult.dbIndex = reader.GetInt32(0);
        //        loginResult.playerName = reader.GetString(1);

        //        Console.WriteLine($"({DateTime.Now}) Player {loginResult.playerName} was selected from the database");
        //        Console.WriteLine($"({DateTime.Now}) Player {loginResult.playerName} registered successfully");

        //        return loginResult;
        //    }
        //}
    }
    public static AuthenticationResult LoginUser(string username, string hashedPassword, ConnectedPlayer[] connectedPlayers)
    {
        Console.WriteLine($"({DateTime.Now}) Player {username} is trying to login...");

        AuthenticationResult authenticationResult = new AuthenticationResult();

        using (SqliteCommand command = new SqliteCommand("SELECT * FROM Players WHERE Username = @username", dbConnection))
        {
            command.Parameters.AddWithValue("@username", username);

            using (SqliteDataReader reader = command.ExecuteReader())
            {
                if (reader.Read()) // User found
                {
                    int databaseID = reader.GetInt32(0);
                    Console.WriteLine($"({DateTime.Now}) Player {username} was found in the database");
                    //if (passwordHasher.VerifyPassword(hashedPassword, $"{reader["Password"]}")) 
                    if (BCrypt.Net.BCrypt.Verify(hashedPassword, $"{reader["Password"]}")) // Checks if passwords matches using bcrypt
                    {
                        Console.WriteLine($"({DateTime.Now}) Player {username} has entered correct password");
                        foreach (ConnectedPlayer player in connectedPlayers) // checks if the user is already logged in
                        {
                            if (player == null) continue;
                            if (databaseID == player.databaseID)
                            {
                                Console.WriteLine($"({DateTime.Now}) Player {username} is logged in already");
                                authenticationResult.result = 4; // user is already logged in
                                return authenticationResult;
                            }
                        }
                        // runs if user isnt logged in yet

                        authenticationResult.result = 1; // Login success
                        authenticationResult.dbIndex = databaseID;
                        authenticationResult.playerName = reader.GetString(1);

                        Console.WriteLine($"({DateTime.Now}) Player {username} has logged in successfully, Database index:{databaseID}");

                        return authenticationResult;
                    }
                    else
                    {
                        Console.WriteLine($"({DateTime.Now}) Player {username} entered wrong password");
                        authenticationResult.result = 2; // wrong password
                        return authenticationResult;
                    }
                }
                else // No user registered with this username
                {
                    Console.WriteLine($"{DateTime.Now} Player {username} is not registered");
                    authenticationResult.result = 3; // no user found with this name
                    return authenticationResult;
                }
            }
        }
    }
    //public static string GetUsername(int databaseID)
    //{
    //    using (SqliteCommand command = new SqliteCommand("SELECT Username FROM Players WHERE ID = @databaseID", dbConnection))
    //    {
    //        command.Parameters.AddWithValue("@databaseID", databaseID);

    //        object result = command.ExecuteScalar();
    //        return result.ToString();

    //    }
    //}
    public static void DeleteUser(string username)
    {
        using (SqliteCommand command = new SqliteCommand("DELETE FROM Players WHERE Username = @username", dbConnection))
        {
            command.Parameters.AddWithValue("@username", username);
            command.ExecuteNonQuery();
        }
    }
    //public static void QueryAndDisplayData()
    //{
    //    using (SqliteCommand command = new SqliteCommand("SELECT * FROM Players;", dbConnection))
    //    {
    //        using (SqliteDataReader reader = command.ExecuteReader())
    //        {
    //            string text = "";
    //            while (reader.Read())
    //            {
    //                text = text + $"ID: {reader["ID"]}, Username: {reader["Username"]}, Password: {reader["Password"]}, Wage: {reader["Wage"]}, Money: {reader["Money"]}\n";
    //                //GD.Print(text);
    //                //Label label = GetNode<Label>("/root/Map/Player/HUD/Info2");
    //                //label.Text = text;

    //            }
    //        }
    //    }
    //}
}