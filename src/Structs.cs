using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Xml.Linq;

public class Packet
{
    public byte type { get; set; }
    public string data { get; set; }
}
public class ConnecetedPlayer
{
    public byte index { get; set; }
    public int databaseID { get; set; }
    public string playerName { get; set; }
    public IPEndPoint address { get; set; }
    public NetworkStream stream { get; set; }
    public CancellationTokenSource cancellationTokenSource { get; set; }
    public byte status { get; set; }

    public bool pingAnswered { get; set; }
    public DateTime pingRequestTime { get; set; }
    public int latency { get; set; }
    public byte timeUntillTimeout { get; set; }
    public PlayerPosition position { get; set; }

    public ConnecetedPlayer()
    {
        databaseID = -1;
        pingAnswered = true;
        timeUntillTimeout = 4;
        status = 1;
    }

    public override string ToString()
    {
        string statusMessage = string.Empty;
        switch (status)
        {
            case 0:
                statusMessage = "Timing out";
                break;
            case 1:
                statusMessage = "Ingame";
                break;

        }
        if (status == 0) latency = 999;
        latency = Math.Clamp(latency, 0, 999);
        return $"Addr: {address} | Slot: {index} | db id: {databaseID} | Name: {playerName} | Status: {statusMessage} | Latency: {latency} | Timeout in: {timeUntillTimeout} | Pos XYZ: {position}";
    }
}
