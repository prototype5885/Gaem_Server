
class Program
{

    static void Main(string[] args)
    {
        int maxPlayers = 4;
        DataProcessing dataProcessing = new DataProcessing(maxPlayers);

        TCPServer tcpServer = new TCPServer(maxPlayers, dataProcessing);
        tcpServer.StartTCPServer();

        //UDPServer udpServer = new UDPServer(maxPlayers, dataProcessing, tcpServer);
        //udpServer.StartUDPServer();

        Thread.Sleep(Timeout.Infinite);
    }
}