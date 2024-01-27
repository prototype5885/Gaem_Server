class DataProcessing
{
    Dictionary<int, (float, float, float)> playersPositionDictionary = new Dictionary<int, (float, float, float)>();
    //public List<ServerPlayerPosition> everyPlayersPosition = new List<ServerPlayerPosition>();
    public EveryPlayerPosition everyPlayerPosition = new EveryPlayerPosition();

    public void AddNewClient(int serverIndex)
    {
        playersPositionDictionary.Add(serverIndex, (0f, 0f, 0f)); // Adds newly connected client to 


        //ServerPlayerPosition serverPlayerPosition = new ServerPlayerPosition(); // Adds newly connected client to list
        //serverPlayerPosition.serverIndex = serverIndex;
        //everyPlayersPosition.Add(serverPlayerPosition);

        //PrintConnectedClients();
    }
    public void ProcessData(int serverIndex, LocalPlayerPosition localPlayerPosition) // Loops for each connected clients
    {
        float posX = localPlayerPosition.x;
        float posY = localPlayerPosition.y;
        float posZ = localPlayerPosition.z;

        playersPositionDictionary[serverIndex] = (posX, posY, posZ); // Writes position X Y Z of each connected client in the dictionary

        everyPlayerPosition.epp = playersPositionDictionary;

        //everyPlayersPosition[serverIndex].posX = posX;
        //everyPlayersPosition[serverIndex].posY = posY;
        //everyPlayersPosition[serverIndex].posZ = posZ;
    }
    public void DeleteDisconnectedPlayer(int serverIndex)
    {
        playersPositionDictionary.Remove(serverIndex);
        //everyPlayersPosition.RemoveAll(p => p.serverIndex == serverIndex);
        //PrintConnectedClients();
    }

    public void PrintConnectedClients()
    {
        foreach (var kvp in playersPositionDictionary)
        {
            Console.WriteLine(kvp);
        }
    }
}





