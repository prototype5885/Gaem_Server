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
}

//public class Players
//{
//    public Player[] players { get; set; }
//}
public class Players
{
    public List<Player> list { get; set; }
}