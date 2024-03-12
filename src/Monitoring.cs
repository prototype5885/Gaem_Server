using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


public static class Monitoring
{
    public static int sentBytesPerSecond = 0;
    public static int receivedBytesPerSecond = 0;

    public const byte timeoutTime = 4;

    public static async Task RunEverySecond()
    {
        while (true)
        {
            receivedBytesPerSecond = 0;
            sentBytesPerSecond = 0;
            MonitorValues();
            await PingClientsUdp();
            Thread.Sleep(1000);
        }
    }

    private static void MonitorValues()
    {
        Console.Clear();
        Console.WriteLine($"TCP port: {Server.tcpPort}, UDP port: {Server.udpPort} | Players: {GetCurrentPlayerCount()}/{Server.maxPlayers}");
        Console.WriteLine($"Received bytes/s: {receivedBytesPerSecond}");
        Console.WriteLine($"Sent bytes/s: {sentBytesPerSecond}\n");
        for (byte i = 0; i < Server.maxPlayers; i++)
        {
            Console.Write($"{i}: ");

            if (Server.connectedPlayers[i] == null)
            {
                Console.WriteLine("Free slot");
                continue;
            }

            Console.WriteLine(Server.connectedPlayers[i]);
        }
    }

    private static async Task PingClientsUdp()
    {
        for (byte i = 0; i < Server.maxPlayers; i++)
        {
            if (Server.connectedPlayers[i] == null) continue;

            if (Server.connectedPlayers[i].udpPingAnswered == false) // runs if connected client hasn't replied to ping
            {
                Server.connectedPlayers[i].timeUntillTimeout--;
                Server.connectedPlayers[i].status = 0;

                if (Server.connectedPlayers[i].timeUntillTimeout < 1) // runs if client didnt answer during timeout interval
                {
                    Authentication.DisconnectClient(Server.connectedPlayers[i]);
                    continue;
                }
            }
            else if (Server.connectedPlayers[i].udpPingAnswered == true && Server.connectedPlayers[i].timeUntillTimeout != timeoutTime) // runs if connected client answered the ping
            {
                Server.connectedPlayers[i].timeUntillTimeout = timeoutTime;
            }

            Server.connectedPlayers[i].udpPingAnswered = false; // resets the array
            Server.connectedPlayers[i].pingRequestTime = DateTime.UtcNow;

            await PacketProcessor.SendTcp(0, "", Server.connectedPlayers[i]);
        }
    }

    public static byte GetCurrentPlayerCount()
    {
        byte playerCount = 0;
        foreach (ConnectedPlayer connectedPlayer in Server.connectedPlayers)
        {
            if (connectedPlayer != null)
            {
                playerCount++;
            }
        }

        return playerCount;
    }
}