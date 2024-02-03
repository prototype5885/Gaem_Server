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
    public Player[] arrayOfPlayersData { get; set; }
}
public class InitialData
{
    public int i { get; set; }
    public int mp { get; set; }
}
public class Packet
{
    public int type { get; set; }
    public string data { get; set; }
}
public class CompleteClientInfo
{
    public IPEndPoint IPEndPoint { get; set; }
    public int status { get; set; }
    public int clientindex { get; set; }
    public bool pingAnswered { get; set; }
    public int timeUntillTimeout { get; set; }
    public Vector3 position { get; set; }

    public override string ToString()
    {
        string statusMessage = string.Empty;
        switch (status)
        {
            case 0:
                statusMessage = "Login";
                break;
            case 1:
                statusMessage = "Ingame";
                break;
            case 2:
                statusMessage = "Timing out";
                break;
        }
        return $"Address: {IPEndPoint}, Statuus: {statusMessage}, Index: {clientindex}, Ping answered: {pingAnswered}, Timeout in: {timeUntillTimeout}, Position: {position}";
    }
}