using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;


public class PacketProcessing
{
    public TcpClient server;

    public async Task Send(byte commandType, string message, NetworkStream stream)
    {
        try
        {
            byte[] messageByte = Encoding.ASCII.GetBytes($"#{commandType}#${message}$");

            await stream.WriteAsync(messageByte);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending message type {commandType}. Exception: {ex.Message}");
        }
    }

    public Packet BreakUpPacket(byte[] receivedBytes, int byteLength)
    {
        string rawPacketString = Encoding.ASCII.GetString(receivedBytes, 0, byteLength);

        Packet packet = new Packet();


        string packetLengthPattern = @"#(.*)#";
        Match match = Regex.Match(rawPacketString, packetLengthPattern);
        if (match.Success)
        {
            // Extract the value between the '#' characters
            string extractedValue = match.Groups[1].Value;
            int.TryParse(extractedValue, out int typeOfPacket);

            packet.type = typeOfPacket;

            int firstHashIndex = rawPacketString.IndexOf('#');
            int secondHashIndex = rawPacketString.IndexOf('#', firstHashIndex + 1) + 1;

            int lengthOfPackage = rawPacketString.Length - secondHashIndex;

            packet.data = rawPacketString.Substring(secondHashIndex, lengthOfPackage);

            return packet;
        }
        else
        {
            return null;
        }
    }
}

