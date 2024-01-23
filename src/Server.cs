using Newtonsoft.Json;
using System;
using System.Net;
using System.Net.Sockets;
using System.Reflection.PortableExecutable;
using System.Text;

public class Server
{
    TcpClient[] clients = new TcpClient[10]; // Creates thing that handles list of clients

    ServerData serverData = new ServerData(); // Creates thing that handles data of each client

    string[] ipAddresses = new string[10]; // String array of clients' ip addresses

    bool ipAuthentication = false;
    bool userAuthentication = true;

    //int messageNumber = 0;

    string messageToBroadcast = "";

    public void StartServer()
    {
        try
        {
            //Task.Run(() => WaitingForNewClients()); // Waiting for clients to connect in async
            WaitingForNewClients();
            //Thread.Sleep(100);
            //Task.Run(() => ReceiveData());

            //Timer broadcastTimer = new Timer(state => Tick(), null, 0, 50); // Starts timer that broadcasts messages to all clients every given time
            //Thread.Sleep(Timeout.Infinite);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Server failed to start with exception: " + ex);
        }
    }
    void WaitingForNewClients()
    {
        try
        {
            TcpListener tcpListener = new TcpListener(IPAddress.Any, 1942); // Sets server address

            tcpListener.Start(); // Starts the server

            Task.Run(() => SendingData()); // Starts the async task that handles sending data to each client

            Console.WriteLine("Server started, waiting for clients to connect...");

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
                if (userAuthentication)
                {
                    Task.Run(() => Authentication(client));
                }
                else
                {
                    ClientAccepted(client);
                }
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
    async Task Authentication(TcpClient client) // Authenticate the client
    {
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
                Console.WriteLine("Failed to read autentication of client");
                break;
            }

            if (bytesRead == 0)
                break;

            string receivedData = Encoding.UTF8.GetString(message, 0, bytesRead); // Converts the received bytes to string

            Credentials credentials = JsonConvert.DeserializeObject<Credentials>(receivedData); // Converts received json format data to username and password

            if (credentials.un == "user" && credentials.pw == "password") // Checks if username and password exists in the database
            {
                Console.WriteLine("Authentication successful");

                // Send a response back to the client
                byte[] data = Encoding.UTF8.GetBytes("1"); // Response 1 means the username/password were accepted
                await authenticationStream.WriteAsync(data, 0, data.Length);

                // Accepts connection
                ClientAccepted(client);
                break;
            }
            else // Rejects
            {
                Console.WriteLine("User entered wrong username or password");

                // Send a response back to the client
                byte[] data = Encoding.UTF8.GetBytes("0"); // Response 0 means the username/password were rejected
                await authenticationStream.WriteAsync(data, 0, data.Length);

                // Rejects connection
                authenticationStream.Flush();
                //client.Close();
                continue;
            }
        }
    }
    void ClientAccepted(TcpClient client)
    {
        string clientIpAddress = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString(); // Gets the IP address of the accepted

        int index = FindSlotForClient(); // Find an available slot for the new client

        if (index != -1) // Runs if there are free slots
        {
            clients[index] = client; // Adds new client to a slot

            Console.WriteLine($"Assigned index {index} for {clientIpAddress}");

            serverData.AddConnectedPlayer(index, "wtf"); // Assings new client to data manager

            ipAddresses[index] = clientIpAddress; // Adds the client to the array of connected clients' ip addresses

            Task.Run(() => ReceivingData(client)); // Creates new async func to receive data from the new client
        }
        else // Reject the connection if all slots are occupied
        {
            Console.WriteLine($"Connection rejected for {clientIpAddress}: Maximum number of clients reached. ");
            client.Close();
        }
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


                Console.WriteLine($"{index} Received from client: {receivedData}");

                receivingStream.Flush();
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
        Console.WriteLine("Waiting for data from users...");
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

                        byte[] sentMessage = Encoding.ASCII.GetBytes("XD");
                        await sendingStream.WriteAsync(sentMessage, 0, sentMessage.Length);

                        sendingStream.Flush();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error sending message to client index {index}. Exception: {ex.Message}");
                        ClientDisconnected(index); // Disconnects client
                    }
                }
            }
            //messageNumber += 1;
            Thread.Sleep(500);
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
        serverData.DeleteDisconnectedPlayer(index); // Deletes client data from data
        Console.WriteLine($"Client index {index} has disconnected.");
    }
}
public class Credentials
{
    public string un { get; set; }
    public string pw { get; set; }
}