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
    private byte maxPlayers = 0;

    private static IPEndPoint serverAddress = null;
    private int serverPort = 0;
    private TcpListener server = null;

    private static bool[] clientSlotTaken = null;
    private static ConnecetedPlayer[] connectedPlayers = null;
    //private static EveryPlayersName everyPlayersName = new EveryPlayersName();

    private static readonly Database database = new Database(); // Creates database object

    public void StartServer(byte maxPlayers, int port)
    {
        serverPort = port;
        this.maxPlayers = maxPlayers;
        serverAddress = new IPEndPoint(IPAddress.Any, serverPort);
        server = new TcpListener(serverAddress);
        server.Start();

        InitializeValues();

        Task.Run(() => WaitForNewConnections());
        Task.Run(() => ReplicatePlayerPositions());
        RunEverySecond();
    }
    private void InitializeValues()
    {
        clientSlotTaken = new bool[maxPlayers];
        connectedPlayers = new ConnecetedPlayer[maxPlayers];

        for (byte i = 0; i < maxPlayers; i++)
        {
            clientSlotTaken[i] = false;
        }
    }
    private async Task WaitForNewConnections()
    {
        while (true)
        {
            TcpClient newClient = server.AcceptTcpClient(); // Waits until a client has connected to the server
            NetworkStream stream = newClient.GetStream();
            Byte[] buffer = new Byte[512];
            int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
            await ProcessBuffer(buffer, bytesRead, newClient, null, stream); // Processes the received packet
        }
    }
    private async Task ReceiveData(ConnecetedPlayer connectedClient)
    {
        CancellationToken cancellationToken = connectedClient.cancellationTokenSource.Token;
        byte[] buffer = null;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                buffer = new byte[8192];
                int receivedBytes = await connectedClient.stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                await ProcessBuffer(buffer, receivedBytes, null, connectedClient, null);
            }
        }
        catch (OperationCanceledException)
        {
            System.Console.WriteLine($"Receiving task for client id {connectedClient.index} was cancelled");
        }
        catch (IOException ex) when (ex.InnerException is SocketException socketEx && socketEx.SocketErrorCode == SocketError.ConnectionReset)
        {
            // Handle sudden client disconnect (ConnectionReset)
            Console.WriteLine($"Client disconnected abruptly: {connectedClient.address}");
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
            Thread.Sleep(100); // server tick, 100 times a second
            for (byte i = 0; i < maxPlayers; i++) // copies the players' positions so server can send
            {
                if (connectedPlayers[i] == null) continue; // skips index if no connected player occupies the slot
                everyPlayersPosition.positions[i] = connectedPlayers[i].position;
            }

            for (byte i = 0; i < maxPlayers; i++) // loops through every connected players positions to each
            {
                if (connectedPlayers[i] == null) continue; // skips index if no connected player occupies the slot
                if (connectedPlayers[i].pingAnswered == false) continue;
                string jsonData = JsonSerializer.Serialize(everyPlayersPosition, EveryPlayersPositionContext.Default.EveryPlayersPosition);
                Send(3, jsonData, connectedPlayers[i].stream);

            }

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
        for (byte i = 0; i < maxPlayers; i++)
        {
            if (connectedPlayers[i] == null) { Console.WriteLine("Free slot"); continue; }
            Console.WriteLine(connectedPlayers[i]);
        }
    }
    private async void PingClients(byte timeoutTime)
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

            Send(0, "{p}", connectedPlayers[i].stream);

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

    private byte GetCurrentPlayerCount()
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
    private void CalculatePlayerLatency(byte clientIndex)
    {
        TimeSpan latency = connectedPlayers[clientIndex].pingRequestTime - DateTime.UtcNow;
        connectedPlayers[clientIndex].latency = Math.Abs(latency.Milliseconds) / 2;
    }

    private void DisconnectClient(byte clientIndex)
    {
        //Console.WriteLine($"Removed client {connectedPlayers[clientIndex].address} from the server");
        clientSlotTaken[clientIndex] = false; // Free a slot
        database.loggedInIds.Remove(connectedPlayers[clientIndex].address.ToString());
        connectedPlayers[clientIndex].cancellationTokenSource.Cancel();
        System.Console.WriteLine(connectedPlayers[clientIndex].stream);
        connectedPlayers[clientIndex].stream.Close();
        System.Console.WriteLine(connectedPlayers[clientIndex].stream);
        connectedPlayers[clientIndex] = null; // Remove the player
    }
    private async Task ProcessBuffer(byte[] receivedBytes, int byteLength, TcpClient newClient, ConnecetedPlayer connectedClient, NetworkStream stream)
    {
        // try
        {
            string bufferString = Encoding.ASCII.GetString(receivedBytes, 0, byteLength);
            // Console.WriteLine(bufferString);

            string packetTypePattern = @"#(.*)#";
            string packetDataPattern = @"\$(.*?)\$";

            MatchCollection packetTypeMatches = Regex.Matches(bufferString, packetTypePattern);
            MatchCollection packetDataMatches = Regex.Matches(bufferString, packetDataPattern);

            for (byte i = 0; i < packetTypeMatches.Count; i++)
            {
                byte.TryParse(packetTypeMatches[i].Groups[1].Value, out byte typeOfPacket);

                Packet packet = new Packet();
                packet.type = typeOfPacket;
                packet.data = packetDataMatches[i].Groups[1].Value;

                if (packet.type == 1)
                    await ProcessDataSentByNewPlayer(packet, newClient, stream);
                else
                    ProcessDataSentByPlayer(packet, connectedClient);
            }
        }
        // catch
        // {
        //     System.Console.WriteLine("Error processing buffer");
        // }

    }
    private void ProcessDataSentByPlayer(Packet packet, ConnecetedPlayer conectedClient)
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
    private async Task ProcessDataSentByNewPlayer(Packet packet, TcpClient newClient, NetworkStream stream)
    {
        // try
        // {
        IPEndPoint clientAddress = (IPEndPoint)newClient.Client.RemoteEndPoint; // Gets the IP address of the new client
        LoginData loginData = JsonSerializer.Deserialize(packet.data, LoginDataContext.Default.LoginData);

        bool loginOrRegister = loginData.lr; // True if client wants to login, false if client wants to register register
        string username = loginData.un;
        string hashedPassword = loginData.pw;

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
                        lr = loginResult, // value represents how the server responded to login, like if success or not
                        i = index, // client's assigned id
                        mp = maxPlayers // max player amount
                    };

                    string jsonData = JsonSerializer.Serialize(initialData, InitialDataContext.Default.InitialData);
                    Send(1, jsonData, stream); // Type 1 means servers sends initial data to the new client
                    _ = ReceiveData(connectedPlayers[index]);
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
            Send(1, jsonData, stream); // Type 1 means servers sends initial data to the new client
            Thread.Sleep(500); // workaround else client cant get the login failed response
            stream.Close();
        }
        // }
        // catch
        // {
        //     System.Console.WriteLine("Error processing data sent by new player, disconnecting new player.");
        //     stream.Close();
        // }
    }
    public void Send(byte commandType, string message, NetworkStream stream)
    {
        try
        {
            byte[] messageBytes = Encoding.ASCII.GetBytes($"#{commandType}#${message}$");


            stream.Write(messageBytes);
            // if (commandType == 1)
            //     System.Console.WriteLine("initialdata sent");
        }
        catch
        {
            Console.WriteLine($"Error sending message type {commandType}.");
        }
    }

}


