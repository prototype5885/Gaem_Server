using System.Text.Json;

public static class PlayersManager
{
    public static async Task ReplicatePlayerPositions()
    {
        PlayerPosition[] everyPlayersPosition = new PlayerPosition[Server.maxPlayers];

        while (true)
        {
            Thread.Sleep(Server.tickRate); // server tick, 100 times a second
            for (byte i = 0; i < Server.maxPlayers; i++) // copies the players' positions so server can send
            {
                if (Server.connectedPlayers[i] == null)
                {
                    everyPlayersPosition[i] = null;
                    continue;
                }

                everyPlayersPosition[i] = Server.connectedPlayers[i].position;
            }
            foreach (ConnectedPlayer connectedPlayer in Server.connectedPlayers)
            {
                if (connectedPlayer == null) continue;
                string jsonData = JsonSerializer.Serialize(everyPlayersPosition);
                try
                {
                    await PacketProcessor.SendUdp(3, jsonData, connectedPlayer);
                }
                catch
                {
                    Console.WriteLine("Error sending position to a player");
                }
            }
        }
    }
    public static void CalculatePlayerLatency(ConnectedPlayer connectedPlayer)
    {
        TimeSpan timeSpan = connectedPlayer.pingRequestTime - DateTime.UtcNow;
        connectedPlayer.latency = Math.Abs(timeSpan.Milliseconds) / 2;
    }
    public static async Task SendChatMessageToEveryone(ConnectedPlayer messageSenderPlayer, string message)
    {
        ChatMessage chatMessage = new ChatMessage
        {
            i = (byte)Array.IndexOf(Server.connectedPlayers, messageSenderPlayer),
            m = message
        };
        
        foreach (ConnectedPlayer player in Server.connectedPlayers)
        {
            if (player == null) continue;
            string jsonData = JsonSerializer.Serialize(chatMessage);
            await PacketProcessor.SendTcp(2, jsonData, player); // type 2 means relaying a message to every players
        }
    }

    public static async Task SendPlayerDataToEveryone()
    {
        foreach (ConnectedPlayer player in Server.connectedPlayers)
        {
            if (player == null) continue;
            string jsonData = JsonSerializer.Serialize(GetDataOfEveryConnectedPlayer());
            await PacketProcessor.SendTcp(4, jsonData, player); // type 4
        }
    }

    public static PlayerData[] GetDataOfEveryConnectedPlayer()
    {
        PlayerData[] playerDataArray = new PlayerData[Server.maxPlayers];
        for (byte i = 0; i < Server.maxPlayers; i++)
        {
            if (Server.connectedPlayers[i] != null)
            {
                playerDataArray[i] = GetDataOfConnectedPlayer(i);
            }
        }
        return playerDataArray;
    }
    public static PlayerData GetDataOfConnectedPlayer(byte index) // runs whenever a player joins
    {
        PlayerData playerData = new PlayerData
        {
            i = index,
            un = Server.connectedPlayers[index].playerName
        };
        return playerData;
    }
}