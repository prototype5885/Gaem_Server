using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace TCP_Gaem_Server.src;
class Server
{
    TcpListener tcpListener;
    TcpClient[] clients = new TcpClient[100];


    Thread listenerThread;
    Timer broadcastTimer;
    //object lockObject = new object();

    int messageNumber = 0;

    int port = 1942;

    ServerData serverData = new ServerData();


    public Server()
    {
        tcpListener = new TcpListener(IPAddress.Any, port);
        listenerThread = new Thread(new ThreadStart(ListenForClients));
        listenerThread.Start();

        // Timer for broadcasting messages to all clients every second
        broadcastTimer = new Timer(Tick, null, 0, 50);
    }

    void ListenForClients()
    {
        tcpListener.Start();

        while (true)
        {
            try
            {
                // Blocks until a client has connected to the server
                TcpClient client = tcpListener.AcceptTcpClient();


                // Find an available slot for the client
                int index = FindClientSlot();

                // Assings to data
                serverData.AddConnectedPlayer(index, "wtf");

                // Prints info about connected client
                Console.WriteLine($"Client index {index} connected from {((IPEndPoint)client.Client.RemoteEndPoint).Address}");

                if (index != -1)
                {
                    //lock (lockObject)
                    {
                        clients[index] = client;
                    }

                    // Create a thread to handle communication with the connected client
                    Thread clientThread = new Thread(new ParameterizedThreadStart(HandleClientComm));
                    clientThread.Start(index);
                }
                else
                {
                    // Reject the connection if all slots are occupied
                    Console.WriteLine("Connection rejected: Maximum number of clients reached.");
                    client.Close();
                }
            }
            catch (SocketException ex)
            {
                // Handle socket exceptions (e.g., client disconnected)
                Console.WriteLine($"SocketException: {ex.Message}");
            }
            catch (Exception ex)
            {
                // Handle other exceptions
                Console.WriteLine($"Exception: {ex.Message}");
            }

        }
    }

    void HandleClientComm(object clientIndex)
    {
        int index = (int)clientIndex;
        TcpClient tcpClient = clients[index];

        try
        {
            while (true)
            {
                //NetworkStream clientStream = tcpClient.GetStream();



                Thread.Sleep(100);
            }
        }
        catch (Exception ex)
        {
            // Handle exceptions that may occur when interacting with the client
            Console.WriteLine($"Client {index} disconnected. Exception: {ex.Message}");
            //lock (lockObject)
            {
                ClientDisconnected(index);
                Console.WriteLine("deleted");
            }
        }

    }

    void Tick(object state)
    {
        //lock (lockObject)
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


                        //Send a message to each connected client at the exact same time every second
                        byte[] message = Encoding.ASCII.GetBytes(messageNumber.ToString());
                        networkStream.Write(message, 0, message.Length);
                    }
                    catch (Exception ex)
                    {
                        // Handle exceptions that may occur when sending messages
                        Console.WriteLine($"Error sending message to a client. Exception: {ex.Message}");
                        int index = Array.IndexOf(clients, client);
                        ClientDisconnected(index);
                    }
                }
            }
        }
        messageNumber += 1;
    }

    int FindClientSlot()
    {
        //lock (lockObject)
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
        clients[index] = null;
        serverData.DeleteDisconnectedPlayer(index);
        Console.WriteLine("Disconnected client: " + index);
    }
}