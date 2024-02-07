using System.Text.Json.Serialization;

[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(LoginData))]
internal partial class LoginDataContext : JsonSerializerContext
{
}

[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(PlayerPosition))]
internal partial class PlayerPositionContext : JsonSerializerContext
{
}


[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(EveryPlayersPosition))]
internal partial class EveryPlayersPositionContext : JsonSerializerContext
{
}

[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(InitialData))]
internal partial class InitialDataContext : JsonSerializerContext
{
}