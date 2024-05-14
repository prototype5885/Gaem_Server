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

namespace Gaem_server;

public class Server
{
    private static readonly ILog logger = LogManager.GetLogger(typeof(Server));

    public readonly int maxPlayers;
    public readonly int tickRate;
    private readonly int tcpPort;
    public readonly TcpListener tcpListener;

    public readonly Player[] players;
    public readonly Database database;

    public readonly ConcurrentQueue<Packet> packetsToProcess = new ConcurrentQueue<Packet>();

    public Server()
    {
        EncryptionRsa.Initialize();

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
    
    public async Task StartServer()
    {

        HandleNewPlayers handleNewPlayers = new HandleNewPlayers(this);
        Task.Run(() => handleNewPlayers.run());

        // server loop
        long startTime;
        long endTime;
        int elapsedTime;
        int sleepTime;
        while (true) {
            // starts the loop
            startTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            ProcessPacketsSentByPlayers();

            // ends the loop
            endTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            elapsedTime = Convert.ToInt32(endTime - startTime);
            sleepTime = 99 - elapsedTime;


            if (sleepTime < 0) {
                logger.Debug($"Skipped sleep time, sleepTime: {sleepTime}");
                continue;
            }
            Thread.Sleep(sleepTime);
            logger.Debug($"Tick finished calculations in: {elapsedTime} ms, then slept for: {DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - endTime} ms");
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
    
    public PlayerData GetDataOfPlayer(Player player) {
        // status means if player is connecting or disconnecting
        PlayerData playerData = new PlayerData();
        playerData.i = player.index;
        playerData.s = player.status;
        playerData.un = player.playerName;

        return playerData;
    }
    
    public PlayerData[] GetDataOfEveryPlayers() {
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

    private void UpdatePlayerPosition()
    {
        
    }
    
    public void DisconnectPlayer(TcpClient tcpClient)
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
    
    private int GetConnectedPlayersCount() {
        int playerCount = 0;
        foreach (Player player in players) {
            if (player != null) {
                playerCount++;
            }
        }
        return playerCount;
    }
    
    public void SendToOnePlayer(int type, Object obj, Player player) {
        try {
            logger.Debug($"Sending message type {type} to: {player.playerName}");
            byte[] bytesToSend = PacketProcessor.MakePacketForSending(type, obj, player.aesKey);
            SendTcp(bytesToSend, player.tcpClient);
        } catch (Exception e) {
            logger.Error(e);
        }
    }
    
    public void SendToEveryoneExcept(int type, Object obj, Player playerToSkip) {
        foreach (Player player in players) {
            if (player == null || player == playerToSkip) continue;
            try {
                SendToOnePlayer(type, obj, player);
            } catch (Exception e) {
                logger.Error(e);
            }
        }
    }

    public void SendToEveryone(int type, Object obj) {
        foreach (Player player in players) {
            if (player == null) continue;
            try {
                SendToOnePlayer(type, obj, player);
            } catch (Exception e) {
                logger.Error(e);
            }
        }
    }

    public void SendTcp(byte[] bytesToSend, TcpClient tcpClient)
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
    
    public string GetIpAddress(TcpClient tcpClient)
    {
        IPEndPoint endPoint = (IPEndPoint)tcpClient.Client.RemoteEndPoint;
        return endPoint.Address.ToString();
    }

    private void ProcessPacketsSentByPlayers()
    {
        while (!packetsToProcess.IsEmpty)
        {
            if (packetsToProcess.TryDequeue(out Packet packet))
            {
                switch (packet.type)
                {
                    case 30:
                        logger.Debug($"Received a chat message from {packet.owner.playerName}, message: {packet.json}");
                        SendChatMessageToEveryone(packet.owner, packet.json);
                        break;
                        //            case 3:
                        //                UpdatePlayerPosition(connectedPlayer, packet.data);
                        //                break;
                }
            }
        }
    }

    private void SendChatMessageToEveryone(Player msgSender, string chatMessageJson)
    {
        try
        {
            logger.Debug($"Making ChatMessage object and then packet for the message {msgSender.playerName} sent");
            // prepares object
            ChatMessage chatMessage = JsonSerializer.Deserialize(chatMessageJson, ChatMessageContext.Default.ChatMessage);
            chatMessage.i = msgSender.index;

            // send the message to each connected player
            SendToEveryone(30, chatMessage);
        } 
        catch (Exception e)
        {
            logger.Error(e.ToString());
        }
    }
}
