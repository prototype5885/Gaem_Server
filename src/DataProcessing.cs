using System.Numerics;
using System.Text.RegularExpressions;

class DataProcessing
{
    //public Player[] players;
    public Players players = new Players();

    public DataProcessing(int maxPlayers) // Runs in the beginning
    {
        players.list = new List<Player>();
    }
    public void AddNewClientToDictionary(int serverIndex)
    {
        Player player = new Player();
        players.list.Add(player);
        //players.players[serverIndex] = new Player();
    }
    public void ProcessPositionOfClients(int serverIndex, Player clientPlayer) // Loops for each connected clients
    {
        players.list[serverIndex] = clientPlayer;
        //players.players[serverIndex] = clientPlayer;

        //Console.WriteLine("X: " + players.list[serverIndex].x + ", Y: " + players.list[serverIndex].y + ", Z: " + players.list[serverIndex].z);
        //Console.WriteLine("X: " + players.players[serverIndex].x + ", Y: " + players.players[serverIndex].y + ", Z: " + players.players[serverIndex].z);
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
    public string FixPacket(string receivedData)
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
}





