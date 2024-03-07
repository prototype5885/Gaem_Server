using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
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
                Console.WriteLine($"({DateTime.Now}) New player is connecting...");

                byte freeSlotIndex = 0;
                foreach (ConnectedPlayer connectedPlayer in Server.connectedPlayers)
                {
                    if (connectedPlayer == null)
                    {
                        Console.WriteLine($"({DateTime.Now}) Assigned slot id {freeSlotIndex} to connecting player");
                        await AuthenticateConnectingPlayer(tcpClient, freeSlotIndex);
                        break;
                    }
                    else
                    {
                        freeSlotIndex++;
                    }
                }
                if (freeSlotIndex > Server.maxPlayers)
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
    static async Task AuthenticateConnectingPlayer(TcpClient tcpClient, byte freeSlotIndex)
    {
        NetworkStream stream = tcpClient.GetStream(); // gets the stream after a new client is connected
        Console.WriteLine($"({DateTime.Now}) New player has connected, waiting for login data...");

        Byte[] buffer = new Byte[1024];
        int bytesRead = stream.Read(new ArraySegment<byte>(buffer)); // reads the bytes the client sent
        Packet[] packets = PacketProcessor.ProcessBuffer(buffer, bytesRead); // Processes the received packet
        Monitoring.receivedBytesPerSecond += bytesRead;

        // Packet packet = packets[0]; // newly connected client is supposed to only send a single login data packet, so assuming there is only 1 packet received
        foreach (Packet packet in packets)
        {
            if (packet.type == 1) // makes sure its really a login data packet
            {

                IPEndPoint clientAddress = ((IPEndPoint)tcpClient.Client.RemoteEndPoint); // Gets the IP address of the new client

                LoginData loginData = JsonSerializer.Deserialize(packet.data, LoginDataContext.Default.LoginData);

                bool loginOrRegister = loginData.loginOrRegister; // True if client wants to login, false if client wants to register register
                string username = loginData.username;
                string hashedPassword = loginData.password;

                AuthenticationResult authenticationResult = new AuthenticationResult(); // creates the thing that holds value for login/register result, db index, playername
                Console.WriteLine($"({DateTime.Now}) Login data has arrived from player ({username})");

                if (loginOrRegister == false) // Runs if client wants to register
                {
                    authenticationResult = Database.RegisterUser(username, hashedPassword);
                    if (authenticationResult.result == 1) // runs if registration was successful
                    {
                        authenticationResult = Database.LoginUser(username, hashedPassword, Server.connectedPlayers);
                    }
                }
                else if (loginOrRegister == true) // Runs if client wants to login
                {
                    authenticationResult = Database.LoginUser(username, hashedPassword, Server.connectedPlayers);
                }

                if (authenticationResult.result == 1)
                {

                    Server.connectedPlayers[freeSlotIndex] = new ConnectedPlayer
                    {
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
                        tr = Server.tickrate
                    };
                    // Thread.Sleep(500);
                    string jsonData = JsonSerializer.Serialize(initialData, InitialDataContext.Default.InitialData);
                    await PacketProcessor.SendTcp(1, jsonData, Server.connectedPlayers[freeSlotIndex]); // Type 1 means servers sends initial data to the new client
                    Console.WriteLine($"({DateTime.Now}) Initial data has been sent to player ({username})");

                    _ = PacketProcessor.ReceiveTcpData(Server.connectedPlayers[freeSlotIndex]);
                    break;
                }
                else
                {
                    await ConnectionRejected(tcpClient, authenticationResult);
                }
            }
        }
        Console.WriteLine(Monitoring.GetCurrentPlayerCount());
    }
    static async Task ConnectionRejected(TcpClient tcpClient, AuthenticationResult authenticationResult)
    {
        NetworkStream stream = tcpClient.GetStream();
        ConnectedPlayer connectedPlayer = new ConnectedPlayer
        {
            tcpStream = stream
        };

        InitialData initialData = new InitialData
        {
            lr = authenticationResult.result, // value represents how the server responded to login, like if success or not
            i = 0, // client's assigned id
            mp = 0, // max player amount
            tr = 0
        };

        string jsonData = JsonSerializer.Serialize(initialData, InitialDataContext.Default.InitialData);
        await PacketProcessor.SendTcp(1, jsonData, connectedPlayer); // Type 1 means servers sends initial data to the new client
        Thread.Sleep(1000); // need to wait before closing else the player wont receive the "authentication failed" message
        stream.Close();
    }
    public static ConnectedPlayer CheckAuthenticationOfUdpClient(EndPoint udpEndpoint) // checks if udp package sender is an actual player
    {
        try
        {
            Console.WriteLine("Trying to authenticate udp packet");
            IPEndPoint udpIpEndpoint = udpEndpoint as IPEndPoint;
            System.Console.WriteLine($"udpIpEndpoint: {udpIpEndpoint}");
            if (udpIpEndpoint == null) return null;

            foreach (ConnectedPlayer player in Server.connectedPlayers)
            {
                if (player == null) continue;

                // Console.WriteLine(player.udpEndpoint + " and " + udpEndpoint);

                if (player.ipAddress.Equals(udpIpEndpoint.Address) && player.udpEndpoint == null) // checks if udp packet sender is authenticated player
                {
                    if (player.udpPort == 0) // if authenticated but its the first package, get its port and assign udp endpoint to authenticated player
                    {
                        player.udpPort = udpIpEndpoint.Port;
                        player.udpEndpoint = udpEndpoint;
                    }
                    return player;
                }
                else if (player.udpEndpoint != null && udpEndpoint.Equals(player.udpEndpoint))
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

