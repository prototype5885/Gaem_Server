using System.Collections.Concurrent;
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

    public ConnectedPlayer[] connectedPlayers;
    public Database database;

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
        connectedPlayers = new ConnectedPlayer[maxPlayers];

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

        while (true)
        {
            await ProcessPacketsSentByPlayers();
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
    
    public async Task SendDataOfConnectedPlayers() {
        // making a list
        logger.Debug("Making list of each player's data for sending to everyone...");
        List<PlayerData> playerDataList = new List<PlayerData>();
        foreach (ConnectedPlayer player in connectedPlayers) {
            if (player == null) continue;

            PlayerData playerData = new PlayerData();
            playerData.i = player.index;
            playerData.un = player.playerName;

            playerDataList.Add(playerData);
        }

        // sending it to each player
        foreach (ConnectedPlayer player in connectedPlayers) {
            if (player != null) {
                logger.Debug($"Sending list of each player's data to: {player.playerName}");
                try {
                    byte[] bytesToSend = PacketProcessor.MakePacketForSending(3, playerDataList, player.aesKey);
                    await SendTcp(bytesToSend, player.tcpClient.GetStream());
                } catch (Exception e) {
                    logger.Error(e.ToString());
                }
            }
        }
    }

    public async Task SendTcp(byte[] bytesToSend, NetworkStream networkStream)
    {
        try
        {
            await networkStream.WriteAsync(bytesToSend);
        }
        catch (Exception e)
        {
            logger.Error(e.ToString());
        }
    }
    public void DisconnectPlayer(TcpClient tcpClient)
    {
        string ipAddress = GetIpAddress(tcpClient);

        logger.Info($"Disconnecting {ipAddress}...");
        try
        {
            tcpClient.Close();
            logger.Debug($"Closed connection for {ipAddress}");
        }
        catch (IOException e)
        {
            logger.Error($"Error closing connection for {ipAddress}: {e.ToString}");
        }

        logger.Debug($"Searching for {ipAddress} in the player list to remove using tcp socket...");
        for (int i = 0; i < maxPlayers; i++)
        {
            if (connectedPlayers[i] != null && connectedPlayers[i].tcpClient.Equals(tcpClient))
            {
                ConnectedPlayer playerToDisconnect = connectedPlayers[i];
                logger.Debug($"Found {playerToDisconnect.playerName} in the player list, removing...");
                connectedPlayers[i] = null;
                logger.Debug($"Removed player from the player list, slot status: {connectedPlayers[i]}");
                return;
            }
        }
        logger.Debug("Player not present in the player list was disconnected successfully");
    }
    public string GetIpAddress(TcpClient tcpClient)
    {
        IPEndPoint endPoint = (IPEndPoint)tcpClient.Client.RemoteEndPoint;
        return endPoint.Address.ToString();
    }

    private async Task ProcessPacketsSentByPlayers()
    {
        while (!packetsToProcess.IsEmpty)
        {
            if (packetsToProcess.TryDequeue(out Packet packet))
            {
                switch (packet.type)
                {
                    case 4:
                        logger.Debug($"Received a chat message from {packet.owner.playerName}, message: {packet.json}");
                        await SendChatMessageToEveryone(packet.owner, packet.json);
                        break;
                        //            case 3:
                        //                UpdatePlayerPosition(connectedPlayer, packet.data);
                        //                break;
                }
            }
        }
    }

    private async Task SendChatMessageToEveryone(ConnectedPlayer msgSender, String chatMessageJson)
    {
        try
        {
            logger.Debug($"Making ChatMessage object and then packet for the message {msgSender.playerName} sent");
            // prepares object
            ClassesShared.ChatMessage chatMessage = JsonSerializer.Deserialize(chatMessageJson, ChatMessageContext.Default.ChatMessage);
            chatMessage.i = msgSender.index;

            // send the message to each connected player
            logger.Debug($"Sending chat message from {msgSender.playerName} to all players...");
            foreach (ConnectedPlayer player in connectedPlayers)
            {
                if (player != null)
                {
                    byte[] bytesToSend = PacketProcessor.MakePacketForSending(4, chatMessage, player.aesKey);
                    await SendTcp(bytesToSend, player.tcpClient.GetStream());
                }
            }
        } 
        catch (Exception e)
        {
            logger.Error(e.ToString());
        }
    }
}
