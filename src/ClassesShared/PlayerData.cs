using System.Text.Json.Serialization;

namespace Gaem_server.ClassesShared;

[JsonSerializable(typeof(PlayerData))]
internal partial class PlayerDataContext : JsonSerializerContext
{
}
[JsonSerializable(typeof(List<PlayerData>))]
internal partial class ListPlayerDataContext : JsonSerializerContext
{
}
public class PlayerData
{
    public int i { get; set; } // player index
    public string un { get; set; } // username
}