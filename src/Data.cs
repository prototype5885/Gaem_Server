using System.Text.Json.Serialization;

[JsonSourceGenerationOptions(WriteIndented = true)]
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

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(LocalPlayerPosition))]
internal partial class LocalPlayerPositionContext : JsonSerializerContext
{
}
public class LocalPlayerPosition
{
    public float x { get; set; } // Player position X
    public float y { get; set; } // Player position Y
    public float z { get; set; } // Player position Z
}

public class EveryPlayerPosition
{
    public Dictionary<int, (float, float, float)> epp { get; set; }
}