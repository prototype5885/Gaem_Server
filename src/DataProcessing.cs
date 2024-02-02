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

    public int FindSlotForClientUdp(IPEndPoint[] connectedUdpClient, IPEndPoint clientAddress)
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





