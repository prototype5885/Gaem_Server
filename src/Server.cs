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
    private int maxPlayers;

    private static IPEndPoint serverAddress;
    private int serverPort;
    private static Socket socket;

    private static readonly Database database = new Database(); // Creates database object
    private static readonly PacketProcessing packetProcessing = new PacketProcessing();

    private static bool[] clientSlotTaken;
    private static ConnecetedPlayer[] connectedPlayers;
    //private static EveryPlayersName everyPlayersName = new EveryPlayersName();

    public void StartUdpServer(int maxPlayers, int port)
    {
        serverPort = port;
        this.maxPlayers = maxPlayers;
        serverAddress = new IPEndPoint(IPAddress.Any, serverPort);
        socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Bind(serverAddress);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Due to this issue: https://stackoverflow.com/questions/74327225/why-does-sending-via-a-udpclient-cause-subsequent-receiving-to-fail
            // .. the following needs to be done on windows
            const uint IOC_IN = 0x80000000U;
            const uint IOC_VENDOR = 0x18000000U;
            const int SIO_UDP_CONNRESET = unchecked((int)(IOC_IN | IOC_VENDOR | 12));
            socket.IOControl(SIO_UDP_CONNRESET, new byte[] { 0x00 }, null);
        }

        InitializeValues();

        Task.Run(() => ReceiveData());
        Task.Run(() => ReplicatePlayerPositions());
        RunEverySecond();
    }
    private void InitializeValues()
    {
        clientSlotTaken = new bool[maxPlayers];
        connectedPlayers = new ConnecetedPlayer[maxPlayers];
        packetProcessing.socket = socket;

        for (int i = 0; i < maxPlayers; i++)
        {
            clientSlotTaken[i] = false;
        }
    }
    private async Task ReceiveData()
    {

        while (true)
        {
            try
            {
                byte[] buffer = new byte[1024];
                EndPoint clientEndPoint = new IPEndPoint(IPAddress.Any, 0);
                SocketReceiveFromResult receivedData = await socket.ReceiveFromAsync(buffer, SocketFlags.None, clientEndPoint);

                Packet packet = packetProcessing.BreakUpPacket(buffer, receivedData.ReceivedBytes); // Processes the received packet

                //await Console.Out.WriteLineAsync(packet.data);

                if (packet == null) continue; // Stops if packet can't be processed

                EndPoint clientAddress = receivedData.RemoteEndPoint;

                // Runs if new client wants to connect
                if (packet.type == 1)
                {
                    int loginResult = Authentication(packet, clientAddress); // checks if username/password is correct
                    await HandleNewlyConnectedClient(loginResult, clientAddress);
                }

                // Stops here if client isn't authenticated yet
                int clientIndex = -1;
                for (int i = 0; i < maxPlayers; i++)
                {
                    if (connectedPlayers[i] != null && clientAddress.Equals(connectedPlayers[i].address))
                    {
                        clientIndex = i;
                    }
                }
                if (clientIndex == -1) continue;

                switch (packet.type)
                {
                    // Type 0 means client answers the ping
                    case 0:
                        connectedPlayers[clientIndex].pingAnswered = true;
                        connectedPlayers[clientIndex].status = 1;
                        CalculatePlayerLatency(clientIndex);
                        break;
                    // Type 3 means client is sending its own position to the server
                    case 3:
                        PlayerPosition clientPlayerPosition = JsonSerializer.Deserialize(packet.data, PlayerPositionContext.Default.PlayerPosition);
                        connectedPlayers[clientIndex].position = clientPlayerPosition;
                        break;
                    // Type 10 means its an ACK for a reliable message
                    case 10:
                        packetProcessing.AcknowledgeReceived(packet.data);
                        break;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
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
                await packetProcessing.SendUnreliable(3, jsonData, connectedPlayers[i].address);

            }
            Thread.Sleep(10); // server tick, 100 times a second
        }
    }
    private void RunEverySecond()
    {
        const byte timeoutTime = 10;

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

            await packetProcessing.SendUnreliable(0, "", connectedPlayers[i].address);

            connectedPlayers[i].pingRequestTime = DateTime.UtcNow;
        }
    }
    private void UpdatePlayerNamesBeforeSending()
    {

    }
    private async void SendPlayerNames(EndPoint clientAddress)
    {
        EveryPlayersName everyPlayersName = new EveryPlayersName();
        everyPlayersName.playerNames = new string[maxPlayers];

        int index = 0;
        for (int i = 0; i < maxPlayers; i++)
        {
            if (connectedPlayers[i] == null) continue;
            //everyPlayersName.playerIndex[i] = connectedPlayers[i].index;
            everyPlayersName.playerNames[index] = connectedPlayers[i].playerName;
            index++;
        }

        string jsonData = JsonSerializer.Serialize(everyPlayersName, EveryPlayersNameContext.Default.EveryPlayersName);
        await packetProcessing.SendUnreliable(4, jsonData, clientAddress);
    }

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
    private int Authentication(Packet packet, EndPoint clientAddress)
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
    private async Task HandleNewlyConnectedClient(int loginResult, EndPoint clientAddress)
    {
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
                        address = clientAddress
                    };
                    connectedPlayers[index].playerName = database.GetUsername(connectedPlayers[index].databaseID);


                    //Console.WriteLine($"Assigned index slot {index}");

                    InitialData initialData = new InitialData
                    {
                        lr = loginResult, // 
                        i = index, // client's assigned id
                        mp = maxPlayers // max player amount
                    };

                    string jsonData = JsonSerializer.Serialize(initialData, InitialDataContext.Default.InitialData);
                    await packetProcessing.SendUnreliable(1, jsonData, clientAddress); // Type 1 means servers sends initial data to the new client
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
            await packetProcessing.SendUnreliable(1, jsonData, clientAddress); // Type 1 means servers sends initial data to the new client
        }
    }
    private void DisconnectClient(int clientIndex)
    {
        //Console.WriteLine($"Removed client {connectedPlayers[clientIndex].address} from the server");
        clientSlotTaken[clientIndex] = false; // Free a slot
        database.loggedInIds.Remove(connectedPlayers[clientIndex].address.ToString());
        connectedPlayers[clientIndex] = null; // Remove the player
    }
}

