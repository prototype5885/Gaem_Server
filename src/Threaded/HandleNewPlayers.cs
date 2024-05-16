using Gaem_server.Classes;
using Gaem_server.src.Classes;
using Gaem_server.src.ClassesShared;
using Gaem_server.Static;
using log4net;
using System.Diagnostics;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Gaem_server.ClassesShared;
using LoginDataContext = Gaem_server.ClassesShared.LoginDataContext;

namespace Gaem_server.Threaded;

public class HandleNewPlayers()
{
    private static readonly ILog logger = LogManager.GetLogger(typeof(HandleNewPlayers));

    private byte[] defaultKey = Encoding.UTF8.GetBytes("zTF7QCAw5amV7OxHQKE82rZKwebXrPkp");

    public async Task run()
    {
        while (true)
        {
            try
            {
                // waits for a player to connect
                logger.Debug("Waiting for a player to connect...");
                TcpClient tcpClient = MainC.tcpListener.AcceptTcpClient();

                string ipAddress = MainC.GetIpAddress(tcpClient);

                logger.Info($"A player from ip {ipAddress} connected...");

                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();

                // starts RSA handshake that will return the AES and RSA encryption keys
                byte[] aesKey = new byte[] { };
                if (EncryptionAes.encryptionEnabled)
                {
                    aesKey = await ExchangeSymmetricKey(tcpClient, ipAddress);
                    ByteProcessor.PrintByteArrayAsHex(aesKey);
                }

                // starts the authentication that will return player instance on success
                Player player = await StartAuthentication(tcpClient, aesKey, ipAddress);

                if (player != null)
                {
                    logger.Debug($"Sending data of player {player.playerName} to everyone except the new player...");
                    PlayerData playerData = MainC.GetDataOfPlayer(player);
                    MainC.SendToEveryoneExcept(20, playerData, player);
                    
                    logger.Debug($"Sending every player's data to the new player {player.playerName}...");
                    PlayerData[] playerDataArray = MainC.GetDataOfEveryPlayers();
                    MainC.SendToOnePlayer(21, playerDataArray, player);
                    
                    ReceiveTcpPacket receiveTcpPacket = new ReceiveTcpPacket(player);
                    Task.Run(async () => await receiveTcpPacket.run());

                }
                else
                {
                    logger.Info($"Authentication of {ipAddress} was failure");
                }
                stopwatch.Stop();
                long elapsedMilliseconds = stopwatch.ElapsedMilliseconds;
                logger.Debug($"Finished authentication in: {elapsedMilliseconds} ms");
            }
            catch (Exception e)
            {
                logger.Error($"Exception during authentication of {e.ToString}, aborting... ");
            }
        }
    }

    private async Task<byte[]> ExchangeSymmetricKey(TcpClient tcpClient, string ipAddress)
    {
        // Processing handshake request sent by the connecting player
        logger.Debug($"Waiting for {ipAddress} to send a handshake request...");

        byte[] aloReceivedBytes = await ReceiveTcpPacket.ReceiveBytes(tcpClient.GetStream());
        aloReceivedBytes = EncryptionAes.Decrypt(aloReceivedBytes, defaultKey);

        if (!aloReceivedBytes.SequenceEqual(Encoding.UTF8.GetBytes("alo")))
        {
            logger.Error($"Improper handshake request received from {ipAddress}, aborting handshake");
            MainC.DisconnectPlayer(tcpClient);
            return null;
        }

        logger.Debug($"Handshake request received from {ipAddress}, sending public key to the player...");

        // sending public key to player
        MainC.SendTcp(EncryptionRsa.PublicKeyToByteArray(), tcpClient);

        // waiting for player to send its own public key
        logger.Debug($"Waiting now for {ipAddress} to send it's own public key...");

        byte[] encryptedKeys = await ReceiveTcpPacket.ReceiveBytes(tcpClient.GetStream());

        // separate
        logger.Debug("Separating encrypted client rsa public key...");
        byte[] encryptedClientPublicKey = new byte[576];
        Array.Copy(encryptedKeys, encryptedClientPublicKey, 576);

        logger.Debug("Separating encrypted client aes key...");
        byte[] encryptedClientAesKey = new byte[512];
        Array.ConstrainedCopy(encryptedKeys, 576, encryptedClientAesKey, 0, 512);

        byte[] decryptedAesKey = EncryptionRsa.Decrypt(encryptedClientAesKey);
        byte[] decryptedClientPublicKey = EncryptionAes.Decrypt(encryptedClientPublicKey, decryptedAesKey);


        // decrypting the public key using local private key
        using RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();
        rsa.ImportSubjectPublicKeyInfo(decryptedClientPublicKey, out _);
        RSAParameters clientPublicKey = rsa.ExportParameters(false);


        // sending a unique AES key to the player encrypted using the player's public key
        logger.Debug($"Sending a random AES key to {ipAddress}");
        byte[] aesKey = EncryptionAes.GenerateRandomKey(16);
        byte[] encryptedAesKey = EncryptionRsa.Encrypt(aesKey, clientPublicKey);
        MainC.SendTcp(encryptedAesKey, tcpClient);

        // testing if it works
        //        logger.debug("Sending test...");
        //        byte[] encryptedBytes = EncryptionAES.Encrypt("test from server", aesKey);
        //        server.SendTcp(encryptedBytes, tcpClientSocket);
        //
        //        logger.trace("Waiting for response...");
        //        byte[] testReceivedBytes = ReceiveTcpPacket.ReceiveBytes(tcpClientSocket);
        //
        //        logger.debug("Decrypting response...");
        //        String decodedMessage = EncryptionAES.DecryptString(testReceivedBytes, aesKey);
        //
        //        logger.debug("Checking if test was successful...");
        //        if (!decodedMessage.equals("test from client")) {
        //            logger.error("Test failed, string doesn't match, aborting handshake");
        //            server.DisconnectPlayer(tcpClientSocket);
        //            return null;
        //        }

        // success
        logger.Debug($"RSA handshake was successful with {ipAddress}");

        return aesKey;
    }

    private async Task<Player> StartAuthentication(TcpClient tcpClient, byte[] aesKey, string ipAddress)
    {
        logger.Debug($"Started authentication for {ipAddress}...");

        Player newPlayer = new Player();

        newPlayer.tcpClient = tcpClient;
        newPlayer.aesKey = aesKey;

        // find which player slot is free
        logger.Debug($"Searching a free slot for {ipAddress}...");
        newPlayer.index = -1;
        for (int i = 1; i < MainC.players.Length; i++)
        {
            if (MainC.players[i] == null)
            {
                newPlayer.index = i;
                logger.Debug($"Found free slot at slot {newPlayer.index}");
                break;
            }
        }

        // runs if server is full
        if (newPlayer.index == -1)
        {
            logger.Debug($"Server is full, rejecting {ipAddress}");
            SendNegativeResponseAndDisconnect(tcpClient, 7, newPlayer.aesKey, ipAddress);
            return null;
        }

        // Reading LoginData sent by the player
        logger.Debug($"Reading LoginData sent by {ipAddress}");
        byte[] receivedBytes = await ReceiveTcpPacket.ReceiveBytes(tcpClient.GetStream());

        // read and process the LoginData sent by the player
        logger.Debug($"Reading LoginData sent by {ipAddress}");
        List<Packet> packets = PacketProcessor.ProcessReceivedBytes(receivedBytes, newPlayer);

        LoginData loginData = null;
        foreach (Packet packet in packets) 
        { 
            if (packet.type == 1)
            {
                try
                {
                    loginData = JsonSerializer.Deserialize(packet.json, LoginDataContext.Default.LoginData);
                    logger.Debug($"reg: {loginData.reg}");
                    logger.Debug($"reg: {loginData.un}");
                    logger.Debug($"reg: {loginData.pw}");
                    logger.Debug($"Player connecting from {ipAddress} identifies as {loginData.un}");
                } 
                catch (Exception e) 
                {
                    logger.Error($"Error processing data received from {ipAddress}: {e.ToString()}");
                    MainC.DisconnectPlayer(tcpClient);
                    return null;
                }
                break;
            }
        }


        // runs if player wants to register first
        if (loginData.reg)
        {
            logger.Debug($"Registering player {loginData.un}...");

            // Checks if chosen name is longer than 16 or shorter than 2 characters
            logger.Debug($"Checking chosen name's length for player {loginData.un}...");
            if (loginData.un.Length < 2 || loginData.un.Length > 16)
            {
                logger.Debug($"Player has {loginData.un} chosen too long or too short name, registration failed");
                SendNegativeResponseAndDisconnect(tcpClient, 5, newPlayer.aesKey, ipAddress);
                return null;
            }

            // Checks if the chosen name is already registered
            logger.Debug($"Checking if chosen name {loginData.un} is already registered...");
            DatabasePlayer regDatabasePlayer = MainC.database.SearchForPlayerInDatabase(loginData.un);

            if (regDatabasePlayer != null)
            {
                logger.Debug($"Player name {loginData.un} already exists in the database, registration failed");
                SendNegativeResponseAndDisconnect(tcpClient, 6, newPlayer.aesKey, ipAddress);
                return null;
            }
           
            // Adds the new player to the database
            logger.Debug($"Successful registration, adding new player {loginData.un} to the database");
            MainC.database.RegisterPlayer(loginData.un, loginData.pw);
        }

        // logs the player in
        logger.Debug($"Logging in player {loginData.un}...");

        // searches if player is already connected to the server
        logger.Debug($"Checking if player {loginData.un} is connected already...");
        foreach (Player player in MainC.players)
        {
            if (player == null)
                continue;
            if (loginData.un.Equals(player.playerName))
            {
                logger.Debug($"Player {loginData.un} is already connected, login failed");
                SendNegativeResponseAndDisconnect(tcpClient, 4, newPlayer.aesKey, ipAddress);
                return null;
            }
        }

        // searches for the player in the database
        logger.Debug($"Searching for player {loginData.un} in the database...");
        DatabasePlayer logDatabasePlayer = MainC.database.SearchForPlayerInDatabase(loginData.un);

        // if player was not found in database
        if (logDatabasePlayer == null)
        {
            logger.Debug($"Player {loginData.un} not found in database, login failed.");
            SendNegativeResponseAndDisconnect(tcpClient, 3, newPlayer.aesKey, ipAddress);
            return null;
        }

        // reads password from database then compares
        logger.Debug($"Comparing entered password for player {loginData.un}...");
        if (!loginData.pw.Equals(logDatabasePlayer.password))
        {
            logger.Debug($"Player {loginData.un} has entered wrong password, login failed");
            SendNegativeResponseAndDisconnect(tcpClient, 2, newPlayer.aesKey, ipAddress);
            return null;
        }

        // login was a success
        logger.Debug($"Successful login for player {loginData.un}");
        newPlayer.databaseID = logDatabasePlayer.id;
        newPlayer.playerName = logDatabasePlayer.name;

        // creating InitialData object
        logger.Debug($"Creating InitialData object to be sent to {loginData.un}");
        InitialData initialData = new InitialData();
        initialData.loginResultValue = 1;
        initialData.index = newPlayer.index;
        initialData.maxPlayers = MainC.maxPlayers;
        initialData.tickRate = MainC.tickRate;

        // reply back to the player about the authentication success
        logger.Debug($"Sending positive reply about authentication back to {newPlayer.playerName}...");
        try
        {
            byte[] bytesToSend = PacketProcessor.MakePacketForSending(1, initialData, newPlayer.aesKey);
            MainC.SendTcp(bytesToSend, tcpClient);
        }
        catch (Exception e)
        {
            logger.Error(e.ToString());
            MainC.DisconnectPlayer(tcpClient);
            return null;
        }

        // sets last login ip address
        //server.database.UpdateLastLoginIpAddress(connectedPlayer);

        // adds the new player to the list of connected players
        logger.Debug($"Adding player {newPlayer.playerName} to the list of connected players...");
        MainC.players[newPlayer.index] = newPlayer;

        // returns success
        logger.Info($"Authentication of {ipAddress} ({newPlayer.playerName}) was success");
        newPlayer.status = 1;
        return newPlayer;
    }

    private void SendNegativeResponseAndDisconnect(TcpClient tcpClient, int resultValue, byte[] aesKey, string ipAddress)
    {
        try
        {
            logger.Debug($"Sending negative initial data with value {resultValue} to the failed player...");

            InitialData initialData = new InitialData
            {
                loginResultValue = resultValue
            };

            byte[] bytesToSend = PacketProcessor.MakePacketForSending(1, initialData, aesKey);
            MainC.SendTcp(bytesToSend, tcpClient);

            logger.Debug("Closing connection of the failed player in 1 second...");
            Thread.Sleep(1000);
            MainC.DisconnectPlayer(tcpClient);

            logger.Debug($"Connection with {ipAddress} has been terminated successfully");
        }
        catch (Exception e)
        {
            logger.Error(e.ToString());
        }
    }
}