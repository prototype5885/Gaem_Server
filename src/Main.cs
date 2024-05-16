using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Gaem_server.Classes;
using Gaem_server.ClassesShared;
using Gaem_server.Instanceables;
using Gaem_server.src.ClassesShared;
using Gaem_server.Static;
using Gaem_server.Threaded;
using log4net;
using ChatMessageContext = Gaem_server.ClassesShared.ChatMessageContext;

[assembly: log4net.Config.XmlConfigurator(ConfigFile = "log4net.config")]

namespace Gaem_server;

public static class MainC
{
    
    private static readonly ILog logger = LogManager.GetLogger(typeof(MainC));

    public static readonly int maxPlayers;
    public static readonly int tickRate;
    private static readonly int tcpPort;
    public static readonly TcpListener tcpListener;

    public static readonly Player[] players;
    public static readonly Database database;

    public static readonly ConcurrentQueue<Packet> packetsToProcess = new ConcurrentQueue<Packet>();

    static MainC()
    {
        // reads and sets up stuff from config file
        ConfigFile configFile = new ConfigFile();
        maxPlayers = configFile.MaxPlayers;
        tickRate = configFile.TickRate;
        tcpPort = configFile.TcpPort;
        
        // creates array that store information about players and empty slots
        players = new Player[maxPlayers];
        
        // Starts TCP server
        tcpListener = new TcpListener(IPAddress.Any, tcpPort);
        tcpListener.Start();
        
        // instantiates database
        database = new Database();
        database.ConnectToDatabase(configFile);
    }
    
    public static async Task Main()
    {
        EncryptionRsa.Initialize();
        
        HandleNewPlayers handleNewPlayers = new HandleNewPlayers();
        Task.Run(() => handleNewPlayers.run());

        // server loop
        while (true) {
            // starts the loop
            long startTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            while (!packetsToProcess.IsEmpty)
            {
                if (packetsToProcess.TryDequeue(out Packet packet))
                {
                    try
                    {
                        switch (packet.type)
                        {
                            // chat message
                            case 30:
                                logger.Debug($"Received then forwarding a chat message from {packet.owner.playerName}, message: {packet.json}");
                            
                                ChatMessage chatMessage = JsonSerializer.Deserialize(packet.json, ChatMessageContext.Default.ChatMessage);
                                chatMessage.i = packet.owner.index;

                                // send the message to every connected player
                                SendToEveryone(30, chatMessage);
                            
                                break;
                            // player position
                            case 40:
                                logger.Debug($"Received then forwarding position data from {packet.owner.playerName}, message: {packet.json}");

                                packet.owner.position = JsonSerializer.Deserialize(packet.json, PlayerPositionContext.Default.PlayerPosition);
                                break;
                        }
                    }
                    catch (Exception e)
                    {
                        logger.Error(e);
                    }
                }
            }
            
            // sends the positions of each player to everyone
            PlayerPositionWithID[] playerPositions = new PlayerPositionWithID[maxPlayers];
            for (int i = 0; i < maxPlayers; i++) {
                PlayerPositionWithID playerPosition = new PlayerPositionWithID();
                playerPosition.i = i;
                playerPositions[i] = playerPosition;
            }
            SendToEveryone(40, playerPositions);

            // ends the loop
            long endTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            int elapsedTime = Convert.ToInt32(endTime - startTime);
            int sleepTime = 99 - elapsedTime;


            if (sleepTime < 0) {
                logger.Debug($"Skipped sleep time, sleepTime: {sleepTime}");
                continue;
            }
            Thread.Sleep(sleepTime);
            // logger.Debug($"Tick finished calculations in: {elapsedTime} ms, then slept for: {DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - endTime} ms");
        }
        

        // if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        // {
        //     // Due to this issue: https://stackoverflow.com/questions/74327225/why-does-sending-via-a-udpclient-cause-subsequent-receiving-to-fail
        //     // .. the following needs to be done on windows
        //     const uint IOC_IN = 0x80000000U;
        //     const uint IOC_VENDOR = 0x18000000U;
        //     const int SIO_UDP_CONNRESET = unchecked((int)(IOC_IN | IOC_VENDOR | 12));
        //     serverUdpSocket.IOControl(SIO_UDP_CONNRESET, new byte[] { 0x00 }, null);
        // }

        // connectedPlayers = new ConnectedPlayer[maxPlayers];
        // Encryption.GetEncryptionKey();

        // Database.Initialize();

        // Task.Run(() => PacketProcessor.ReceiveUdpData());
        // Task.Run(() => PlayersManager.ReplicatePlayerPositions());
        // // Task.Run(() => Monitoring.RunEverySecond());
        // Task.Run(() => Authentication.WaitForPlayerToConnect());
        // Thread.Sleep(Timeout.Infinite);
    }
    
    public static PlayerData GetDataOfPlayer(Player player) {
        // status means if player is connecting or disconnecting
        PlayerData playerData = new PlayerData();
        playerData.i = player.index;
        playerData.s = player.status;
        playerData.un = player.playerName;

        return playerData;
    }
    
    public static PlayerData[] GetDataOfEveryPlayers() {
        PlayerData[] playerDataArray = new PlayerData[maxPlayers];
        for (int i = 0; i < maxPlayers; i++) {
            if (players[i] == null) continue;

            PlayerData playerData = new PlayerData
            {
                i = players[i].index,
                s = players[i].status,
                un = players[i].playerName
            };

            playerDataArray[i] = playerData;
        }
        return playerDataArray;
    }

    private static void UpdatePlayerPosition()
    {
        
    }
    
    public static void DisconnectPlayer(TcpClient tcpClient)
    {
        string ipAddress = GetIpAddress(tcpClient);

        logger.Info($"Disconnecting {ipAddress}...");
        try
        {
            tcpClient.Close();
            // logger.Debug($"Closed connection for {ipAddress}");
        }
        catch (IOException e)
        {
            logger.Error($"Error closing connection for {ipAddress}: {e.ToString}");
        }

        logger.Debug($"Searching for {ipAddress} in the player array to remove...");
        for (int i = 0; i < maxPlayers; i++)
        {
            if (players[i] != null && players[i].tcpClient.Equals(tcpClient))
            {
                logger.Debug($"Found {players[i].playerName} in the player list, removing...");
                players[i] = null;
                // logger.Debug($"Removed player from the player list, slot status: {players[i]}");
                
                logger.Debug("Sending the disconnection info to each player...");
                PlayerData playerData = new PlayerData
                {
                    i = i,
                    s = 0
                };

                SendToEveryone(20, playerData);
            }
        }
    }
    
    private static int GetConnectedPlayersCount() {
        int playerCount = 0;
        foreach (Player player in players) {
            if (player != null) {
                playerCount++;
            }
        }
        return playerCount;
    }
    
    public static void SendToOnePlayer(int type, Object obj, Player player) {
        try {
            logger.Debug($"Sending message type {type} to: {player.playerName}");
            byte[] bytesToSend = PacketProcessor.MakePacketForSending(type, obj, player.aesKey);
            SendTcp(bytesToSend, player.tcpClient);
        } catch (Exception e) {
            logger.Error(e);
        }
    }
    
    public static void SendToEveryoneExcept(int type, Object obj, Player playerToSkip) {
        foreach (Player player in players) {
            if (player == null || player == playerToSkip) continue;
            try {
                SendToOnePlayer(type, obj, player);
            } catch (Exception e) {
                logger.Error(e);
            }
        }
    }

    public static void SendToEveryone(int type, Object obj) {
        foreach (Player player in players) {
            if (player == null) continue;
            try {
                SendToOnePlayer(type, obj, player);
            } catch (Exception e) {
                logger.Error(e);
            }
        }
    }

    public static void SendTcp(byte[] bytesToSend, TcpClient tcpClient)
    {
        try
        {
            tcpClient.GetStream().Write(bytesToSend);
        }
        catch (Exception e)
        {
            logger.Error(e.ToString());
        }
    }
    
    public static string GetIpAddress(TcpClient tcpClient)
    {
        IPEndPoint endPoint = (IPEndPoint)tcpClient.Client.RemoteEndPoint;
        return endPoint.Address.ToString();
    }
    
}
