using Gaem_server.Classes;
using System.Text.Json.Serialization;

namespace Gaem_server.src.ClassesShared;

[JsonSerializable(typeof(Packet))]
internal partial class PacketContext : JsonSerializerContext
{
}
public class Packet
{
    public int type;
    public Player owner;
    public string json;
}