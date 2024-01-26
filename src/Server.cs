//using Newtonsoft.Json;
using System;
using System.Net;
using System.Net.Sockets;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;


public class Server
{
    TcpClient[] clients = new TcpClient[10]; // Creates thing that handles list of clients
    Database database = new Database(); // Creates database
    DataProcessing dataProcessing = new DataProcessing();
    string[] ipAddresses = new string[10]; // String array of clients' ip addresses

    bool ipAuthentication = false;

    public void StartServer()
    {
        try
        {
            TcpListener tcpListener = new TcpListener(IPAddress.Any, 1942); // Sets server address
            tcpListener.Start(); // Starts the server
            Task.Run(() => SendingData()); // Starts the async task that handles sending data to each client
            WaitingForNewClients(tcpListener);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Server failed to start with exception: " + ex);
        }
    }
    void WaitingForNewClients(TcpListener tcpListener)
    {
        Console.WriteLine("Server started, waiting for clients to connect...");
        try
        {
            while (true)
            {
                TcpClient client = tcpListener.AcceptTcpClient(); // Waits/blocks until a client has connected to the server
                string clientIpAddress = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString(); // Gets the IP address of the new client
                Console.WriteLine($"Client connecting from {clientIpAddress}"); // Prints info about the new client

                if (ipAuthentication)
                {
                    bool ipAlreadyConnected = false;
                    foreach (string ipAddress in ipAddresses) // Rejects connection if a client tries to connect from same ip address multiple times
                    {
                        if (ipAddress == clientIpAddress)
                        {
                            Console.WriteLine($"Connection rejected for {clientIpAddress}: A client with same IP address is already connected.");
                            client.Close();
                            ipAlreadyConnected = true;
                        }
                    }
                    if (ipAlreadyConnected) // Restarts the while loop if ip is already connected
                    {
                        continue;
                    }
                }

                // Proceeds to check the authentication of the connecting client
                Task.Run(() => Authentication(client, clientIpAddress));
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

        byte[] message = new byte[128];
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
        string clientIpAddress = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString(); // Gets the IP address of the accepted

        database.UpdateLastLoginIP(username, clientIpAddress);

        int index = FindSlotForClient(); // Find an available slot for the new client

        if (index != -1) // Runs if there are free slots
        {
            clients[index] = client; // Adds new client to a slot
            Console.WriteLine($"Assigned index slot {index} for {clientIpAddress}");
            ClientAccepted(client, index, clientIpAddress);
        }
        else // Reject the connection if all slots are occupied
        {
            Console.WriteLine($"Connection rejected for {clientIpAddress}: Maximum number of clients reached. ");
            client.Close();
        }
    }
    void ClientAccepted(TcpClient client, int index, string clientIpAddress)
    {
        dataProcessing.AddNewClient(index); // Assings new client to data manager
        ipAddresses[index] = clientIpAddress; // Adds the client to the array of connected clients' ip addresses

        Task.Run(() => ReceivingData(client)); // Creates new async func to receive data from the new client
    }
    async Task ReceivingData(TcpClient client) // One such async task is created for each client
    {
        NetworkStream receivingStream = client.GetStream();
        int index = Array.IndexOf(clients, client);
        while (true)
        {
            try
            {
                byte[] receivedBytes = new byte[1024];
                int bytesRead;
                bytesRead = await receivingStream.ReadAsync(receivedBytes, 0, receivedBytes.Length);
                string receivedData = Encoding.ASCII.GetString(receivedBytes, 0, bytesRead);



                // some workaround so the data isnt being read duplicated
                int indexOfSpecificCharacter = receivedData.IndexOf("}");
                if (indexOfSpecificCharacter != -1)
                {
                    receivedData = receivedData.Substring(0, indexOfSpecificCharacter + 1);
                }
                else
                {
                    continue;
                }
                // end of workaround

                try
                {
                    LocalPlayerPosition localPlayerPosition = JsonSerializer.Deserialize(receivedData, LocalPlayerPositionContext.Default.LocalPlayerPosition);
                    dataProcessing.ProcessData(index, localPlayerPosition);
                    dataProcessing.PrintConnectedClients();

                }
                catch (Exception ex)
                {
                    Console.WriteLine($"{ex.Message}");
                }
                await receivingStream.FlushAsync();


            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error receiving message from client index {index}. Exception: {ex.Message}");
                ClientDisconnected(index); // Disconnects client
                break;
            }
        }
    }
    async Task SendingData()
    {
        while (true)
        {
            foreach (TcpClient client in clients)
            {
                if (client != null)
                {
                    int index = Array.IndexOf(clients, client);
                    try
                    {
                        NetworkStream sendingStream = client.GetStream();
                        StreamReader reader = new StreamReader(sendingStream);

                        //string jsonData = JsonSerializer.Serialize(dataProcessing.everyPlayersPosition, EveryPlayerPositionContext.Default.EveryPlayerPosition);

                        byte[] sentMessage = Encoding.ASCII.GetBytes("xd");
                        await sendingStream.WriteAsync(sentMessage, 0, sentMessage.Length);
                        await sendingStream.FlushAsync();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error sending message to client index {index}. Exception: {ex.Message}");
                        ClientDisconnected(index); // Disconnects client
                    }
                }
            }
            Thread.Sleep(100);
        }
    }
    int FindSlotForClient()
    {
        {
            for (int i = 0; i < clients.Length; i++)
            {
                if (clients[i] == null)
                {
                    return i;
                }
            }
        }
        return -1; // No available slot
    }
    void ClientDisconnected(int index)
    {
        clients[index].Close(); // Closes connection to the client
        clients[index] = null; // Deletes it from list of clients
        ipAddresses[index] = null; // Deletes client ip address of disconnected client
        dataProcessing.DeleteDisconnectedPlayer(index); // Deletes client data from data
        Console.WriteLine($"Client index {index} has disconnected.");
    }
}