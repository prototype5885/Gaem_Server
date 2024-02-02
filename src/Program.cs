
class Program
{

    static void Main(string[] args)
    {
        int maxPlayers = 4;
        DataProcessing dataProcessing = new DataProcessing(maxPlayers);
        PacketProcessing packetProcessing = new PacketProcessing();

        ServerUDP server = new ServerUDP(maxPlayers, dataProcessing, packetProcessing);


        Thread.Sleep(Timeout.Infinite);
    }
}