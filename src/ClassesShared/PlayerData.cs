using System.Text.Json.Serialization;

namespace Gaem_server.ClassesShared;

[JsonSerializable(typeof(PlayerData))]
internal partial class PlayerDataContext : JsonSerializerContext
{
}
[JsonSerializable(typeof(PlayerData[]))]
internal partial class PlayerDataArrayContext : JsonSerializerContext
{
}
public class PlayerData
{
    public int i { get; set; } = -1; // player index
    public int s { get; set; } // status of player connection
    public string un { get; set; } = "Unnamed"; // username
}