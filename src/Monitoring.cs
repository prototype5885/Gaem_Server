using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public static class CalculateLatency
{
    public static int sentBytesPerSecond = 0;
    public static int receivedBytesPerSecond = 0;
}
public static class Monitoring
{
    public const byte timeoutTime = 4;
    public static async Task RunEverySecond(int tcpPort, int udpPort, byte maxPlayers)
    {
        while (true)
        {
            CalculateLatency.receivedBytesPerSecond = 0;
            CalculateLatency.sentBytesPerSecond = 0;
            MonitorValues(tcpPort, udpPort, maxPlayers);
            await PingClientsUdp(timeoutTime, maxPlayers, Encryption.encryptionKey);
            Thread.Sleep(1000);
        }
    }
    static void MonitorValues(int tcpPort, int udpPort, byte maxPlayers)
    {
        Console.Clear();
        Console.WriteLine($"TCP port: {tcpPort}, UDP port: {udpPort} | Players: {GetCurrentPlayerCount(maxPlayers)}/{maxPlayers}");
        Console.WriteLine($"Received bytes/s: {CalculateLatency.receivedBytesPerSecond}");
        Console.WriteLine($"Sent bytes/s: {CalculateLatency.sentBytesPerSecond}\n");
        for (byte i = 0; i < maxPlayers; i++)
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
    static async Task PingClientsUdp(byte timeoutTime, byte maxPlayers, byte[] encryptionKey)
    {
        for (byte i = 0; i < maxPlayers; i++)
        {
            try
            {
                if (Server.connectedPlayers[i] == null) continue;

                if (Server.connectedPlayers[i].udpPingAnswered == false) // runs if connected client hasn't replied to ping
                {
                    Server.connectedPlayers[i].timeUntillTimeout--;
                    Server.connectedPlayers[i].status = 0;

                    if (Server.connectedPlayers[i].timeUntillTimeout < 1) // runs if client didnt answer during timeout interval
                    {
                        PlayersManager.DisconnectClient(Server.connectedPlayers[i]);
                        continue;
                    }
                }
                else if (Server.connectedPlayers[i].udpPingAnswered == true && Server.connectedPlayers[i].timeUntillTimeout != timeoutTime) // runs if connected client answered the ping
                {
                    Server.connectedPlayers[i].timeUntillTimeout = timeoutTime;
                }

                Server.connectedPlayers[i].udpPingAnswered = false; // resets the array
                Server.connectedPlayers[i].pingRequestTime = DateTime.UtcNow;

                await PacketProcessor.SendTcp(0, "", Server.connectedPlayers[i].tcpStream);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }
    }
    static byte GetCurrentPlayerCount(byte maxPlayers)
    {
        byte playerCount = 0;
        for (byte i = 0; i < maxPlayers; i++)
        {
            if (Server.connectedPlayers[i] != null)
            {
                playerCount++;
            }
        }
        return playerCount;
    }
}

