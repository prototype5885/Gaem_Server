using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

public class Server
{    
    TcpClient[] clients = new TcpClient[10]; // Creates thing that handles list of clients

    ServerData serverData = new ServerData(); // Creates thing that handles data of each client

    int messageNumber = 0;

    public void StartServer()
    {
        try
        {
            Task.Run(() => WaitingForNewClients()); // Waiting for clients to connect in async

            //Timer broadcastTimer = new Timer(state => Tick(), null, 0, 50); // Starts timer that broadcasts messages to all clients every given time
            Thread.Sleep(Timeout.Infinite);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Server failed to start with exception: " + ex);
        }
    }
    async Task WaitingForNewClients()
    {
        TcpListener tcpListener = new TcpListener(IPAddress.Any, 1942); // Sets server address

        tcpListener.Start(); // Starts the server

        Console.WriteLine("Server started.");

        Task.Run(() => ReceiveSendData());
        try
        {
            while (true)
            {
                TcpClient client = tcpListener.AcceptTcpClient(); // Waits / blocks until a client has connected to the server

                string clientIpAddress = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString(); // Gets the IP address of the client

                Console.WriteLine($"Client connecting from {clientIpAddress}"); // Prints info about connected client

                int index = FindSlotForClient(); // Find an available slot for the client

                if (index != -1) // Assigns slot for the client
                {
                    clients[index] = client; // Adds to a slot if there is free one available
                    Console.WriteLine($"Assigned index {index} for {clientIpAddress}");
                    serverData.AddConnectedPlayer(index, "wtf"); // Assings client to data manager
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
    async Task ReceiveSendData()
    {
        while (true)
        {
            //Console.Clear();
            foreach (TcpClient client in clients)
            {
                //    //prints connected clients
                //    if (client == null)
                //    {
                //        Console.WriteLine("Empty");
                //    }
                //    else
                //    {
                //        Console.WriteLine(client);
                //    }

                // send message
                if (client != null)
                {
                    try
                    {
                        NetworkStream networkStream = client.GetStream();

                        //Handle the response from the client
                        byte[] response = new byte[1024];
                        int bytesRead;

                        bytesRead = await networkStream.ReadAsync(response, 0, response.Length);

                        while (bytesRead > 0)
                        {
                            string clientResponse = Encoding.ASCII.GetString(response, 0, bytesRead);
                            Console.WriteLine("Received from client: " + clientResponse);
                        }

  
                        byte[] sentMessage = Encoding.ASCII.GetBytes("0");
                        await networkStream.WriteAsync(sentMessage, 0, sentMessage.Length);


                        networkStream.Flush();
                    }
                    catch (Exception ex)
                    {
                        // Handle exceptions that may occur
                        Console.WriteLine($"Error sending message to a client. Exception: {ex.Message}");
                        int index = Array.IndexOf(clients, client);

                        ClientDisconnected(index); // Disconnect client
                    }
                }
            }
            //messageNumber += 1;
            Thread.Sleep(50);
        }
    }
    void SendMessageToClient()
    {

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