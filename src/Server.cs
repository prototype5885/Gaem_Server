using System.Net.Sockets;
using System.Net;
using System.Text;

using System.Text.Json;
using System.Collections;
using System.Security;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;


public class Server
{
    private int maxPlayers;

    private static IPEndPoint serverAddress;
    private int serverPort;
    private static Socket socket;

    private static readonly Database database = new Database(); // Creates database object

    private static bool[] clientSlotTaken;
    private static ConnecetedPlayer[] connectedPlayers;
    //private static EveryPlayersName everyPlayersName = new EveryPlayersName();

    public void StartUdpServer(int maxPlayers, int port)
    {
        serverPort = port;
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

        this.maxPlayers = maxPlayers;
        clientSlotTaken = new bool[maxPlayers];
        connectedPlayers = new ConnecetedPlayer[maxPlayers];
        //everyPlayersName.playerIndex = new int[maxPlayers];
        //everyPlayersName.playerNames = new string[maxPlayers];

        for (int i = 0; i < maxPlayers; i++)
        {
            clientSlotTaken[i] = false;
        }

        Task.Run(() => RunEverySecond());
        Task.Run(() => ReceiveDataUdp());
        SendDataUdp();
    }
    private async Task ReceiveDataUdp()
    {
        PacketProcessing packetProcessing = new PacketProcessing();
        while (true)
        {
            try
            {
                byte[] buffer = new byte[512];
                EndPoint clientEndPoint = new IPEndPoint(IPAddress.Any, 0);
                SocketReceiveFromResult receivedData = await socket.ReceiveFromAsync(buffer, SocketFlags.None, clientEndPoint);

                Packet packet = packetProcessing.BreakUpPacket(buffer, receivedData.ReceivedBytes); // Processes the received packet

                // await Console.Out.WriteLineAsync(packet.data);

                if (packet == null) continue; // Stops if packet can't be processed

                EndPoint clientAddress = receivedData.RemoteEndPoint;

                // Runs if new client wants to connect
                if (packet.type == 1)
                {
                    int loginResult = Authentication(packet, clientAddress);

                    int commandType = 1; // Type 1 means server responds to client if login/register was fail or not
                    byte[] messageByte = Encoding.ASCII.GetBytes($"#{commandType}#{loginResult}");
                    await socket.SendToAsync(messageByte, SocketFlags.None, clientAddress);

                    switch (loginResult)
                    {
                        case 1:
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


                                    Console.WriteLine($"Assigned index slot {index}");

                                    InitialData initialData = new InitialData
                                    {
                                        i = index, // Prepares sending client's own index to new client
                                        mp = maxPlayers // Prepares sending max players amount to new client
                                    };

                                    string jsonData = JsonSerializer.Serialize(initialData, InitialDataContext.Default.InitialData);

                                    int replyCommandType = 2; // Type 2 means servers sends initial data to the new client
                                    byte[] replyMessageByte = Encoding.ASCII.GetBytes($"#{replyCommandType}#{jsonData}");
                                    await socket.SendToAsync(replyMessageByte, SocketFlags.None, clientAddress);
                                    break;
                                }
                                Console.WriteLine("Server is full");
                            }
                            break;
                        case 2: // wrong username or password

                            break;
                    }
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
                    case 0: // Type 0 means client answers the ping
                        connectedPlayers[clientIndex].pingAnswered = true;
                        connectedPlayers[clientIndex].status = 1;
                        CalculatePlayerLatency(clientIndex);
                        break;
                    case 3: // Type 3 means client is sending its own position to the server
                        PlayerPosition clientPlayerPosition = JsonSerializer.Deserialize(packet.data, PlayerPositionContext.Default.PlayerPosition);
                        connectedPlayers[clientIndex].position = clientPlayerPosition;
                        break;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }
    private void SendDataUdp()
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

            for (int i = 0; i < maxPlayers; i++) // loops through every connected players
            {
                if (connectedPlayers[i] == null) continue; // skips index if no connected player occupies the slot
                if (connectedPlayers[i].pingAnswered == true) // wont send players' position to clients that are currently timing out
                {
                    try
                    {
                        string jsonData = JsonSerializer.Serialize(everyPlayersPosition, EveryPlayersPositionContext.Default.EveryPlayersPosition);
                        int commandType = 3; // Type 3 means server sends everyone's position to the clients
                        byte[] messageByte = Encoding.ASCII.GetBytes($"#{commandType}#{jsonData}");
                        socket.SendTo(messageByte, connectedPlayers[i].address);
                    }
                    catch (Exception ex)
                    {
                        Console.Clear();
                        Console.WriteLine($"Error sending UDP message to client index {connectedPlayers[i].address}. Exception: {ex.Message}");
                    }
                }
            }
            Thread.Sleep(10); // server tick, 100 times a second
        }
    }
    private async Task RunEverySecond()
    {
        const byte timeoutTime = 10;

        while (true)
        {
            MonitorValues();
            await PingClients(timeoutTime);
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
    private async Task PingClients(byte timeoutTime)
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
                //Console.WriteLine($"Timeout timer was reset for index {i} player");
                connectedPlayers[i].timeUntillTimeout = timeoutTime;
            }


            //Console.WriteLine("Resetting ping array");
            connectedPlayers[i].pingAnswered = false; // resets the array

            int commandType = 0; // Type 0 means pinging
            byte[] messageByte = Encoding.ASCII.GetBytes($"#{commandType}#");
            await socket.SendToAsync(messageByte, SocketFlags.None, connectedPlayers[i].address);
            connectedPlayers[i].pingRequestTime = DateTime.UtcNow;

            //Console.WriteLine(connectedUdpClient[i]);
        }
    }
    private void UpdatePlayerNamesBeforeSending()
    {

    }
    private void SendPlayerNames(EndPoint clientAddress)
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
        int commandType = 4; // Type 4 means server sends everyone's name to the clients
        byte[] messageByte = Encoding.ASCII.GetBytes($"#{commandType}#{jsonData}");
        socket.SendTo(messageByte, clientAddress);
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
        Console.WriteLine("Starting authentication of client...");

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
                    Console.WriteLine("Login was accepted");
                    return 1;
                case 2:
                    Console.WriteLine("Client entered wrong username or password");
                    return 2;
                case 3:
                    Console.WriteLine("No user was found with this name");
                    return 3;
                case 4:
                    Console.WriteLine("This user is already logged in");
                    return 4;
            }
            return -1;
        }
        else // Runs if client wants to register
        {
            if (username.Length < 2 || username.Length > 16) // Checks if username is longer than 16 or shorter than 2 characters
            {
                Console.WriteLine("Client's chosen username is too long or too short");
                return 5; // 5 means username is too long or too short
            }
            else if (database.RegisterUser(username, hashedPassword, clientAddress.ToString())) // Runs if registration was successful
            {
                Console.WriteLine("Registration of client was successful");
                return 1; // 1 means registration was successful
            }
            else
            {
                Console.WriteLine("Client's chosen username is already taken");
                return 6; // 6 means username is already taken
            }
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

