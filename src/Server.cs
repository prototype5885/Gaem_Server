using System.Net.Sockets;
using System.Net;
using System.Text;

using System.Text.Json;
using System.Collections;
using System.Security;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System;
using System.Text.RegularExpressions;



public class Server
{
    byte maxPlayers;

    int tcpPort;
    int udpPort;
    readonly Socket serverTcpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
    readonly Socket serverUdpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

    bool[] clientSlotTaken;
    ConnectedPlayer[] connectedPlayers;


    readonly Database database = new Database(); // Creates database object
    readonly AesEncryption aes = new AesEncryption();

    int tickrate = 10;

    public Server(byte maxPlayerss, int port)
    {
        maxPlayers = maxPlayerss;
        tcpPort = port;
        udpPort = port + 1;

        // Starts TCP server
        serverTcpSocket.Bind(new IPEndPoint(IPAddress.Any, tcpPort));
        serverTcpSocket.Listen();

        // Starts UDP server
        serverUdpSocket.Bind(new IPEndPoint(IPAddress.Any, udpPort));

        InitializeValues(maxPlayers);

        Task.Run(() => WaitForNewConnections());
        Task.Run(() => ReceiveUdpData());
        Task.Run(() => ReplicatePlayerPositions());
        Task.Run(() => RunEverySecond());
        Thread.Sleep(Timeout.Infinite);
    }
    void SSL()
    {

    }
    void InitializeValues(int maxPlayers)
    {
        clientSlotTaken = new bool[maxPlayers];
        connectedPlayers = new ConnectedPlayer[maxPlayers];

        for (byte i = 0; i < maxPlayers; i++)
        {
            clientSlotTaken[i] = false;
        }
    }
    async Task WaitForNewConnections()
    {
        Byte[] buffer = new Byte[512];
        int bytesReceived;
        while (true)
        {
            using (Socket newClientSocket = await serverTcpSocket.AcceptAsync())
            {
                bytesReceived = await newClientSocket.ReceiveAsync(new ArraySegment<byte>(buffer), SocketFlags.None);

                //foreach (byte b in buffer)
                //{
                //    Console.Write(b);
                //}

                await ProcessBuffer(buffer, bytesReceived, newClientSocket, null); // Processes the received packet
            }
        }
    }
    async Task ReceiveTcpData(ConnectedPlayer connectedClient)
    {
        try
        {
            CancellationToken cancellationToken = connectedClient.cancellationTokenSource.Token;
            Socket clientTcpSocket = connectedClient.tcpSocket;

            Byte[] buffer = new Byte[1024];
            int bytesReceived;
            while (!cancellationToken.IsCancellationRequested)
            {
                bytesReceived = await clientTcpSocket.ReceiveAsync(new ArraySegment<byte>(buffer), SocketFlags.None, cancellationToken);
                await ProcessBuffer(buffer, bytesReceived, null, connectedClient);
            }
        }
        catch (OperationCanceledException)
        {
            System.Console.WriteLine($"Receiving task for client id {connectedClient.index} was cancelled");
        }
        catch (IOException ex) when (ex.InnerException is SocketException socketEx && socketEx.SocketErrorCode == SocketError.ConnectionReset)
        {
            // Handle sudden client disconnect (ConnectionReset)
            Console.WriteLine($"Client disconnected abruptly: {connectedClient.ipAddress}");
        }
        // catch (Exception e)
        // {
        //     Console.WriteLine("fail");
        // }
    }
    async Task ReceiveUdpData()
    {
        try
        {
            byte[] buffer = new byte[1024];
            while (true)
            {
                EndPoint udpEndPoint = new IPEndPoint(IPAddress.Any, 0);

                int bytesRead = serverUdpSocket.ReceiveFrom(buffer, ref udpEndPoint);

                ConnectedPlayer connectedPlayer = CheckAuthenticationOfUdpClient(udpEndPoint);
                if (connectedPlayer != null)
                {
                    await ProcessBuffer(buffer, bytesRead, null, connectedPlayer);
                }
            }
        }
        catch
        {
            Console.WriteLine("Error receiving UDP packet");
        }
    }
    ConnectedPlayer CheckAuthenticationOfUdpClient(EndPoint udpEndpoint) // checks if udp package sender is an actual player
    {
        IPEndPoint udpIpEndpoint = udpEndpoint as IPEndPoint;
        if (udpIpEndpoint == null) return null;

        foreach (ConnectedPlayer player in connectedPlayers)
        {
            if (player == null) continue;

            if (player.ipAddress.Equals(udpIpEndpoint.Address) && player.udpEndpoint == null) // checks if udp packet sender is authenticated player
            {
                if (player.udpPort == 0) // if authenticated but its the first package, get its port and assign udp endpoint to authenticated player
                {
                    player.udpPort = udpIpEndpoint.Port;
                    player.udpEndpoint = udpEndpoint;
                }
                return player;
            }
            else if (player.udpEndpoint.Equals(udpEndpoint))
            {
                return player;
            }
        }
        return null;
    }
    async Task ReplicatePlayerPositions()
    {
        EveryPlayersPosition everyPlayersPosition = new EveryPlayersPosition(); // this thing is the format the server sends player positions in to each client
        everyPlayersPosition.positions = new PlayerPosition[maxPlayers];

        string jsonData;
        while (true)
        {
            Thread.Sleep(tickrate); // server tick, 100 times a second
            for (byte i = 0; i < maxPlayers; i++) // copies the players' positions so server can send
            {
                if (connectedPlayers[i] == null)
                {
                    if (everyPlayersPosition.positions[i] != null)
                    {
                        everyPlayersPosition.positions[i] = null;
                    }
                    continue;
                }

                everyPlayersPosition.positions[i] = connectedPlayers[i].position;
            }

            for (byte i = 0; i < maxPlayers; i++) // loops through every connected players positions to each
            {
                if (connectedPlayers[i] == null) continue; // skips index if no connected player occupies the slot
                if (connectedPlayers[i].pingAnswered == false) continue;
                jsonData = JsonSerializer.Serialize(everyPlayersPosition, EveryPlayersPositionContext.Default.EveryPlayersPosition);
                await SendUdp(3, jsonData, connectedPlayers[i].udpEndpoint);
            }
        }
    }
    async Task RunEverySecond()
    {
        const byte timeoutTime = 10;

        while (true)
        {
            MonitorValues();
            await PingClients(timeoutTime);
            Thread.Sleep(1000);
        }
    }
    void MonitorValues()
    {
        Console.Clear();
        Console.WriteLine($"TCP port: {tcpPort}, UDP port: {udpPort} | Players: {GetCurrentPlayerCount()}/{maxPlayers}\n");
        for (byte i = 0; i < maxPlayers; i++)
        {
            Console.Write($"{i}: ");
            if (connectedPlayers[i] == null) { Console.WriteLine("Free slot"); continue; }
            Console.WriteLine(connectedPlayers[i]);
        }
    }
    async Task PingClients(byte timeoutTime)
    {
        for (byte i = 0; i < maxPlayers; i++)
        {
            if (connectedPlayers[i] == null) continue;

            if (connectedPlayers[i].pingAnswered == false) // runs if connected client hasn't replied to ping
            {
                connectedPlayers[i].timeUntillTimeout--;
                connectedPlayers[i].status = 0;

                if (connectedPlayers[i].timeUntillTimeout < 1) // runs if client didnt answer during timeout interval
                {
                    DisconnectClient(connectedPlayers[i].index);
                    continue;
                }
            }
            else if (connectedPlayers[i].pingAnswered == true && connectedPlayers[i].timeUntillTimeout != timeoutTime) // runs if connected client answered the ping
            {
                connectedPlayers[i].timeUntillTimeout = timeoutTime;
            }

            connectedPlayers[i].pingAnswered = false; // resets the array

            await SendUdp(0, "", connectedPlayers[i].udpEndpoint);

            connectedPlayers[i].pingRequestTime = DateTime.UtcNow;
        }
    }
    byte GetCurrentPlayerCount()
    {
        byte playerCount = 0;
        for (byte i = 0; i < maxPlayers; i++)
        {
            if (connectedPlayers[i] != null)
            {
                playerCount++;
            }
        }
        return playerCount;
    }
    void CalculatePlayerLatency(byte clientIndex)
    {
        TimeSpan latency = connectedPlayers[clientIndex].pingRequestTime - DateTime.UtcNow;
        connectedPlayers[clientIndex].latency = Math.Abs(latency.Milliseconds) / 2;
    }
    void DisconnectClient(byte clientIndex)
    {
        //Console.WriteLine($"Removed client {connectedPlayers[clientIndex].address} from the server");
        clientSlotTaken[clientIndex] = false; // Free a slot
        database.loggedInIds.Remove(connectedPlayers[clientIndex].ipAddress.ToString());

        connectedPlayers[clientIndex].tcpSocket.Shutdown(SocketShutdown.Both);
        connectedPlayers[clientIndex].tcpSocket.Close(); // Closes TCP socket of client
        connectedPlayers[clientIndex].cancellationTokenSource.Cancel(); // Cancels receiving task from client
        connectedPlayers[clientIndex] = null; // Remove the player
    }
    async Task ProcessBuffer(byte[] buffer, int byteLength, Socket clientTcpSocket, ConnectedPlayer connectedPlayer)
    {
        // try
        {
            string bufferString = Encoding.UTF8.GetString(buffer, 0, byteLength);
            //await Console.Out.WriteLineAsync(bufferString);

            byte[] receivedBytes = new byte[byteLength];
            Array.Copy(buffer, receivedBytes, byteLength);

            //foreach (byte b in receivedBytes)
            //{
            //    Console.WriteLine(b);
            //}

            string receivedBytesInString = aes.Decrypt(receivedBytes);
            //await Console.Out.WriteLineAsync(receivedBytesInString);

            string packetTypePattern = @"#(.*)#";
            string packetDataPattern = @"\$(.*?)\$";

            MatchCollection packetTypeMatches = Regex.Matches(receivedBytesInString, packetTypePattern);
            MatchCollection packetDataMatches = Regex.Matches(receivedBytesInString, packetDataPattern);

            for (byte i = 0; i < packetTypeMatches.Count; i++)
            {
                byte.TryParse(packetTypeMatches[i].Groups[1].Value, out byte typeOfPacket);

                Packet packet = new Packet();
                packet.type = typeOfPacket;
                packet.data = packetDataMatches[i].Groups[1].Value;

                if (packet.type == 1)
                    await ProcessDataSentByNewPlayer(packet, clientTcpSocket);
                else
                    ProcessDataSentByPlayer(packet, connectedPlayer);
            }
        }
        // catch
        // {
        //     System.Console.WriteLine("Error processing buffer");
        // }

    }
    void ProcessDataSentByPlayer(Packet packet, ConnectedPlayer conectedClient)
    {
        // try
        // {
        switch (packet.type)
        {
            // Type 0 means client answers the ping
            case 0:
                conectedClient.pingAnswered = true;
                conectedClient.status = 1;
                CalculatePlayerLatency(conectedClient.index);
                break;
            // Type 3 means client is sending its own position to the server
            case 3:
                PlayerPosition clientPlayerPosition = JsonSerializer.Deserialize(packet.data, PlayerPositionContext.Default.PlayerPosition);
                conectedClient.position = clientPlayerPosition;
                break;
        }
        // }
        // catch
        // {
        //     Console.WriteLine("Packet error");
        // }
    }
    async Task ProcessDataSentByNewPlayer(Packet packet, Socket clientTcpSocket)
    {
        // try
        // {
        IPEndPoint clientAddress = (IPEndPoint)clientTcpSocket.RemoteEndPoint; // Gets the IP address of the new client
        // NetworkStream clientStream = newClient.GetStream();

        LoginData loginData = JsonSerializer.Deserialize(packet.data, LoginDataContext.Default.LoginData);

        bool loginOrRegister = loginData.loginOrRegister; // True if client wants to login, false if client wants to register register
        string username = loginData.username;
        string hashedPassword = loginData.password;

        byte loginResult;
        if (loginOrRegister == true)
            loginResult = database.LoginUser(username, hashedPassword, clientAddress.ToString()); // Runs if client wants to login
        else
            loginResult = database.RegisterUser(username, hashedPassword, clientAddress.ToString()); // Runs if client wants to register

        System.Console.WriteLine(loginResult);
        if (loginResult == 1)
        {
            for (byte index = 0; index < maxPlayers; index++)
            {
                if (clientSlotTaken[index] == false)
                {
                    clientSlotTaken[index] = true; // New client will take found the empty slot 

                    connectedPlayers[index] = new ConnectedPlayer
                    {
                        index = index,
                        databaseID = database.loggedInIds[clientAddress.ToString()],
                        tcpSocket = clientTcpSocket,
                        ipAddress = clientAddress.Address,
                        tcpPort = clientAddress.Port,
                        cancellationTokenSource = new CancellationTokenSource()
                    };
                    connectedPlayers[index].playerName = database.GetUsername(connectedPlayers[index].databaseID);

                    InitialData initialData = new InitialData
                    {
                        lr = loginResult, // value represents how the server responded to login, like if success or not
                        i = index, // client's assigned id
                        mp = maxPlayers // max player amount
                    };

                    string jsonData = JsonSerializer.Serialize(initialData, InitialDataContext.Default.InitialData);
                    await SendTcp(1, jsonData, clientTcpSocket); // Type 1 means servers sends initial data to the new client
                    _ = ReceiveTcpData(connectedPlayers[index]);
                    break;
                }
            }
        }
        else // login failed
        {
            InitialData initialData = new InitialData
            {
                lr = loginResult, // value represents how the server responded to login, like if success or not
                i = -1, // client's assigned id
                mp = -1 // max player amount
            };

            string jsonData = JsonSerializer.Serialize(initialData, InitialDataContext.Default.InitialData);
            await SendTcp(1, jsonData, clientTcpSocket); // Type 1 means servers sends initial data to the new client
            Thread.Sleep(500); // workaround else client cant get the login failed response
            // newClient.GetStream().Close();
        }
        // }
        // catch
        // {
        //     System.Console.WriteLine("Error processing data sent by new player, disconnecting new player.");
        //     stream.Close();
        // }
    }
    async Task SendTcp(byte commandType, string message, Socket clientTcpSocket)
    {
        try
        {
            byte[] messageBytes = EncodeMessage(commandType, message, true);
            await clientTcpSocket.SendAsync(messageBytes, SocketFlags.None);

        }
        catch
        {
            //Console.WriteLine($"Error sending message type {commandType}.");
        }
    }
    async Task SendUdp(byte commandType, string message, EndPoint udpEndpoint)
    {
        try
        {
            byte[] messageBytes = EncodeMessage(commandType, message, true);
            await serverUdpSocket.SendToAsync(messageBytes, SocketFlags.None, udpEndpoint);
        }
        catch
        {

        }
    }
    byte[] EncodeMessage(byte commandType, string message, bool encrypted)
    {
        string combinedMessage = $"#{commandType}#${message}$";
        if (encrypted)
            return aes.Encrypt(combinedMessage);
        else
            return Encoding.ASCII.GetBytes(combinedMessage);
    }

}


