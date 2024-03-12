using System.Net.Sockets;
using System.Net;
using System.Text.Json;

public static class Authentication
{
    public static async Task WaitForPlayerToConnect()
    {
        while (true)
        {
            try
            {
                Console.WriteLine($"({DateTime.Now}) waiting for a player to connect...");
                TcpClient tcpClient = Server.tcpListener.AcceptTcpClient(); // waits for a new client to join
                NetworkStream tcpStream = tcpClient.GetStream();
                Console.WriteLine($"({DateTime.Now}) New player is connecting...");

                byte freeSlotIndex = 0;
                foreach (ConnectedPlayer connectedPlayer in Server.connectedPlayers)
                {
                    if (connectedPlayer == null)
                    {
                        Console.WriteLine($"({DateTime.Now}) Assigned slot id {freeSlotIndex} to connecting player");
                        await AuthenticateConnectingPlayer(tcpClient, freeSlotIndex, tcpStream);
                        break;
                    }
                    else
                    {
                        freeSlotIndex++;
                    }
                }

                if (freeSlotIndex > Server.maxPlayers - 1)
                {
                    Console.WriteLine($"({DateTime.Now}) Server is full, rejecting connection");

                    AuthenticationResult authenticationResult = new AuthenticationResult { result = 7 };
                    await ConnectionRejected(tcpClient, authenticationResult);
                }
            }
            catch
            {
                Console.WriteLine($"({DateTime.Now}) Error while accepting new client");
            }
        }
    }

    private static async Task AuthenticateConnectingPlayer(TcpClient tcpClient, byte freeSlotIndex, NetworkStream tcpStream)
    {
        Console.WriteLine($"({DateTime.Now}) New player has connected, waiting for login data...");

        byte[] buffer = new byte[512];
        int bytesRead = tcpStream.Read(new ArraySegment<byte>(buffer));
        Console.WriteLine($"bytes read: {bytesRead}");
        Packet[] packets = PacketProcessor.ProcessBuffer(buffer, bytesRead);
        Monitoring.receivedBytesPerSecond += bytesRead;

        foreach (Packet packet in packets)
        {
            if (packet.type == 1) // makes sure its really a login data packet
            {
                LoginData loginData = JsonSerializer.Deserialize<LoginData>(packet.data);

                bool loginOrRegister = loginData.lr; // True if client wants to login, false if client wants to register register
                string username = loginData.un;
                string hashedPassword = loginData.pw;

                AuthenticationResult authenticationResult = new AuthenticationResult(); // creates the thing that holds value for login/register result, db index, playername
                Console.WriteLine($"({DateTime.Now}) Login data has arrived from player ({username})");

                if (loginOrRegister) // Runs if client wants to login
                {
                    authenticationResult = Database.LoginUser(username, hashedPassword, Server.connectedPlayers);
                }
                else // Runs if client wants to register
                {
                    authenticationResult = Database.RegisterUser(username, hashedPassword);
                    if (authenticationResult.result == 1) // runs if registration was successful
                    {
                        authenticationResult = Database.LoginUser(username, hashedPassword, Server.connectedPlayers);
                    }
                }

                if (authenticationResult.result == 1)
                {
                    IPEndPoint clientAddress = ((IPEndPoint)tcpClient.Client.RemoteEndPoint); // Gets the IP address of the new client

                    Server.connectedPlayers[freeSlotIndex] = new ConnectedPlayer
                    {
                        index = freeSlotIndex,
                        databaseID = authenticationResult.dbIndex,
                        tcpClient = tcpClient,
                        tcpStream = tcpClient.GetStream(),
                        tcpEndpoint = clientAddress,
                        ipAddress = clientAddress.Address,
                        tcpPort = clientAddress.Port,
                        cancellationTokenSource = new CancellationTokenSource(),
                        playerName = authenticationResult.playerName
                    };

                    InitialData initialData = new InitialData
                    {
                        lr = authenticationResult.result, // value represents how the server responded to login, like if success or not
                        i = freeSlotIndex, // client's assigned id
                        mp = Server.maxPlayers, // max player amount
                        tr = Server.tickRate,
                        pda = PlayersManager.GetDataOfEveryConnectedPlayer()
                    };
                    string jsonData = JsonSerializer.Serialize(initialData);
                    await PacketProcessor.SendTcp(1, jsonData, Server.connectedPlayers[freeSlotIndex]); // Type 1 means servers sends initial data to the new client
                    Console.WriteLine($"({DateTime.Now}) Initial data has been sent to player ({username})");

                    await PlayersManager.SendPlayerDataToEveryone();

                    _ = PacketProcessor.ReceiveTcpData(Server.connectedPlayers[freeSlotIndex]);
                    break;
                }
                else
                {
                    await ConnectionRejected(tcpClient, authenticationResult);
                    break;
                }
            }
        }
    }

    private static async Task ConnectionRejected(TcpClient tcpClient, AuthenticationResult authenticationResult)
    {
        ConnectedPlayer connectedPlayer = new ConnectedPlayer
        {
            tcpStream = tcpClient.GetStream()
        };

        InitialData initialData = new InitialData
        {
            lr = authenticationResult.result, // value represents how the server responded to login, like if success or not
            i = 0, // client's assigned id
            mp = 0, // max player amount
            tr = 0
        };

        string jsonData = JsonSerializer.Serialize(initialData);
        await PacketProcessor.SendTcp(1, jsonData, connectedPlayer); // Type 1 means servers sends initial data to the new client
        Thread.Sleep(1000); // need to wait before closing else the player wont receive the "authentication failed" message
        tcpClient.GetStream().Close();
    }

    public static ConnectedPlayer CheckAuthenticationOfUdpClient(EndPoint udpEndpoint) // checks if udp package sender is an actual player
    {
        try
        {
            IPEndPoint udpIpEndpoint = udpEndpoint as IPEndPoint;
            if (udpIpEndpoint == null) return null;

            foreach (ConnectedPlayer player in Server.connectedPlayers)
            {
                if (player == null) continue;
                if (player.ipAddress.Equals(udpIpEndpoint.Address) && player.udpEndpoint == null) // runs if ip address is in array but udp socket is not yet
                {
                    if (player.udpPort != 0)
                        return player; // if authenticated but its the first package, get its port and assign udp endpoint to authenticated player
                    player.udpPort = udpIpEndpoint.Port;
                    player.udpEndpoint = udpIpEndpoint;
                    return player;
                }
                else if (player.udpEndpoint != null && udpEndpoint.Equals(player.udpEndpoint)) // runs if ip address and udp socket is inside the array
                {
                    return player;
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    public static void DisconnectClient(ConnectedPlayer connectedPlayer)
    {
        connectedPlayer.tcpClient.Close(); // Closes TCP connection of client
        connectedPlayer.cancellationTokenSource.Cancel(); // Cancels receiving task from client

        Console.WriteLine($"Player {connectedPlayer.playerName} was disconnected");

        int index = Array.IndexOf(Server.connectedPlayers, connectedPlayer);
        Server.connectedPlayers[index] = null; // Remove the player

        Console.WriteLine(Monitoring.GetCurrentPlayerCount());
    }
}