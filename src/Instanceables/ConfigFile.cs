using log4net;

namespace Gaem_server.Instanceables;

public class ConfigFile
{
    private static readonly ILog Logger = LogManager.GetLogger(typeof(Server));
    
    public readonly int TcpPort;
    public readonly int TickRate;
    public readonly int MaxPlayers;
    public readonly string DatabaseType;
    public readonly string RemoteDatabaseIpAddress;
    public readonly string RemoteDatabasePort;
    public readonly string DbUsername;
    public readonly string DbPassword;

    public ConfigFile()
    {
        const string configFilePath = "config.txt";
        
        // runs if config file doesn't exist
        const string tcpPortString = "tcpPort";
        const string tickRateString = "tickRate";
        const string maxPlayersString = "maxPlayers";
        // const string encryptionKeyString = "encryptionKey";
        const string databaseTypeString = "databaseType";
        const string remoteDatabaseIpAddressString = "remoteDatabaseIpAddress";
        const string remoteDatabasePortString = "remoteDatabasePort";
        const string dbUsernameString = "dbUsername";
        const string dbPasswordString = "dbPassword";

        // creates config file if it doesnt exist
        Logger.Debug("Looking for config file...");
        if (File.Exists(configFilePath)) {
            Logger.Debug("Config file found");
        } else {
            Logger.Debug("Config file doesn't exist, creating new...");

            using (FileStream fs = File.Create(configFilePath))
            {
                Logger.Debug("Config file created successfully.");
            }

            using (StreamWriter writer = new StreamWriter(configFilePath))
            {
                writer.WriteLine(FormatConfig(tcpPortString, 1942.ToString()));
                writer.WriteLine(FormatConfig(tickRateString, 10.ToString()));
                writer.WriteLine(FormatConfig(maxPlayersString, 128.ToString()));
                // writer.WriteLine(FormatConfig(encryptionKeyString, "0123456789ABCDEF0123456789ABCDEF"));
                writer.WriteLine(FormatConfig(databaseTypeString, "sqlite"));
                writer.WriteLine(FormatConfig(remoteDatabaseIpAddressString, "127.0.0.1"));
                writer.WriteLine(FormatConfig(remoteDatabasePortString, "3306"));
                writer.WriteLine(FormatConfig(dbUsernameString, "username"));
                writer.WriteLine(FormatConfig(dbPasswordString, "password"));
            }
            Logger.Debug("New config file was created");
        }

        // runs if or when config file exists
        Logger.Debug("Reading config file...");

        using (StreamReader reader = new StreamReader(configFilePath))
        {
            // Read and display each line from the text file
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                string[] parts = line.Split("=");
                if (parts.Length == 2) {
                    string key = parts[0].Trim();
                    string value = parts[1].Trim();
                    switch (key) {
                        case tcpPortString:
                            TcpPort = int.Parse(value);
                            break;
                        case tickRateString:
                            TickRate = int.Parse(value);
                            break;
                        case maxPlayersString:
                            MaxPlayers = int.Parse(value);
                            break;
//                    case encryptionKeyString:
//                        encryptionKey = parts[1].trim();
//                        break;
                        case databaseTypeString:
                            DatabaseType = value;
                            break;
                        case remoteDatabaseIpAddressString:
                            RemoteDatabaseIpAddress = value;
                            break;
                        case remoteDatabasePortString:
                            RemoteDatabasePort = value;
                            break;
                        case dbUsernameString:
                            DbUsername = value;
                            break;
                        case dbPasswordString:
                            DbPassword = value;
                            break;
                    }
                }
            }
        }
        

        // checks if each values were read successfully
        var missingValues = new List<string>();

        if (TcpPort != 0) {
            Logger.Debug($"{tcpPortString}: {TcpPort}");
        } else {
            missingValues.Add(tcpPortString);
        }

        if (TickRate != 0) {
            Logger.Debug($"{tickRateString}: {TickRate}");
        } else {
            missingValues.Add(tickRateString);
        }

        if (MaxPlayers != 0) {
            Logger.Debug($"{maxPlayersString}: {MaxPlayers}");
        } else {
            missingValues.Add(maxPlayersString);
        }

//        if (encryptionKey != null) {
//            logger.Debug("{}: {}", encryptionKeyString, encryptionKey);
//        } else {
//            missingValues.add(encryptionKeyString);
//        }

        if (DatabaseType != null) {
            Logger.Debug($"{databaseTypeString}: {DatabaseType}");
        } else {
            missingValues.Add(databaseTypeString);
        }

        // these only get checked if type isn't sqlite as it doesn't require
        // authentication
        if (DatabaseType != null && !DatabaseType.Equals("sqlite")) {
            if (RemoteDatabaseIpAddress != null) {
                Logger.Debug($"{remoteDatabaseIpAddressString}: {RemoteDatabaseIpAddress}");
            } else {
                missingValues.Add(remoteDatabaseIpAddressString);
            }

            if (RemoteDatabasePort != null) {
                Logger.Debug($"{remoteDatabasePortString}: {RemoteDatabasePort}");
            } else {
                missingValues.Add(remoteDatabasePortString);
            }

            if (DbUsername != null) {
                Logger.Debug($"{dbUsernameString}: {DbUsername}");
            } else {
                missingValues.Add(dbUsernameString);
            }

            if (DbPassword != null) {
                Logger.Debug($"{dbPasswordString}: {DbPassword}");
            } else {
                missingValues.Add(dbPasswordString);
            }
        } else {
            Logger.Debug($"{databaseTypeString} is {DatabaseType}, extra authentication is not needed");
        }


        if (missingValues.Count != 0) {
            foreach (string missingConfigValue in missingValues) {
                Logger.Fatal($"{missingConfigValue} is missing from config file");
            }
            throw new Exception("Incomplete config file");
        } else {
            Logger.Info("Config file read successfully");
        }
    }
    
    private string FormatConfig(string name, string value) {
        return name + "=" + value;
    }
}