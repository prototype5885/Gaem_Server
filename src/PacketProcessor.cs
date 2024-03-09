using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

public static class PacketProcessor
{
    public static async Task SendTcp(byte commandType, string message, ConnectedPlayer connectedPlayer)
    {
        try
        {
            byte[] messageBytes = EncodeMessage(commandType, message);
            Monitoring.sentBytesPerSecond += messageBytes.Length;
            await connectedPlayer.tcpStream.WriteAsync(messageBytes);
        }
        catch
        {
            Console.WriteLine($"Failed sending TCP data to ({connectedPlayer.playerName}), disconnecting player...");
            Authentication.DisconnectClient(connectedPlayer);
        }
    }
    public static async Task SendUdp(byte commandType, string message, ConnectedPlayer connectedPlayer)
    {
        if (connectedPlayer.udpEndpoint != null)
        {
            byte[] messageBytes = EncodeMessage(commandType, message);
            Monitoring.sentBytesPerSecond += messageBytes.Length;
            await Server.serverUdpSocket.SendToAsync(messageBytes, SocketFlags.None, connectedPlayer.udpEndpoint);
        }
    }
    public static async Task ReceiveTcpData(ConnectedPlayer connectedPlayer)
    {
        try
        {
            Console.WriteLine($"({DateTime.Now}) Listening to tcp data from player ({connectedPlayer.playerName})");
            CancellationToken cancellationToken = connectedPlayer.cancellationTokenSource.Token;
            
            Byte[] buffer = new Byte[4096];
            while (!cancellationToken.IsCancellationRequested)
            {
                int bytesRead = await connectedPlayer.tcpStream.ReadAsync(new ArraySegment<byte>(buffer), cancellationToken);
                Packet[] packets = ProcessBuffer(buffer, bytesRead);
                ProcessPackets(packets, connectedPlayer);
                
                Monitoring.receivedBytesPerSecond += bytesRead;
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine($"Receiving task for client id {Array.IndexOf(Server.connectedPlayers, connectedPlayer)} was cancelled");
        }
        catch
        {
            Console.WriteLine($"Failed to finish receiving TCP data from ({connectedPlayer.playerName}), disconnecting player...");
            Authentication.DisconnectClient(connectedPlayer);
        }
    }
    public static async Task ReceiveUdpData()
    {
        try
        {
            byte[] buffer = new byte[4096];
            while (true)
            {
                EndPoint udpEndPoint = new IPEndPoint(IPAddress.Any, 0);
                SocketReceiveFromResult receivedMessage = await Server.serverUdpSocket.ReceiveFromAsync(new ArraySegment<byte>(buffer), SocketFlags.None, udpEndPoint);
                
                ConnectedPlayer connectedPlayer = Authentication.CheckAuthenticationOfUdpClient(receivedMessage.RemoteEndPoint);
                if (connectedPlayer != null)
                {
                    Packet[] packets = ProcessBuffer(buffer, receivedMessage.ReceivedBytes);
                    ProcessPackets(packets, connectedPlayer);
                }
                Monitoring.receivedBytesPerSecond += receivedMessage.ReceivedBytes;
            }
        }
        catch
        {
            Console.WriteLine("Error receiving UDP packet");
        }
    }
    public static Packet[] ProcessBuffer(byte[] buffer, int byteLength) // processes bytes received from the buffer
    {
        string receivedBytesInString; // creates this empty string for to be used later

        if (Encryption.encryption) // runs if encryption is enabled
        {
            byte[] receivedBytes = new byte[byteLength];
            Array.Copy(buffer, receivedBytes, byteLength);
            receivedBytesInString = Encryption.Decrypt(receivedBytes);
        }
        else // runs if encryption is disabled
        {
            receivedBytesInString = Encoding.UTF8.GetString(buffer, 0, byteLength);
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

            Packet packet = new Packet
            {
                type = typeOfPacket,
                data = packetDataMatches[i].Groups[1].Value
            };
            packets[i] = packet;
        }
        return packets;
    }

    private static void ProcessPackets(Packet[] packets, ConnectedPlayer connectedPlayer)
    {
        foreach (Packet packet in packets)
        {
            ProcessDataSentByPlayer(packet, connectedPlayer);
        }
    }

    private static void ProcessDataSentByPlayer(Packet packet, ConnectedPlayer connectedClient)
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
                Task.Run(() => PlayersManager.SendChatMessageToEveryone(connectedClient, packet.data));
                break;
            // Type 3 means client is sending its own position to the server
            case 3:
                connectedClient.position = JsonSerializer.Deserialize<PlayerPosition>(packet.data);
                break;
        }
    }
    private static byte[] EncodeMessage(byte commandType, string message)
    {
        if (Encryption.encryption)
        {
            return Encryption.Encrypt($"#{commandType}#${message}$");
        }
        else
        {
            return Encoding.UTF8.GetBytes($"#{commandType}#${message}$");
        }
    }
}

