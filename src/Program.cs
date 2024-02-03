
class Program
{

    static void Main(string[] args)
    {
        int maxPlayers = 2;
        PacketProcessing packetProcessing = new PacketProcessing();

        ServerUDP server = new ServerUDP(maxPlayers, packetProcessing);


        Thread.Sleep(Timeout.Infinite);
    }
}