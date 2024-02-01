using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Xml.Linq;

public class LoginData
{
    public bool lr { get; set; } // True if login, false if register
    public string un { get; set; } // Username
    public string pw { get; set; } // Password
}
public class Player
{
    public float x { get; set; } // Player position X
    public float y { get; set; } // Player position Y
    public float z { get; set; } // Player position Z

    public float rx { get; set; } // Player head rotation X
    public float ry { get; set; } // Player head rotation Y
    public float rz { get; set; } // Player head rotation Z

    public override string ToString()
    {
        return $"X:{x}, Y:{y}, Z:{z}, rX:{rx}, rY:{ry}, rZ:{rz}";
    }
}
public class Players
{
    public Player[] list { get; set; }
}
public class InitialData
{
    public int i { get; set; }
    public int mp { get; set; }
}
public class Packet
{
    public int packetType { get; set; }
    public string packetString { get; set; }
}