using System.Text.Json.Serialization;

// LoginData
[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(LoginData))]
internal partial class LoginDataContext : JsonSerializerContext
{
}
public class LoginData
{
    public bool lr { get; set; } // True if login, false if register
    public string un { get; set; } // Username
    public string pw { get; set; } // Password
}

// PlayerPosition
[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(PlayerPosition))]
internal partial class PlayerPositionContext : JsonSerializerContext
{
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

// EveryPlayerPosition
[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(EveryPlayersPosition))]
internal partial class EveryPlayersPositionContext : JsonSerializerContext
{
}
public class EveryPlayersPosition
{
    public PlayerPosition[] positions { get; set; }
}

// InitialData
[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(InitialData))]
internal partial class InitialDataContext : JsonSerializerContext
{
}
public class InitialData
{
    public int lr { get; set; } // login result, response to the client about how the login went
    public int i { get; set; } // client index so player knows what slot he/she/it is in
    public int mp { get; set; } // max player amount so client will also know it
}

// EveryPlayersName
[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(EveryPlayersName))]
internal partial class EveryPlayersNameContext : JsonSerializerContext
{
}
public class EveryPlayersName
{
    public int[] playerIndex { get; set; }
    public string[] playerNames { get; set; }
}
