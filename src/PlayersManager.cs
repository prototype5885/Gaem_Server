using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;


public static class PlayersManager
{
    public static async Task ReplicatePlayerPositions()
    {
        EveryPlayersPosition everyPlayersPosition = new EveryPlayersPosition(); // this thing is the format the server sends player positions in to each client
        everyPlayersPosition.p = new PlayerPosition[Server.maxPlayers];

        while (true)
        {
            Thread.Sleep(Server.tickrate); // server tick, 100 times a second
            for (byte i = 0; i < Server.maxPlayers; i++) // copies the players' positions so server can send
            {
                if (Server.connectedPlayers[i] == null)
                {
                    everyPlayersPosition.p[i] = null;
                    continue;
                }

                everyPlayersPosition.p[i] = Server.connectedPlayers[i].position;
            }
            // Console.Clear();
            // foreach (PlayerPosition playerPosition in everyPlayersPosition.p)
            // {
            //     if (playerPosition == null)
            //         Console.Write("0");
            //     else
            //         Console.Write("1");
            // }
            foreach (ConnectedPlayer connectedPlayer in Server.connectedPlayers)
            {
                if (connectedPlayer == null) continue;
                else
                {
                    string jsonData = JsonSerializer.Serialize(everyPlayersPosition, EveryPlayersPositionContext.Default.EveryPlayersPosition);
                    try
                    {
                        await PacketProcessor.SendUdp(3, jsonData, connectedPlayer);
                    }
                    catch
                    {
                        Console.WriteLine("error sending position to a player");
                    }
                }
            }
        }
    }
    public static void CalculatePlayerLatency(ConnectedPlayer connectedPlayer)
    {
        TimeSpan timeSpan = connectedPlayer.pingRequestTime - DateTime.UtcNow;
        connectedPlayer.latency = Math.Abs(timeSpan.Milliseconds) / 2;
    }
}

