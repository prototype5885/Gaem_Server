class Program
{
    private static void Main()
    {
        int maxPlayers = 10;
        int port = 1943;
        Server server = new Server();
        server.StartUdpServer(maxPlayers, port);

    }
}
