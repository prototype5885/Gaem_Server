using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Xml;
using System.Text.Json;
using static System.Runtime.InteropServices.JavaScript.JSType;



public class ServerUDP
{
    int maxPlayers;

    UdpClient udpServer;
    IPEndPoint[] connectedUdpClient;

    DataProcessing dataProcessing;

    public ServerUDP(int maxPlayers, DataProcessing dataProcessing)
    {
        this.maxPlayers = maxPlayers;
        this.dataProcessing = dataProcessing;

        StartUDPServer();
    }

    public void StartUDPServer()
    {
        try
        {
            int port = 1943;
            udpServer = new UdpClient(port);
            connectedUdpClient = new IPEndPoint[maxPlayers];
            Console.WriteLine($"UDP Server is listening on port {port}...");

            Task.Run(() => ReceiveDataUDP());

            Thread.Sleep(Timeout.Infinite);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Server failed to start with exception: " + ex);
        }
    }
    void ClientAccepted(IPEndPoint client, int index)
    {
        dataProcessing.AddNewClientToPlayersList(index); // Assings new client to list managing player position

        SendInitialData(client, index);
    }
    void SendInitialData(IPEndPoint client, int index) // Sends the client the initial stuff
    {
        try
        {
            //NetworkStream sendingStream = client.GetStream();

            InitialData initialData = new InitialData();
            initialData.i = index; // Prepares sending client's own index to new client
            initialData.mp = maxPlayers; // Prepares sending maxplayers amount to new client

            string jsonData = JsonSerializer.Serialize(initialData, InitialDataContext.Default.InitialData);
            //byte[] messageByte = Encoding.ASCII.GetBytes($"#{jsonData.Length}#" + jsonData); // Adds the length of the message
            //sendingStream.Write(messageByte, 0, messageByte.Length);

            byte[] messageByte = Encoding.UTF8.GetBytes($"#{jsonData.Length}#" + jsonData);
            udpServer.Send(messageByte, messageByte.Length, client);


        }
        catch (Exception ex)
        {
            Console.Out.WriteLine(ex.ToString());
        }
    }
    async Task ReceiveDataUDP()
    {
        try
        {
            while (true)
            {
                IPEndPoint clientEndpoint = new IPEndPoint(IPAddress.Any, 0);
                UdpReceiveResult data = await udpServer.ReceiveAsync();
                string message = Encoding.UTF8.GetString(data.Buffer);
                await Console.Out.WriteLineAsync(message);

                switch (message)
                {
                    case "iWantToConnect":
                        int clientIndex = FindSlotForClientUDP(connectedUdpClient, data.RemoteEndPoint);
                        await Console.Out.WriteLineAsync("Replying...");
                        if (clientIndex != -1) // Replies that the client was accepted
                        {
                            byte[] messageByte = Encoding.UTF8.GetBytes("1");
                            udpServer.Send(messageByte, messageByte.Length, data.RemoteEndPoint);
                            ClientAccepted(data.RemoteEndPoint, clientIndex);
                        }
                        else // Replies that the client was rejected
                        {
                            byte[] messageByte = Encoding.UTF8.GetBytes("0");
                            udpServer.Send(messageByte, messageByte.Length, data.RemoteEndPoint);
                        }
                        break;
                }

            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
    int FindSlotForClientUDP(IPEndPoint[] connectedUdpClient, IPEndPoint clientEndPoint)
    {
        for (int i = 0; i < connectedUdpClient.Length; i++)
        {
            if (connectedUdpClient[i] == null)
            {
                connectedUdpClient[i] = clientEndPoint;
                Console.WriteLine($"Assigned index slot {i}");
                return i;
            }
        }
        Console.WriteLine($"Connection rejected: Maximum number of clients reached. ");
        return -1; // No available slot
    }
}

