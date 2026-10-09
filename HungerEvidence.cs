using UnityEngine;
namespace Forger;
// Shared evidence contract for the future Dog role. Ordinary players cannot see scent.
internal sealed record HungerTrace(byte VictimId,byte KillerId,Vector2 Position,bool HalfBody,bool FromCannibal);
internal static class HungerEvidence
{
    internal static readonly Dictionary<byte,HungerTrace> Traces=new();
    internal static void Add(byte victim,byte killer,Vector2 position,bool half,bool cannibal)=>Traces[victim]=new(victim,killer,position,half,cannibal);
    internal static bool TryIdentify(byte victim,out byte killer)
    {
        killer=255;
        if(!Traces.TryGetValue(victim,out var trace) || !trace.FromCannibal || !trace.HalfBody || !DogEvidence.BodyPresent(victim))return false;
        killer=trace.KillerId;return true;
    }
    internal static void Clear()=>Traces.Clear();
}
