using System;
using System.Net;
using System.Net.Sockets;
using System.Reflection.PortableExecutable;
using System.Text;

public class Server
{
    TcpClient[] clients = new TcpClient[10]; // Creates thing that handles list of clients

    ServerData serverData = new ServerData(); // Creates thing that handles data of each client

    int messageNumber = 0;

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
                TcpClient client = tcpListener.AcceptTcpClient(); // Waits / blocks until a client has connected to the server

                string clientIpAddress = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString(); // Gets the IP address of the new client

                Console.WriteLine($"Client connecting from {clientIpAddress}"); // Prints info about the new client

                int index = FindSlotForClient(); // Find an available slot for the new client

                if (index != -1) // Adds new client to a slot if there is free one available
                {
                    clients[index] = client;

                    Console.WriteLine($"Assigned index {index} for {clientIpAddress}");

                    serverData.AddConnectedPlayer(index, "wtf"); // Assings new client to data manager

                    Task.Run(() => ReceivingData(client)); // Creates new async func to receive data from the new client

                }
                else // Reject the connection if all slots are occupied
                {
                    Console.WriteLine($"Connection rejected for {clientIpAddress}: Maximum number of clients reached. ");
                    client.Close();
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

                        byte[] sentMessage = Encoding.ASCII.GetBytes("");
                        await sendingStream.WriteAsync(sentMessage, 0, sentMessage.Length);

                        sendingStream.Flush();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error sending message to client index {index}. Exception: {ex.Message}");
                        ClientDisconnected(index); // Disconnect client
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
        clients[index].Close();
        clients[index] = null;
        serverData.DeleteDisconnectedPlayer(index);
        Console.WriteLine($"Client index {index} has disconnected.");
    }
}