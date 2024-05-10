using System.Text.Json.Serialization;

namespace Gaem_server.ClassesShared;

[JsonSerializable(typeof(InitialData))]
internal partial class InitialDataContext : JsonSerializerContext
{
}
public class InitialData
{
    public int loginResultValue { get; set; } // login result, response to the client about how the login went
    public int index { get; set; } // client index so player knows what slot he/she/it is in
    public int maxPlayers { get; set; } // max player amount so client will also know it
    public int tickRate { get; set; } // tick rate
    public int udpPort { get; set; } // udp port
}