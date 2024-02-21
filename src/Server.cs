using System.Net.Sockets;
using System.Net;
using System.Text;

using System.Text.Json;
using System.Collections;
using System.Security;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System;


public class Server
{
    private int maxPlayers = 0;

    private static IPEndPoint serverAddress = null;
    private int serverPort = 0;
    private TcpListener server = null;

    private static bool[] clientSlotTaken = null;
    private static ConnecetedPlayer[] connectedPlayers = null;
    //private static EveryPlayersName everyPlayersName = new EveryPlayersName();

    private static readonly Database database = new Database(); // Creates database object
    private static readonly PacketProcessing packetProcessing = new PacketProcessing();

    public void StartServer(int maxPlayers, int port)
    {
        serverPort = port;
        this.maxPlayers = maxPlayers;
        serverAddress = new IPEndPoint(IPAddress.Any, serverPort);
        server = new TcpListener(serverAddress);
        server.Start();

        InitializeValues();

        Task.Run(() => WaitForConnections());
        Task.Run(() => ReplicatePlayerPositions());
        RunEverySecond();
    }
    private void InitializeValues()
    {
        clientSlotTaken = new bool[maxPlayers];
        connectedPlayers = new ConnecetedPlayer[maxPlayers];
        // packetProcessing.socket = socket;

        for (int i = 0; i < maxPlayers; i++)
        {
            clientSlotTaken[i] = false;
        }
    }
    private async Task WaitForConnections()
    {
        while (true)
        {
            TcpClient client = server.AcceptTcpClient(); // Waits until a client has connected to the server
            IPEndPoint clientAddress = (IPEndPoint)client.Client.RemoteEndPoint; // Gets the IP address of the new client

            NetworkStream stream = client.GetStream();

            Byte[] buffer = new Byte[256];

            int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);

            Packet packet = packetProcessing.BreakUpPacket(buffer, bytesRead); // Processes the received packet

            if (packet.type == 1)
            {
                int loginResult = Authentication(packet, clientAddress); // checks if username/password is correct

                if (loginResult == 1)
                {
                    //await packetProcessing.SendUnreliable(10, packet.data, null); // send ACK back
                    for (int index = 0; index < maxPlayers; index++)
                    {
                        if (clientSlotTaken[index] == false)
                        {
                            clientSlotTaken[index] = true; // New client will take found the empty slot 

                            connectedPlayers[index] = new ConnecetedPlayer
                            {
                                index = index,
                                databaseID = database.loggedInIds[clientAddress.ToString()],
                                address = clientAddress,
                                stream = stream,
                                cancellationTokenSource = new CancellationTokenSource()
                            };
                            connectedPlayers[index].playerName = database.GetUsername(connectedPlayers[index].databaseID);


                            InitialData initialData = new InitialData
                            {
                                lr = loginResult, // 
                                i = index, // client's assigned id
                                mp = maxPlayers // max player amount
                            };

                            System.Console.WriteLine("replying");

                            string jsonData = JsonSerializer.Serialize(initialData, InitialDataContext.Default.InitialData);
                            await packetProcessing.Send(1, jsonData, stream); // Type 1 means servers sends initial data to the new client
                            _ = ReceiveData(connectedPlayers[index]);
                            break;
                        }
                    }
                }
                else // login failed
                {
                    InitialData initialData = new InitialData
                    {
                        lr = loginResult, // 
                        i = -1, // client's assigned id
                        mp = -1 // max player amount
                    };

                    string jsonData = JsonSerializer.Serialize(initialData, InitialDataContext.Default.InitialData);
                    await packetProcessing.Send(1, jsonData, stream); // Type 1 means servers sends initial data to the new client
                    stream.Close();
                }
            }
            else
            {
                stream.Close();
            }
        }
    }
    private async Task ReceiveData(ConnecetedPlayer client)
    {
        CancellationToken cancellationToken = client.cancellationTokenSource.Token;
        byte[] buffer = null;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                buffer = new byte[1024];
                EndPoint clientEndPoint = new IPEndPoint(IPAddress.Any, 0);
                int receivedBytes = await client.stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                // int receivedBytes = await client.stream.ReadAsync(buffer, 0, buffer.Length);
                Packet packet = packetProcessing.BreakUpPacket(buffer, receivedBytes); // Processes the received packet

                if (packet == null) continue; // Stops if packet can't be processed

                switch (packet.type)
                {
                    // Type 0 means client answers the ping
                    case 0:
                        client.pingAnswered = true;
                        client.status = 1;
                        CalculatePlayerLatency(client.index);
                        break;
                    // Type 3 means client is sending its own position to the server
                    case 3:
                        PlayerPosition clientPlayerPosition = JsonSerializer.Deserialize(packet.data, PlayerPositionContext.Default.PlayerPosition);
                        client.position = clientPlayerPosition;
                        break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            System.Console.WriteLine($"Receiving task for client id {client.index} was cancelled");
        }
        catch (IOException ex) when (ex.InnerException is SocketException socketEx && socketEx.SocketErrorCode == SocketError.ConnectionReset)
        {
            // Handle sudden client disconnect (ConnectionReset)
            Console.WriteLine($"Client disconnected abruptly: {client.address}");
        }
        // catch (Exception e)
        // {
        //     Console.WriteLine("fail");
        // }
    }

    private async Task ReplicatePlayerPositions()
    {
        EveryPlayersPosition everyPlayersPosition = new EveryPlayersPosition(); // this thing is the format the server sends player positions in to each client
        everyPlayersPosition.positions = new PlayerPosition[maxPlayers];

        while (true)
        {
            for (int i = 0; i < maxPlayers; i++) // copies the players' positions so server can send
            {
                if (connectedPlayers[i] == null) continue; // skips index if no connected player occupies the slot
                everyPlayersPosition.positions[i] = connectedPlayers[i].position;
            }

            for (int i = 0; i < maxPlayers; i++) // loops through every connected players positions to each
            {
                if (connectedPlayers[i] == null) continue; // skips index if no connected player occupies the slot
                if (connectedPlayers[i].pingAnswered == false) continue;
                string jsonData = JsonSerializer.Serialize(everyPlayersPosition, EveryPlayersPositionContext.Default.EveryPlayersPosition);
                await packetProcessing.Send(3, jsonData, connectedPlayers[i].stream);

            }
            Thread.Sleep(100); // server tick, 100 times a second
        }
    }
    private void RunEverySecond()
    {
        const byte timeoutTime = 4;

        while (true)
        {
            MonitorValues();
            PingClients(timeoutTime);
            Thread.Sleep(1000);
        }
    }
    private void MonitorValues()
    {
        Console.Clear();
        Console.WriteLine($"Server port: {serverPort} | Players: {GetCurrentPlayerCount()}/{maxPlayers}\n");
        for (int i = 0; i < maxPlayers; i++)
        {
            if (connectedPlayers[i] == null) { Console.WriteLine("Free slot"); continue; }
            Console.WriteLine(connectedPlayers[i]);
        }
    }
    private async void PingClients(byte timeoutTime)
    {
        for (int i = 0; i < maxPlayers; i++)
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

            await packetProcessing.Send(0, "{p}", connectedPlayers[i].stream);

            connectedPlayers[i].pingRequestTime = DateTime.UtcNow;
        }
    }
    private void UpdatePlayerNamesBeforeSending()
    {

    }
    // private async void SendPlayerNames(EndPoint clientAddress)
    // {
    //     EveryPlayersName everyPlayersName = new EveryPlayersName();
    //     everyPlayersName.playerNames = new string[maxPlayers];

    //     int index = 0;
    //     for (int i = 0; i < maxPlayers; i++)
    //     {
    //         if (connectedPlayers[i] == null) continue;
    //         //everyPlayersName.playerIndex[i] = connectedPlayers[i].index;
    //         everyPlayersName.playerNames[index] = connectedPlayers[i].playerName;
    //         index++;
    //     }

    //     string jsonData = JsonSerializer.Serialize(everyPlayersName, EveryPlayersNameContext.Default.EveryPlayersName);
    //     await packetProcessing.Send(4, jsonData, clientAddress);
    // }

    private int GetCurrentPlayerCount()
    {
        int playerCount = 0;
        for (int i = 0; i < maxPlayers; i++)
        {
            if (connectedPlayers[i] != null)
            {
                playerCount++;
            }
        }
        return playerCount;
    }
    private void CalculatePlayerLatency(int clientIndex)
    {
        TimeSpan latency = connectedPlayers[clientIndex].pingRequestTime - DateTime.UtcNow;
        connectedPlayers[clientIndex].latency = Math.Abs(latency.Milliseconds) / 2;
    }
    private int Authentication(Packet packet, IPEndPoint clientAddress)
    {
        //Console.WriteLine("Starting authentication of client...");

        // Converts received json format data to username and password
        LoginData loginData = new LoginData();
        try
        {
            loginData = JsonSerializer.Deserialize(packet.data, LoginDataContext.Default.LoginData);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{ex.Message}");
        }

        bool loginOrRegister = loginData.lr; // True if client wants to login, false if client wants to register register
        string username = loginData.un;
        string hashedPassword = loginData.pw;

        if (loginOrRegister == true) // Runs if client wants to login
        {
            byte loginResult = database.LoginUser(username, hashedPassword, clientAddress.ToString()); // Try to login the user
            switch (loginResult)
            {
                case 1:
                    return 1; // Login was accepted
                case 2:
                    return 2; // Client entered wrong username or password
                case 3:
                    return 3; // No user was found with this name
                case 4:
                    return 4; // This user is already logged in
            }
            return -1;
        }
        else // Runs if client wants to register
        {
            if (username.Length < 2 || username.Length > 16) // Checks if username is longer than 16 or shorter than 2 characters
            {
                return 5; // Client's chosen username is too long or too short
            }
            else if (database.RegisterUser(username, hashedPassword, clientAddress.ToString())) // Runs if registration was successful
            {
                return 1; // Registration of client was successful
            }
            else
            {
                return 6; // Client's chosen username is already taken
            }
        }
    }

    private void DisconnectClient(int clientIndex)
    {
        //Console.WriteLine($"Removed client {connectedPlayers[clientIndex].address} from the server");
        clientSlotTaken[clientIndex] = false; // Free a slot
        database.loggedInIds.Remove(connectedPlayers[clientIndex].address.ToString());
        System.Console.WriteLine("cancel start");
        connectedPlayers[clientIndex].cancellationTokenSource.Cancel();
        connectedPlayers[clientIndex].stream.Close();
        connectedPlayers[clientIndex] = null; // Remove the player
    }
}

