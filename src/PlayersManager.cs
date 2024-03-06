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

        string jsonData;
        while (true)
        {
            Thread.Sleep(Server.tickrate); // server tick, 100 times a second
            for (byte i = 0; i < Server.maxPlayers; i++) // copies the players' positions so server can send
            {
                if (Server.connectedPlayers[i] == null)
                {
                    if (everyPlayersPosition.p[i] != null)
                    {
                        everyPlayersPosition.p[i] = null;
                    }
                    continue;
                }

                everyPlayersPosition.p[i] = Server.connectedPlayers[i].position;
            }

            for (byte i = 0; i < Server.maxPlayers; i++) // loops through every connected players positions to each
            {
                if (Server.connectedPlayers[i] == null) continue; // skips index if no connected player occupies the slot
                //if (connectedPlayers[i].pingAnswered == false) continue;
                jsonData = JsonSerializer.Serialize(everyPlayersPosition, EveryPlayersPositionContext.Default.EveryPlayersPosition);
                await PacketProcessor.SendTcp(3, jsonData, Server.connectedPlayers[i].tcpStream);
            }
        }
    }
    public static void CalculatePlayerLatency(ConnectedPlayer connectedPlayer)
    {
        TimeSpan timeSpan = connectedPlayer.pingRequestTime - DateTime.UtcNow;
        connectedPlayer.latency = Math.Abs(timeSpan.Milliseconds) / 2;
    }
    public static void DisconnectClient(ConnectedPlayer connectedPlayer)
    {
        connectedPlayer.tcpClient.Close(); // Closes TCP connection of client
        connectedPlayer.cancellationTokenSource.Cancel(); // Cancels receiving task from client

        Console.WriteLine($"Player {connectedPlayer.playerName} was disconnected");

        byte clientIndex = (byte)Array.IndexOf(Server.connectedPlayers, connectedPlayer);
        Server.connectedPlayers[clientIndex] = null; // Remove the player
    }
}

