class Program
{
    private static void Main()
    {
        byte maxPlayers = 10;
        int port = 1943;
        Server server = new Server();
        server.StartServer(maxPlayers, port);

    }
}
