
public class ServerData
{
    // id, posX, posY, posZ, name
    Dictionary<int, (float, float, float, string)> currentPlayers = new Dictionary<int, (float, float, float, string)>();

    public void AddConnectedPlayer(int key, string name)
    {
        currentPlayers.Add(key, (0f, 0f, 0f, name));
        PrintConnectedClients();
    }
    public void DeleteDisconnectedPlayer(int key)
    {
        currentPlayers.Remove(key);
        PrintConnectedClients();
    }

    void PrintConnectedClients()
    {
        foreach (var kvp in currentPlayers)
        {
            Console.WriteLine(kvp);
        }
    }
}
