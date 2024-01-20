using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

public class Server
{
    private TcpListener tcpListener;
    private TcpClient[] clients;
    private ServerData serverData;
    
    private int messageNumber = 0;
    
    public Server(int port)
    {
        try
        {
            // Starts server
            tcpListener = new TcpListener(IPAddress.Any, port);
            
            // Starts stuff that handles the clients
            clients = new TcpClient[2];
            
            // The thing that manages data about clients
            serverData = new ServerData();
            
            // Opens the thing that accepts new clients
            Thread listenerThread = new Thread(new ThreadStart(ListenForClients));
            listenerThread.Start();

            // Starts timer that broadcasts messages to all clients every given time
            Timer broadcastTimer = new Timer(state => Tick(), null, 0, 50);

            Console.Write("Server Started");
        }
        catch (Exception ex)
        {
            Console.Write("Server failed to start with exception: " + ex);
        }
    }

    private void ListenForClients()
    {
        // TcpListener tcpListener = (TcpListener)tcpListenerObj;
        tcpListener.Start();
        while (true)
        {
            try
            {
                // Blocks/waits until a client has connected to the server
                TcpClient client = tcpListener.AcceptTcpClient();
                
                // Gets the IP address of the client
                string clientIpAddress = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();
                
                // Prints info about connected client
                Console.WriteLine($"Client connecting from {clientIpAddress}");

                // Find an available slot for the client
                int index = FindSlotForClient();

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
        }
    }
    private void Tick()
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
                        ClientDisconnected(index);
                    }
                }
            }
        }
        messageNumber += 1;
    }

    private int FindSlotForClient()
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

    private void ClientDisconnected(int index)
    {
        clients[index] = null;
        serverData.DeleteDisconnectedPlayer(index);
        Console.WriteLine("Disconnected client: " + index);
    }
}