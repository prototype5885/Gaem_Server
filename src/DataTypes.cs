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
public class PlayerPosition
{
    public float x { get; set; } // Player position X
    public float y { get; set; } // Player position Y
    public float z { get; set; } // Player position Z

    public float rx { get; set; } // Player head rotation X
    public float ry { get; set; } // Player body rotation Y


    public override string ToString()
    {
        return $"{(int)x}, {(int)y}, {(int)z}";
    }
}
public class EveryPlayersPosition
{
    public PlayerPosition[] positions { get; set; }
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
public class ConnecetedPlayer
{
    public int index { get; set; }
    public int databaseID { get; set; }
    public EndPoint address { get; set; }
    public int status { get; set; }

    public bool pingAnswered { get; set; }
    public int timeUntillTimeout { get; set; }
    public PlayerPosition position { get; set; }

    public ConnecetedPlayer()
    {
        databaseID = -1;
        pingAnswered = true;
        timeUntillTimeout = 4;
    }

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
        return $"Address: {address} | Index: {index} | Status: {statusMessage} | Ping answered: {pingAnswered} | Timeout in: {timeUntillTimeout} | Position XYZ: {position}";
    }
}