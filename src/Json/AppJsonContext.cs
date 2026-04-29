using System.Text.Json.Serialization;
using UniversalSensRandomizer.Models;

namespace UniversalSensRandomizer.Json;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(PersistedSettings))]
[JsonSerializable(typeof(HotkeyCombination))]
internal sealed partial class AppJsonContext : JsonSerializerContext
{
}
