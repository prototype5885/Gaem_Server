using System.Text.Json.Serialization;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(LoginData))]
internal partial class LoginDataContext : JsonSerializerContext
{
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(Player))]
internal partial class PlayerContext : JsonSerializerContext
{
}


[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(Players))]
internal partial class PlayersContext : JsonSerializerContext
{
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(InitialData))]
internal partial class InitialDataContext : JsonSerializerContext
{
}