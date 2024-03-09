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
public class InitialData
{
    public byte lr { get; set; } // login result, response to the client about how the login went
    public byte i { get; set; } // client index so player knows what slot he/she/it is in
    public byte mp { get; set; } // max player amount so client will also know it
    public int tr { get; set; } // tick rate
    public PlayerData[] pda { get; set; } // list of data of players, such as name
}
public class PlayerData
{
    public byte i { get; set; } // player index
    public string un { get; set; } // username
}
public class ChatMessage
{
    public byte i { get; set; } // index of sender
    public string m { get; set; } // the message
}