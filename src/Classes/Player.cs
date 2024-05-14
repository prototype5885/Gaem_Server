using System.Net;
using System.Net.Sockets;
using Gaem_server.ClassesShared;

namespace Gaem_server.Classes;

public class Player {
    public int index { get; set; }
    public int databaseID { get; set; } = -1;
    public int status { get; set; }
    public string playerName { get; set; }
    public TcpClient tcpClient { get; set; }
    public byte[] aesKey { get; set; }
    public PlayerPosition position { get; set; } = new PlayerPosition();
}