using System.Text.Json;
namespace Forger;
internal static class UpdatePolicy
{
    // Legacy mandatory feeds have no flag. Optional previews must explicitly opt out.
    internal static bool IsMandatory(JsonElement manifest)=>!manifest.TryGetProperty("mandatory",out var value)||value.ValueKind==JsonValueKind.True;
}
