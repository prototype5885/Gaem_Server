using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;


public class PacketProcessing
{
    List<string> sentPackets = new List<string>();
    public async Task SendReliablePacket(string message, UdpClient udpClient)
    {

        Byte[] messageBytes = Encoding.ASCII.GetBytes(message);
        sentPackets.Add(message);
        Console.WriteLine($"Packet {message} has been added to the packet list waiting for reply...");

        int attempts = 0;
        while (sentPackets.Contains(message))
        {
            if (attempts > 100)
            {
                Console.WriteLine("Packet delivery failed");
                break;
            }
            await udpClient.SendAsync(messageBytes, messageBytes.Length);
            Thread.Sleep(500);
            attempts++;
            Console.WriteLine($"Sent packet {message}, attempts: {attempts}");
        }
    }
    public void AcknowledgeReceived(string message)
    {
        sentPackets.Remove(message);
    }
    public Packet BreakUpPacket(byte[] receivedBytes)
    {
        string rawPacketString = Encoding.ASCII.GetString(receivedBytes, 0, receivedBytes.Length);

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

