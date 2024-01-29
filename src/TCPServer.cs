using System;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Timers;


public class TCPServer
{
    int maxPlayers;

    public TcpClient[] tcpClients; // List of TCP clients

    Database database = new Database(); // Creates database object

    DataProcessing dataProcessing; // Object that deals with managing players and fixing received packets

    public TCPServer(int maxPlayers, DataProcessing dataProcessing)
    {
        this.maxPlayers = maxPlayers;
        this.dataProcessing = dataProcessing;
        tcpClients = new TcpClient[maxPlayers];

    }
    public void StartTCPServer()
    {
        try
        {
            TcpListener tcpListener = new TcpListener(IPAddress.Any, 1942); // Sets server address
            tcpListener.Start(); // Starts the server
            Console.WriteLine("TCP Server is listening on port {0}...", 1942);

            Task.Run(() => WaitingForNewClients(tcpListener)); // Waiting for clients to connect
            Task.Run(() => SendDataTCP()); // Starts the async task that handles sending data to each client
        }
        catch (Exception ex)
        {
            Console.WriteLine("Server failed to start with exception: " + ex);
        }
    }
    async Task WaitingForNewClients(TcpListener tcpListener)
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

                //Task.Run(() => Authentication(tcpClient, clientIpAddress)); // Proceeds to check the authentication of the connecting client
                await Authentication(tcpClient, clientIpAddress);
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

        byte[] message = new byte[1024];
        int bytesRead;

        while (true)
        {
            bytesRead = 0;
            try
            {
                bytesRead = await authenticationStream.ReadAsync(message, 0, message.Length); // Waits for new client to send username and password
            }
            catch
            {
                Console.WriteLine("Failed to authenticate client, most likely disconnected.");
                break;
            }

            if (bytesRead == 0)
                break;
            string receivedData = Encoding.ASCII.GetString(message, 0, bytesRead); // Converts the received bytes to string

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
                    byte[] data = Encoding.ASCII.GetBytes("1"); // Response 1 means the username/password were accepted
                    await authenticationStream.WriteAsync(data, 0, data.Length);

                    // Accepts connection
                    CheckForFreeSlots(client, username);
                    break;
                }
                else // Rejects
                {
                    Console.WriteLine("User entered wrong username or password");

                    // Send a response back to the client
                    byte[] data = Encoding.ASCII.GetBytes("0"); // Response 0 means the username/password were rejected
                    await authenticationStream.WriteAsync(data, 0, data.Length);

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
                    byte[] data = Encoding.ASCII.GetBytes("2"); // Response to client, 2 means username is too long
                    await authenticationStream.WriteAsync(data, 0, data.Length);
                }
                else if (database.RegisterUser(username, hashedPassword, clientIpAddress)) // Runs if registration was succesful
                {
                    Console.WriteLine("Registration of client was successful");
                    byte[] data = Encoding.ASCII.GetBytes("1"); // Response to client, 1 means registration was successful
                    await authenticationStream.WriteAsync(data, 0, data.Length);

                    CheckForFreeSlots(client, username);
                    break;
                }
                else
                {
                    Console.WriteLine("Client's chosen username is already taken");
                    byte[] data = Encoding.ASCII.GetBytes("3"); // Response to client, 3 means username is already taken
                    await authenticationStream.WriteAsync(data, 0, data.Length);
                }
            }
            await authenticationStream.FlushAsync();
        }
    }
    void CheckForFreeSlots(TcpClient client, string username)
    {
        IPEndPoint udpClient = (IPEndPoint)client.Client.RemoteEndPoint;
        string clientIpAddress = (udpClient).Address.ToString(); // Gets the IP address of the accepted

        database.UpdateLastLoginIP(username, clientIpAddress);

        //ClientAccepted(client, iPEndPoint); // skips searching for slot

        int index = dataProcessing.FindSlotForClient(tcpClients); // Find an available slot for the new client

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
        tcpClients[index] = client; // Adds new client to list of tcp clients

        SendInitialData(client, index);

        Task.Run(() => ReceiveDataTCP(client, index)); // Creates new async func to handle receiving data from the new client
    }
    void SendInitialData(TcpClient client, int index) // Sends the client the initial stuff
    {
        NetworkStream sendingStream = client.GetStream();

        InitialData initialData = new InitialData();
        initialData.i = index; // Prepares sending client's own index to new client
        initialData.mp = maxPlayers; // Prepares sending maxplayers amount to new client

        string jsonData = JsonSerializer.Serialize(initialData, InitialDataContext.Default.InitialData);

        byte[] sentMessage = Encoding.ASCII.GetBytes(jsonData);

        sendingStream.Write(sentMessage, 0, sentMessage.Length);
        sendingStream.Flush();
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
                string receivedData = Encoding.ASCII.GetString(receivedBytes, 0, bytesRead);

                receivedData = dataProcessing.FixPacket(receivedData);

                Player clientPlayer = JsonSerializer.Deserialize(receivedData, PlayerContext.Default.Player);
                dataProcessing.ProcessPositionOfClients(index, clientPlayer);
                //Console.WriteLine("X: " + clientPlayerPosition.x + ", Y: " + clientPlayerPosition.y + ", Z: " + clientPlayerPosition.z);


                await receivingStream.FlushAsync();


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
            foreach (TcpClient client in tcpClients)
            {
                if (client != null)
                {
                    int index = Array.IndexOf(tcpClients, client);
                    try
                    {
                        NetworkStream sendingStream = client.GetStream();
                        StreamReader reader = new StreamReader(sendingStream);

                        string jsonData = JsonSerializer.Serialize(dataProcessing.players, PlayersContext.Default.Players);
                        //Console.WriteLine(jsonData);

                        byte[] sentMessage = Encoding.ASCII.GetBytes(jsonData);
                        await sendingStream.WriteAsync(sentMessage, 0, sentMessage.Length);
                        await sendingStream.FlushAsync();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error sending TCP message to client index {index}. Exception: {ex.Message}");
                        //ClientDisconnected(index); // Disconnects client
                    }
                }
            }
            //dataProcessing.PrintConnectedClients();
            Thread.Sleep(25);
        }
    }
    void ClientDisconnected(int index)
    {
        tcpClients[index].Close(); // Closes connection to the client
        tcpClients[index] = null; // Deletes it from list of clients
        dataProcessing.DeleteDisconnectedPlayer(index); // Deletes client data from data
        Console.WriteLine($"Client index {index} has disconnected.");
    }
}