using System;
using System.Net.Sockets;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;

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
        receivedData = FixPacket(receivedData);
        return receivedData;
    }
    string FixPacket(string receivedData)
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
    public int FindSlotForClient(TcpClient[] tcpClients)
    {
        //int i = 0;
        //foreach (string ipAddress in clientIpAddresses)
        //{
        //    if (clientIpAddress == ipAddress)
        //    {
        //        Console.WriteLine($"Assigned index slot {i} for {clientIpAddress}");
        //        return i;
        //    }
        //    i++;
        //}
        //Console.WriteLine($"Connection rejected for {clientIpAddress}: Maximum number of clients reached. ");
        //return -1; // No available slot

        for (int i = 0; i < tcpClients.Length; i++)
        {
            if (tcpClients[i] == null)
            {
                Console.WriteLine($"Assigned index slot {i}");
                return i;
            }
        }
        Console.WriteLine($"Connection rejected: Maximum number of clients reached. ");
        return -1; // No available slot
    }
}





