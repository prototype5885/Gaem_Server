using System.Net;
using System.Net.Sockets;
using System.Text;

public class Server
{
    private int messageNumber = 0;

    public void StartServer()
    {
        try
        {
            Task.Run(() => ListenForClients());

            Thread.Sleep(Timeout.Infinite);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Server failed to start with exception: " + ex);
        }
    }

    async Task ListenForClients()
    {
        // Sets server address
        TcpListener tcpListener = new TcpListener(IPAddress.Any, 1942);

        // Creates thing that handles list of clients
        TcpClient[] clients = new TcpClient[2];

        // Creates thing that handles data of each client
        ServerData serverData = new ServerData();

        // Starts the server
        tcpListener.Start();

        // Starts timer that broadcasts messages to all clients every given time
        Timer broadcastTimer = new Timer(state => Tick(clients, serverData), null, 0, 50);

        Console.WriteLine("Server started.");
        try
        {
            while (true)
            {
                Console.WriteLine("Waiting for client to connect...");

                // Blocks/waits until a client has connected to the server
                TcpClient client = tcpListener.AcceptTcpClient();

                //Gets the IP address of the client
                string clientIpAddress = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();

                // Prints info about connected client
                Console.WriteLine($"Client connecting from {clientIpAddress}");

                // Find an available slot for the client
                int index = FindSlotForClient(clients);

                // Assigns slot for the client
                if (index != -1)
                {
                    // Adds to slot if there is free one available
                    clients[index] = client;
                    Console.WriteLine($"Assigned index {index} for {clientIpAddress}");

                    // Assings client to data
                    serverData.AddConnectedPlayer(index, "wtf");
                }
                else
                {
                    // Reject the connection if all slots are occupied
                    Console.WriteLine($"Connection rejected for {clientIpAddress}: Maximum number of clients reached. ");
                    client.Close();
                }
            }
        }
        catch (SocketException ex)
        {
            // Handle socket exceptions
            Console.WriteLine($"SocketException: {ex.Message}");
        }
        catch (Exception ex)
        {
            // Handle other exceptions
            Console.WriteLine($"Exception: {ex.Message}");
        }
        finally
        {
            tcpListener.Stop();
        }
    }
    private void Tick(TcpClient[] clients, ServerData serverData)
    {
        {
            //Console.Clear();
            foreach (TcpClient client in clients)
            {
                // prints connected clients
                //if (client == null)
                //{
                //    Console.WriteLine("Empty");
                //}
                //else
                //{
                //    Console.WriteLine(client);
                //}

                // send message
                if (client != null)
                {
                    try
                    {
                        NetworkStream networkStream = client.GetStream();

                        //// receiving
                        //// Buffer to store the received data
                        //byte[] buffer = new byte[1024];

                        //// Read the data from the network stream
                        //int bytesRead = networkStream.Read(buffer, 0, buffer.Length);

                        //// Convert the received bytes to a string
                        //string receivedMessage = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        //Console.WriteLine($"Received message: {receivedMessage}");


                        // Send a message to each connected clients
                        byte[] message = Encoding.ASCII.GetBytes(messageNumber.ToString());
                        networkStream.Write(message, 0, message.Length);
                    }
                    catch (Exception ex)
                    {
                        // Handle exceptions that may occur
                        Console.WriteLine($"Error sending message to a client. Exception: {ex.Message}");
                        int index = Array.IndexOf(clients, client);
                        ClientDisconnected(index, clients, serverData);
                    }
                }
            }
        }
        messageNumber += 1;
    }

    private static int FindSlotForClient(TcpClient[] clients)
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

    private void ClientDisconnected(int index, TcpClient[] clients, ServerData serverData)
    {
        clients[index] = null;
        serverData.DeleteDisconnectedPlayer(index);
        Console.WriteLine("Disconnected client: " + index);
    }
}