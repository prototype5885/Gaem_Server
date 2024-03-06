using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;


public static class PacketProcessor
{
    public static async Task SendTcp(byte commandType, string message, NetworkStream stream)
    {
        byte[] messageBytes = EncodeMessage(commandType, message);
        CalculateLatency.sentBytesPerSecond += messageBytes.Length;
        await stream.WriteAsync(messageBytes);
    }
    public static async Task SendUdp(byte commandType, string message, EndPoint udpEndpoint)
    {
        byte[] messageBytes = EncodeMessage(commandType, message);
        CalculateLatency.sentBytesPerSecond += messageBytes.Length;
        await Server.serverUdpSocket.SendToAsync(messageBytes, SocketFlags.None, udpEndpoint);
    }
    public static async Task ReceiveTcpData(ConnectedPlayer connectedClient)
    {
        try
        {
            CancellationToken cancellationToken = connectedClient.cancellationTokenSource.Token;

            Byte[] buffer = new Byte[1024];
            int bytesRead;
            while (!cancellationToken.IsCancellationRequested)
            {
                bytesRead = await connectedClient.tcpStream.ReadAsync(new ArraySegment<byte>(buffer), cancellationToken);
                CalculateLatency.receivedBytesPerSecond += bytesRead;
                Packet[] packets = ProcessBuffer(buffer, bytesRead);
                ProcessPackets(packets, connectedClient);
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine($"Receiving task for client id {Array.IndexOf(Server.connectedPlayers, connectedClient)} was cancelled");
        }
        catch (IOException ex) when (ex.InnerException is SocketException socketEx && socketEx.SocketErrorCode == SocketError.ConnectionReset)
        {
            // Handle sudden client disconnect (ConnectionReset)
            Console.WriteLine($"Client disconnected abruptly: {connectedClient.ipAddress}");
            PlayersManager.DisconnectClient(connectedClient);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
    }
    public static async Task ReceiveUdpData()
    {
        try
        {
            byte[] buffer = new byte[1024];
            int bytesRead;
            while (true)
            {
                EndPoint udpEndPoint = new IPEndPoint(IPAddress.Any, 0);

                bytesRead = await Server.serverUdpSocket.ReceiveAsync(new ArraySegment<byte>(buffer), SocketFlags.None);
                CalculateLatency.receivedBytesPerSecond += bytesRead;

                ConnectedPlayer connectedPlayer = Authentication.CheckAuthenticationOfUdpClient(udpEndPoint);
                if (connectedPlayer != null)
                {
                    Packet[] packets = ProcessBuffer(buffer, bytesRead);
                    ProcessPackets(packets, connectedPlayer);
                }
            }
        }
        catch
        {
            Console.WriteLine("Error receiving UDP packet");
        }
    }
    public static Packet[] ProcessBuffer(byte[] buffer, int byteLength) // processes bytes received from the buffer
    {
        string receivedBytesInString = string.Empty; // creates this empty string for to be used later

        if (Encryption.encryption) // runs if encryption is enabled
        {
            byte[] receivedBytes = new byte[byteLength];
            Array.Copy(buffer, receivedBytes, byteLength);

            receivedBytesInString = Encryption.Decrypt(receivedBytes);
        }
        else // runs if encryption is disabled
        {
            receivedBytesInString = Encoding.ASCII.GetString(buffer, 0, byteLength);
        }

        //Console.WriteLine(receivedBytesInString);

        string packetTypePattern = @"#(.*)#"; // pattern to read the packet type
        string packetDataPattern = @"\$(.*?)\$"; // pattern to read the packet data

        MatchCollection packetTypeMatches = Regex.Matches(receivedBytesInString, packetTypePattern);
        MatchCollection packetDataMatches = Regex.Matches(receivedBytesInString, packetDataPattern);

        Packet[] packets = new Packet[packetTypeMatches.Count];
        for (byte i = 0; i < packetTypeMatches.Count; i++) // saves all the packets found in the buffer
        {
            byte.TryParse(packetTypeMatches[i].Groups[1].Value, out byte typeOfPacket);

            Packet packet = new Packet();
            packet.type = typeOfPacket;
            packet.data = packetDataMatches[i].Groups[1].Value;

            packets[i] = packet;
        }
        return packets;
    }
    static void ProcessPackets(Packet[] packets, ConnectedPlayer connectedPlayer)
    {
        foreach (Packet packet in packets)
        {
            ProcessDataSentByPlayer(packet, connectedPlayer);
        }
    }
    static void ProcessDataSentByPlayer(Packet packet, ConnectedPlayer connectedClient)
    {
        switch (packet.type)
        {
            // Type 0 means client answers the ping
            case 0:
                connectedClient.udpPingAnswered = true;
                connectedClient.status = 1;
                PlayersManager.CalculatePlayerLatency(connectedClient);
                break;
            // Type 2 means client is sending a chat message
            case 2:
                Console.WriteLine(packet.data);
                break;
            // Type 3 means client is sending its own position to the server
            case 3:
                PlayerPosition clientPlayerPosition = JsonSerializer.Deserialize(packet.data, PlayerPositionContext.Default.PlayerPosition);
                connectedClient.position = clientPlayerPosition;
                break;
        }
    }
    static byte[] EncodeMessage(byte commandType, string message)
    {
        if (Encryption.encryption)
        {
            return Encryption.Encrypt($"#{commandType}#${message}$");
        }
        else
        {
            return Encoding.ASCII.GetBytes($"#{commandType}#${message}$");
        }
    }
}

