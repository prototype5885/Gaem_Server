using System.Text.Json.Serialization;

namespace Gaem_server.ClassesShared;

[JsonSerializable(typeof(PlayerPositionWithID))]
internal partial class PlayerPositionWithIDContext : JsonSerializerContext
{
}
[JsonSerializable(typeof(PlayerPositionWithID[]))]
internal partial class PlayerPositionWithIDArrayContext : JsonSerializerContext
{
}

public class PlayerPositionWithID
{
    public int i { get; set; }
    public PlayerPosition p { get; set; }
}