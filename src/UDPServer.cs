using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using System.Net.Http;


public class UDPServer
{
    int maxPlayers;

    UdpClient udpServer; // Creates UDP Server

    IPEndPoint[] udpClients; // List of UDP clients


    DataProcessing dataProcessing; // Object that deals with managing players and fixing received packets

    TCPServer tcpServer;

    public UDPServer(int maxPlayers, DataProcessing dataProcessing, TCPServer tcpServer)
    {
        this.tcpServer = tcpServer;
        this.maxPlayers = maxPlayers;
        this.dataProcessing = dataProcessing;

        udpServer = new UdpClient(new IPEndPoint(IPAddress.Any, 1943)); // Starts the UDP server
        udpClients = new IPEndPoint[maxPlayers]; // Sets the max amount of UDP clients

        //for (int i = 0; i < udpClients.Length; i++)
        //{
        //    udpClients[i] = new IPEndPoint(0, 0);
        //}
    }
    public void StartUDPServer()
    {
        Task.Run(() => ReceiveDataUDP());
        Task.Run(() => SendDataUDP());

        Console.WriteLine("UDP Server is listening on port {0}...", 1943);
    }
    async Task ReceiveDataUDP()
    {
        //IPEndPoint remoteEP;
        while (true)
        {
            try
            {
                UdpReceiveResult data = await udpServer.ReceiveAsync();

                string message = Encoding.UTF8.GetString(data.Buffer);
                //await Console.Out.WriteLineAsync(message);

                Player clientPlayer = JsonSerializer.Deserialize(message, PlayerContext.Default.Player);
                dataProcessing.ProcessPositionOfClients(0, clientPlayer);

                //remoteEP = data.RemoteEndPoint;
                //await Console.Out.WriteLineAsync($"Message: {message}, from {remoteEP}");
            }
            catch
            {
                //await Console.Out.WriteLineAsync("failed receiving udp data");
            }

        }
    }
    async Task SendDataUDP()
    {
        while (true)
        {
            foreach (IPEndPoint clientEndPoint in udpClients)
            {
                if (clientEndPoint != null)
                {
                    int index = Array.IndexOf(udpClients, clientEndPoint);
                    try
                    {
                        string jsonData = JsonSerializer.Serialize(dataProcessing.players, PlayersContext.Default.Players);
                        byte[] sentMessage = Encoding.ASCII.GetBytes(jsonData);
                        await udpServer.SendAsync(sentMessage, sentMessage.Length, clientEndPoint);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error sending UDP message to client index {index}. Exception: {ex.Message}");

                        await Console.Out.WriteLineAsync(index.ToString()); // Disconnects client
                    }
                }
            }
            //dataProcessing.PrintConnectedClients();
            Thread.Sleep(50);
        }
    }

}

