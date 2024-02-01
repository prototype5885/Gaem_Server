using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks.Dataflow;

public class DataProcessing
{
    public Players players = new Players();

    public DataProcessing(int maxPlayers) // Runs in the beginning
    {
        //players.list = new List<Player>();
        players.list = new Player[maxPlayers];
    }
    public void AddNewClientToPlayersList(int index)
    {
        players.list[index] = new Player();
    }
    public void ProcessPositionOfClients(int serverIndex, Player clientPlayer) // Loops for each connected clients
    {
        players.list[serverIndex] = clientPlayer;
    }
    public void DeleteDisconnectedPlayer(int serverIndex)
    {
        players.list[serverIndex] = null;
    }
    public void PrintConnectedClients()
    {
        foreach (Player player in players.list)
        {
            if (player == null)
            {
                Console.WriteLine("Null");
            }
            else
            {
                Console.WriteLine(player);
            }
        }
    }
    public string ByteToStringWithFix(byte[] receivedBytes, int bytesRead)
    {
        string receivedData = Encoding.ASCII.GetString(receivedBytes, 0, bytesRead);
        receivedData = ProcessPacket(receivedData);
        return receivedData;
    }
    string ProcessPacket(string receivedData)
    {
        //string pattern = @"#(.*?)#";
        string pattern = @"#(.*)#";

        // Use Regex.Match to find the first match
        Match match = Regex.Match(receivedData, pattern);

        // Check if a match is found
        if (match.Success)
        {
            // Extract the value between the '#' characters
            string extractedValue = match.Groups[1].Value;

            int.TryParse(extractedValue, out int lengthOfJson);

            int firstHashIndex = receivedData.IndexOf('#');
            int secondHashIndex = receivedData.IndexOf('#', firstHashIndex + 1) + 1;

            int startIndex = secondHashIndex;
            int endIndex = lengthOfJson;

            string jsonData = receivedData.Substring(startIndex, endIndex);

            return jsonData;
        }
        else
        {
            return "error";
        }
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

            packet.packetType = typeOfPacket;

            int firstHashIndex = rawPacketString.IndexOf('#');
            int secondHashIndex = rawPacketString.IndexOf('#', firstHashIndex + 1) + 1;

            int lengthOfPackage = rawPacketString.Length - secondHashIndex;

            packet.packetString = rawPacketString.Substring(secondHashIndex, lengthOfPackage);

            return packet;
        }
        else
        {
            return null;
        }
    }

    public int FindSlotForClientTCP(TcpClient[] tcpClient, TcpClient client)
    {
        for (int i = 0; i < tcpClient.Length; i++)
        {
            if (tcpClient[i] == null)
            {
                tcpClient[i] = client; // Adds new client to list of tcp clients
                Console.WriteLine($"Assigned index slot {i}");
                return i;
            }
        }
        Console.WriteLine($"Connection rejected: Maximum number of clients reached. ");
        return -1; // No available slot
    }
    public int FindSlotForClientUDP(IPEndPoint[] connectedUdpClient, IPEndPoint clientAddress)
    {
        for (int i = 0; i < connectedUdpClient.Length; i++)
        {
            if (connectedUdpClient[i] == null)
            {
                connectedUdpClient[i] = clientAddress; // Adds new client to list of tcp clients
                Console.WriteLine($"Assigned index slot {i}");
                return i;
            }
        }
        Console.WriteLine($"Connection rejected: Maximum number of clients reached. ");
        return -1; // No available slot
    }

}





