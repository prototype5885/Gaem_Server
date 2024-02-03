using System.Net.Sockets;
using System.Net;
using System.Text;

using System.Text.Json;
using System.Net.Http.Headers;
using System;
using System.Reflection;
using System.Numerics;

public class ServerUDP
{
    private int maxPlayers;

    private UdpClient udpClient;
    private IPEndPoint[] connectedUdpClient;


    private Database database = new Database(); // Creates database object
    private PacketProcessing packetProcessing;

    private Players players = new Players(); // List of every players' position and such

    private bool[] pingedPlayers; // keeps track of which clients pinged or did not ping back, true = replied to ping, false = didnt reply
    private int[] timeUntillTimeout;

    private Dictionary<int, CompleteClientInfo> clients = new Dictionary<int, CompleteClientInfo>();



    public ServerUDP(int maxPlayers, PacketProcessing packetProcessing)
    {
        this.maxPlayers = maxPlayers;
        this.packetProcessing = packetProcessing;

        StartUdpServer();
    }
    private void StartUdpServer()
    {
        try
        {
            const int port = 1943;
            udpClient = new UdpClient(port);

            connectedUdpClient = new IPEndPoint[maxPlayers];
            pingedPlayers = new bool[maxPlayers];
            timeUntillTimeout = new int[maxPlayers];
            players.arrayOfPlayersData = new Player[maxPlayers];
            clientStatus = new int[maxPlayers];

            Console.WriteLine($"UDP Server is listening on port {port}...");

            Task.Run(ReceiveDataUdp);
            Task.Run(SendDataUdp);
            Task.Run(PingClients);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Server failed to start with exception: " + ex);
        }
    }
    private async Task ReceiveDataUdp()
    {
        while (true)
        {
            UdpReceiveResult udpReceiveResult = await udpClient.ReceiveAsync();

            Packet packet = packetProcessing.BreakUpPacket(udpReceiveResult.Buffer);

            try
            {
                switch (packet.type)
                {
                    case 0: // Client answers the ping
                        //await Console.Out.WriteLineAsync($"A client from {udpReceiveResult.RemoteEndPoint} answered the ping");
                        int index = Array.IndexOf(connectedUdpClient, udpReceiveResult.RemoteEndPoint); // gets the index of client who pinged back

                        pingedPlayers[index] = true;
                        break;
                    case 1: // If client wants to connect

                        //await Console.Out.WriteLineAsync($"A client from {udpReceiveResult.RemoteEndPoint} is connecting");
                        byte[] messageByte = Encoding.ASCII.GetBytes("1"); // Reply 1 means that connection is accepted
                        await udpClient.SendAsync(messageByte, messageByte.Length, udpReceiveResult.RemoteEndPoint);
                        CheckForFreeSlots(udpReceiveResult.RemoteEndPoint);
                        break;

                    case 2: // If client wants to login/register
                        //await Console.Out.WriteLineAsync($"A client from {udpReceiveResult.RemoteEndPoint} is trying to login or register");
                        await Authentication(packet.data, udpReceiveResult.RemoteEndPoint);
                        break;

                    case 3: // If client sends its own position
                        Player clientPlayer = JsonSerializer.Deserialize(packet.data, PlayerContext.Default.Player);

                        foreach (IPEndPoint clientAddress in connectedUdpClient)
                        {
                            if (clientAddress != null)
                            {
                                int clientIndex = Array.IndexOf(connectedUdpClient, clientAddress);
                                if (udpReceiveResult.RemoteEndPoint.Equals(connectedUdpClient[clientIndex]))
                                {
                                    players.arrayOfPlayersData[clientIndex] = clientPlayer;
                                }
                            }
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
    }
    private async Task SendDataUdp()
    {
        while (true)
        {
            foreach (IPEndPoint clientAddress in connectedUdpClient)
            {
                if (clientAddress != null)
                {
                    int index = Array.IndexOf(connectedUdpClient, clientAddress);
                    try
                    {
                        string jsonData = JsonSerializer.Serialize(players, PlayersContext.Default.Players);
                        int commandType = 4; // Type 4 means server is sending position of other players
                        byte[] messageByte = Encoding.ASCII.GetBytes($"#{commandType}#{jsonData}");
                        await udpClient.SendAsync(messageByte, messageByte.Length, clientAddress);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error sending UDP message to client index {index}. Exception: {ex.Message}");
                    }
                }
            }
            //dataProcessing.PrintConnectedClients();

            //Thread.Sleep(10);
        }
    }
    private async Task PingClients()
    {
        const int timeoutTime = 10;
        //int[] counterToTimeout = new int[maxPlayers]; // array that stores timeout values for each client

        for (int i = 0; i < maxPlayers; i++) // initializes array that stores timeout values for each client
        {
            timeUntillTimeout[i] = timeoutTime;
        }

        IPEndPoint clientAddress;
        while (true)
        {
            MonitorValues();
            for (int i = 0; i < maxPlayers; i++)
            {
                clientAddress = connectedUdpClient[i];
                if (clientAddress == null) continue;

                if (pingedPlayers[i] == false) // runs if connected client hasnt replied to ping
                {
                    timeUntillTimeout[i]--;
                    //Console.WriteLine($"Index {i} player will timeout in: {counterToTimeout[i]} seconds");

                    if (timeUntillTimeout[i] < 1) // runs if client didnt answer during timeout interval
                    {
                        DisconnectClient(i);
                    }
                }
                else if (pingedPlayers[i] == true && timeUntillTimeout[i] != timeoutTime) // runs if connected client answered the ping
                {
                    //Console.WriteLine($"Timeout timer was reset for index {i} player");
                    timeUntillTimeout[i] = timeoutTime;
                }


                //Console.WriteLine("Reseting ping array");
                pingedPlayers[i] = false; // resets the array

                int commandType = 0; // Type 0 means pinging
                byte[] messageByte = Encoding.ASCII.GetBytes($"#{commandType}#");
                await udpClient.SendAsync(messageByte, messageByte.Length, clientAddress);

                //Console.WriteLine(connectedUdpClient[i]);
            }
            Thread.Sleep(1000);
        }
    }
    private void MonitorValues()
    {
        Console.Clear();
        CompleteClientInfo completeClientDebugInfo;
        for (int i = 0; i < maxPlayers; i++)
        {
            //Console.WriteLine(connectedUdpClient[i]);
            if (connectedUdpClient[i] != null)
            {
                completeClientDebugInfo = new CompleteClientInfo();
                completeClientDebugInfo.IPEndPoint = connectedUdpClient[i];

                completeClientDebugInfo.clientindex = i;
                completeClientDebugInfo.pingAnswered = pingedPlayers[i];
                completeClientDebugInfo.timeUntillTimeout = timeUntillTimeout[i];

                float posX = (float)Math.Round(players.arrayOfPlayersData[i].x);
                float posY = (float)Math.Round(players.arrayOfPlayersData[i].y);
                float posZ = (float)Math.Round(players.arrayOfPlayersData[i].z);

                completeClientDebugInfo.position = new Vector3(posX, posY, posZ);

                Console.WriteLine(completeClientDebugInfo.ToString());
                return;
            }
        }
        Console.WriteLine("Server is running, but nobody is connected");
    }
    private async Task Authentication(string packetString, IPEndPoint clientAddress) // Authenticate the client
    {
        Console.WriteLine("Starting authentication of client...");


        // Converts received json format data to username and password
        LoginData loginData = new LoginData();
        try
        {
            loginData = JsonSerializer.Deserialize(packetString, LoginDataContext.Default.LoginData);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{ex.Message}");
        }

        bool LoginOrRegister = loginData.lr; // True if client wants to login, false if client wants to register register
        string username = loginData.un;
        string hashedPassword = loginData.pw;

        if (LoginOrRegister == true) // Runs if client wants to login
        {
            if (database.LoginUser(username, hashedPassword)) // Checks if username and password exists in the database
            {
                Console.WriteLine("Login was accepted");

                // Send a response back to the client
                string response = "1"; // Response 1 means the username/password were accepted
                byte[] messageByte = Encoding.ASCII.GetBytes(response); // Adds the length of the message
                await udpClient.SendAsync(messageByte, messageByte.Length, clientAddress);

                // Accepts connection
                //CheckForFreeSlots(clientAddress);
            }
            else // Rejects
            {
                Console.WriteLine("Client entered wrong username or password");

                // Send a response back to the client
                string response = "2"; // Response 2 means the username/password were rejected
                byte[] messageByte = Encoding.ASCII.GetBytes(response); // Adds the length of the message
                await udpClient.SendAsync(messageByte, messageByte.Length, clientAddress);

                // Rejects connection
                //client.Close();
            }
        }
        else // Runs if client wants to register
        {
            if (username.Length > 16) // Checks if username is longer than 16 characters
            {
                Console.WriteLine("Client's chosen username is too long");
                string response = "2"; // Response 2 means username is too long
                byte[] messageByte = Encoding.ASCII.GetBytes(response); // Adds the length of the message
                await udpClient.SendAsync(messageByte, messageByte.Length, clientAddress);
            }
            else if (database.RegisterUser(username, hashedPassword, clientAddress.Address.ToString())) // Runs if registration was succesful
            {
                Console.WriteLine("Registration of client was successful");
                string response = "1"; // Response 1 means registration was successful
                byte[] messageByte = Encoding.ASCII.GetBytes(response); // Adds the length of the message
                await udpClient.SendAsync(messageByte, messageByte.Length, clientAddress);

                //CheckForFreeSlots(clientAddress);
            }
            else
            {
                Console.WriteLine("Client's chosen username is already taken");
                string response = "3"; // Response 3 means username is already taken
                byte[] messageByte = Encoding.ASCII.GetBytes(response); // Adds the length of the message
                                                                        //await authenticationStream.WriteAsync(messageByte, 0, messageByte.Length);
                await udpClient.SendAsync(messageByte, messageByte.Length, clientAddress);
            }
        }
    }
    private void CheckForFreeSlots(IPEndPoint clientAddress)
    {
        string clientIpAddress = (clientAddress).Address.ToString(); // Gets the IP address of the accepted

        //database.UpdateLastLoginIP(username, clientIpAddress);

        for (int index = 0; index < connectedUdpClient.Length; index++)
        {
            if (connectedUdpClient[index] == null)
            {
                connectedUdpClient[index] = clientAddress; // Adds new client to list of tcp clients
                Console.WriteLine($"Assigned index slot {index}");

                ClientAccepted(clientAddress, index);
                return;
            }
        }
        Console.WriteLine("Client rejected, no more free slots");
        // here code should run that tells client it was rejected because server is full
    }
    private void ClientAccepted(IPEndPoint clientAddress, int index)
    {
        SendInitialData(clientAddress, index);
        connectedUdpClient[index] = clientAddress;
        players.arrayOfPlayersData[index] = new Player(); // Assings new client to list managing player position
    }
    void SendInitialData(IPEndPoint clientAddress, int index) // Sends the client the initial stuff
    {
        try
        {
            //NetworkStream sendingStream = client.GetStream();

            InitialData initialData = new InitialData();
            initialData.i = index; // Prepares sending client's own index to new client
            initialData.mp = maxPlayers; // Prepares sending maxplayers amount to new client

            string jsonData = JsonSerializer.Serialize(initialData, InitialDataContext.Default.InitialData);

            byte[] messageByte = Encoding.ASCII.GetBytes(jsonData);
            udpClient.Send(messageByte, messageByte.Length, clientAddress);


        }
        catch (Exception ex)
        {
            Console.Out.WriteLine(ex.ToString());
        }
    }
    void DisconnectClient(int index)
    {
        connectedUdpClient[index] = null; // disconnects the client
        players.arrayOfPlayersData[index] = null;
        //players.list.
        Console.WriteLine($"Removed client index {index} from the server");
    }

}

