using System.Net;
using System.Net.Sockets;
using Gaem_server.ClassesShared;

namespace Gaem_server.Classes;

public class ConnectedPlayer {
    public int index;
    public int databaseID = -1;
    public string playerName;
    public TcpClient tcpClient;
    public byte[] aesKey;
    public byte status;
    public PlayerPosition position = new PlayerPosition();
}