using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

public static class Server
{
    public static TcpListener tcpListener;
    public static readonly Socket serverUdpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

    public static int tcpPort;
    public static int udpPort;

    public static ConnectedPlayer[] connectedPlayers;

    public static byte maxPlayers;
    public static int tickRate;

    private static void Main(string[] args)
    {
        maxPlayers = 10;
        tickRate = 10;

        tcpPort = 1942;
        udpPort = tcpPort + 1;

        // Starts TCP server
        tcpListener = new TcpListener(IPAddress.Any, tcpPort);
        tcpListener.Start();

        // Starts UDP server
        serverUdpSocket.Bind(new IPEndPoint(IPAddress.Any, udpPort));

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Due to this issue: https://stackoverflow.com/questions/74327225/why-does-sending-via-a-udpclient-cause-subsequent-receiving-to-fail
            // .. the following needs to be done on windows
            const uint IOC_IN = 0x80000000U;
            const uint IOC_VENDOR = 0x18000000U;
            const int SIO_UDP_CONNRESET = unchecked((int)(IOC_IN | IOC_VENDOR | 12));
            serverUdpSocket.IOControl(SIO_UDP_CONNRESET, new byte[] { 0x00 }, null);
        }

        connectedPlayers = new ConnectedPlayer[maxPlayers];
        Encryption.GetEncryptionKey();

        Database.Initialize();

        Task.Run(() => PacketProcessor.ReceiveUdpData());
        Task.Run(() => PlayersManager.ReplicatePlayerPositions());
        // Task.Run(() => Monitoring.RunEverySecond());
        Task.Run(() => Authentication.WaitForPlayerToConnect());
        Thread.Sleep(Timeout.Infinite);
    }
}
