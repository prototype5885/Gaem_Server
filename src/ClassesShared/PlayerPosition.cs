using System.Text.Json.Serialization;

namespace Gaem_server.ClassesShared;

[JsonSerializable(typeof(PlayerPosition))]
internal partial class PlayerPositionContext : JsonSerializerContext
{
}
// [JsonSerializable(typeof(List<PlayerPosition>))]
// internal partial class ListPlayerPositionContext : JsonSerializerContext
// {
// }
public class PlayerPosition
{
    public float x;
    public float y;
    public float z;
    public float rx;
    public float ry;

    public override string ToString()
    {
        return $"{(int)x}, {(int)y}, {(int)z}";
    }
}