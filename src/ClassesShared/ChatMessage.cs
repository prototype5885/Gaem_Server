using System.Text.Json.Serialization;

namespace Gaem_server.ClassesShared;

[JsonSerializable(typeof(ChatMessage))]
internal partial class ChatMessageContext : JsonSerializerContext
{
}
public class ChatMessage
{
    public int i { get; set; } // index of sender
    public string msg { get; set; } // the message
}