
class Program
{

    static void Main(string[] args)
    {
        int maxPlayers = 4;
        DataProcessing dataProcessing = new DataProcessing(maxPlayers);

        //Server server = new Server(maxPlayers, dataProcessing);
        ServerUDP server = new ServerUDP(maxPlayers, dataProcessing);


        Thread.Sleep(Timeout.Infinite);
    }
}