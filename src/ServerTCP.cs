using System;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Timers;


public class ServerTCP
{
    int maxPlayers;

    // TCP
    TcpListener tcpListener; // TCP Server
    TcpClient[] tcpClient; // List of TCP clients
    // TCP



    Database database = new Database(); // Creates database object
    DataProcessing dataProcessing; // Object that deals with managing players and fixing received packets

    public ServerTCP(int maxPlayers, DataProcessing dataProcessing)
    {
        this.maxPlayers = maxPlayers;
        this.dataProcessing = dataProcessing;

        StartTCPServer();
    }
    public void StartTCPServer()
    {
        try
        {
            int port = 1942;
            tcpListener = new TcpListener(IPAddress.Any, port); // Sets server address
            tcpClient = new TcpClient[maxPlayers]; // Initializes array of connected clients
            tcpListener.Start(); // Starts the server
            Console.WriteLine($"TCP Server is listening on port {port}...");

            Task.Run(() => SendDataTCP()); // Starts the async task that handles sending data to each client
            WaitingForNewTCPClients(); // Waiting for clients to connect
        }
        catch (Exception ex)
        {
            Console.WriteLine("Server failed to start with exception: " + ex);
        }
    }
    void WaitingForNewTCPClients()
    {
        Console.WriteLine("Server started, waiting for clients to connect...");
        try
        {
            while (true)
            {

                TcpClient tcpClient = tcpListener.AcceptTcpClient(); // Waits/blocks until a client has connected to the server
                string clientIpAddress = ((IPEndPoint)tcpClient.Client.RemoteEndPoint).Address.ToString(); // Gets the IP address of the new client
                Console.WriteLine($"Client connecting from {clientIpAddress}"); // Prints info about the new client

                //if (ipAuthentication)
                //{
                //    bool ipAlreadyConnected = false;
                //    foreach (string ipAddress in ipAddresses) // Rejects connection if a client tries to connect from same ip address multiple times
                //    {
                //        if (ipAddress == clientIpAddress)
                //        {
                //            Console.WriteLine($"Connection rejected for {clientIpAddress}: A client with same IP address is already connected.");
                //            client.Close();
                //            ipAlreadyConnected = true;
                //        }
                //    }
                //    if (ipAlreadyConnected) // Restarts the while loop if ip is already connected
                //    {
                //        continue;
                //    }
                //}
                Task.Run(() => Authentication(tcpClient, clientIpAddress)); // Proceeds to check the authentication of the connecting client

            }
        }
        catch (SocketException ex)
        {
            Console.WriteLine($"SocketException: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception: {ex.Message}");
        }
    }
    async Task Authentication(TcpClient client, string clientIpAddress) // Authenticate the client
    {
        Console.WriteLine("Starting authentication of client...");
        NetworkStream authenticationStream = client.GetStream();

        byte[] receivedBytes = new byte[1024];
        int bytesRead;

        while (true)
        {

            bytesRead = await authenticationStream.ReadAsync(receivedBytes, 0, receivedBytes.Length); // Waits for new client to send username and password
            string receivedData = dataProcessing.ByteToStringWithFix(receivedBytes, bytesRead); // Converts byte to string and tries to fix if multiple packets were read as one

            // Converts received json format data to username and password
            LoginData loginData;
            try
            {
                loginData = JsonSerializer.Deserialize(receivedData, LoginDataContext.Default.LoginData);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{ex.Message}");
                await authenticationStream.FlushAsync();
                continue;
            }

            bool LoginOrRegister = loginData.lr; // True if client wants to login, false if client wants to register register
            string username = loginData.un;
            string hashedPassword = loginData.pw;

            if (LoginOrRegister == true) // Runs if client wants to login
            {
                if (database.LoginUser(username, hashedPassword)) // Checks if username and password exists in the database
                {
                    Console.WriteLine("Login of client successful");

                    // Send a response back to the client
                    string response = "1"; // Response 1 means the username/password were accepted
                    byte[] messageByte = Encoding.ASCII.GetBytes($"#{response.Length}#" + response); // Adds the length of the message
                    await authenticationStream.WriteAsync(messageByte, 0, messageByte.Length);

                    // Accepts connection
                    CheckForFreeSlots(client, username);
                    break;
                }
                else // Rejects
                {
                    Console.WriteLine("Client entered wrong username or password");

                    // Send a response back to the client
                    string response = "2"; // Response 2 means the username/password were rejected
                    byte[] messageByte = Encoding.ASCII.GetBytes($"#{response.Length}#" + response); // Adds the length of the message
                    await authenticationStream.WriteAsync(messageByte, 0, messageByte.Length);

                    // Rejects connection
                    await authenticationStream.FlushAsync();
                    //client.Close();
                    continue;
                }
            }
            else // Runs if client wants to register
            {
                if (username.Length > 16) // Checks if username is longer than 16 characters
                {
                    Console.WriteLine("Client's chosen username is too long");
                    string response = "2"; // Response 2 means username is too long
                    byte[] messageByte = Encoding.ASCII.GetBytes($"#{response.Length}#" + response); // Adds the length of the message
                    await authenticationStream.WriteAsync(messageByte, 0, messageByte.Length);
                }
                else if (database.RegisterUser(username, hashedPassword, clientIpAddress)) // Runs if registration was succesful
                {
                    Console.WriteLine("Registration of client was successful");
                    string response = "1"; // Response 1 means registration was successful
                    byte[] messageByte = Encoding.ASCII.GetBytes($"#{response.Length}#" + response); // Adds the length of the message
                    await authenticationStream.WriteAsync(messageByte, 0, messageByte.Length);

                    CheckForFreeSlots(client, username);
                    break;
                }
                else
                {
                    Console.WriteLine("Client's chosen username is already taken");
                    string response = "3"; // Response 3 means username is already taken
                    byte[] messageByte = Encoding.ASCII.GetBytes($"#{response.Length}#" + response); // Adds the length of the message
                    await authenticationStream.WriteAsync(messageByte, 0, messageByte.Length);
                }
            }
            await authenticationStream.FlushAsync();
        }
    }
    void CheckForFreeSlots(TcpClient client, string username)
    {
        IPEndPoint tcpEndPoint = (IPEndPoint)client.Client.RemoteEndPoint;
        string clientIpAddress = (tcpEndPoint).Address.ToString(); // Gets the IP address of the accepted

        database.UpdateLastLoginIP(username, clientIpAddress);

        //ClientAccepted(client, iPEndPoint); // skips searching for slot

        int index = dataProcessing.FindSlotForClientTCP(tcpClient, client); // Find an available slot for the new client

        if (index != -1) // Runs if there are free slots
        {
            ClientAccepted(client, index);
        }
        else // Reject the connection if all slots are occupied
        {
            client.Close();
        }
    }
    void ClientAccepted(TcpClient client, int index)
    {
        dataProcessing.AddNewClientToPlayersList(index); // Assings new client to list managing player position


        //SendInitialData(client, index);
        SendInitialData(client, index);

        Task.Run(() => ReceiveDataTCP(client, index)); // Creates new async func to handle receiving data from the new client
    }
    void SendInitialData(TcpClient client, int index) // Sends the client the initial stuff
    {
        try
        {
            NetworkStream sendingStream = client.GetStream();

            InitialData initialData = new InitialData();
            initialData.i = index; // Prepares sending client's own index to new client
            initialData.mp = maxPlayers; // Prepares sending maxplayers amount to new client

            string jsonData = JsonSerializer.Serialize(initialData, InitialDataContext.Default.InitialData);
            byte[] messageByte = Encoding.ASCII.GetBytes($"#{jsonData.Length}#" + jsonData); // Adds the length of the message
            sendingStream.Write(messageByte, 0, messageByte.Length);


        }
        catch (Exception ex)
        {
            Console.Out.WriteLine(ex.ToString());
        }
    }
    async Task ReceiveDataTCP(TcpClient client, int index) // One such async task is created for each client
    {
        NetworkStream receivingStream = client.GetStream();
        while (true)
        {
            try
            {
                byte[] receivedBytes = new byte[1024];
                int bytesRead;
                bytesRead = await receivingStream.ReadAsync(receivedBytes, 0, receivedBytes.Length);
                string receivedData = dataProcessing.ByteToStringWithFix(receivedBytes, bytesRead); // Converts byte to string and tries to fix if multiple packets were read as one

                Player clientPlayer = JsonSerializer.Deserialize(receivedData, PlayerContext.Default.Player);
                dataProcessing.ProcessPositionOfClients(index, clientPlayer);
                //Console.WriteLine("X: " + clientPlayer.x + ", Y: " + clientPlayer.y + ", Z: " + clientPlayer.z);

                //await receivingStream.FlushAsync();


            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error receiving TCP message from client index {index}. Exception: {ex.Message}");
                //ClientDisconnected(index); // Disconnects client
                break;
            }
            //Thread.Sleep(100);
        }
    }

    async Task SendDataTCP()
    {
        while (true)
        {
            foreach (TcpClient client in tcpClient)
            {
                if (client != null)
                {
                    int index = Array.IndexOf(tcpClient, client);
                    try
                    {
                        NetworkStream sendingStream = client.GetStream();
                        StreamReader reader = new StreamReader(sendingStream);

                        string jsonData = JsonSerializer.Serialize(dataProcessing.players, PlayersContext.Default.Players);
                        byte[] messageByte = Encoding.ASCII.GetBytes($"#{jsonData.Length}#" + jsonData); // Adds the length to the beginning of message
                        await sendingStream.WriteAsync(messageByte, 0, messageByte.Length);
                        //await sendingStream.FlushAsync();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error sending TCP message to client index {index}. Exception: {ex.Message}");
                        //ClientDisconnected(index); // Disconnects client
                    }
                }
            }
            //dataProcessing.PrintConnectedClients();
            Thread.Sleep(50);
        }
    }

    void ClientDisconnected(int index)
    {
        tcpClient[index].Close(); // Closes connection to the client
        tcpClient[index] = null; // Deletes it from list of clients
        dataProcessing.DeleteDisconnectedPlayer(index); // Deletes client data from data
        Console.WriteLine($"Client index {index} has disconnected.");
    }
}