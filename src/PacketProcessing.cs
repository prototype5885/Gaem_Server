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
    public Socket socket;

    private List<string> packetWithoutACK = new List<string>();

    public async Task SendUnreliable(byte commandType, string message, EndPoint address)
    {
        try
        {
            byte[] messageByte = Encoding.ASCII.GetBytes($"#{commandType}#{message}");

            if (address == null)
            {
                await socket.SendAsync(messageByte, SocketFlags.None);
            }
            else
            {
                await socket.SendToAsync(messageByte, SocketFlags.None, address);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending unreliable message type {commandType}. Exception: {ex.Message}");
        }
    }
    public async Task SendReliable(byte commandType, string message, EndPoint address)
    {
        try
        {
            Console.WriteLine("Sending repliable message");
            byte[] messageByte = Encoding.ASCII.GetBytes($"#{commandType}#{message}");
            packetWithoutACK.Add(message);
            Console.WriteLine($"Packet {message} has been added to the packet list waiting for reply...");

            if (address == null)
            {
                await socket.SendAsync(messageByte, SocketFlags.None);
            }
            else
            {
                await socket.SendToAsync(messageByte, SocketFlags.None, address);
            }

            var timer = new System.Timers.Timer();
            timer.Interval = 1000;
            timer.Elapsed += SendAgain;
            timer.AutoReset = false;
            timer.Enabled = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending reliable message type {commandType}. Exception: {ex.Message}");
        }

    }

    private static void SendAgain(object source, ElapsedEventArgs e)
    {
        Console.WriteLine("Timer elapsed at: " + e.SignalTime);
    }

    public void AcknowledgeReceived(string message)
    {
        packetWithoutACK.Remove(message);
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

