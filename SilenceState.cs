namespace Forger;

// Host owns meeting numbering and expiry; clients receive the same state transitions.
internal sealed class SilenceState
{
    internal readonly Dictionary<byte, (int meeting, float release)> Targets = new();
    internal readonly HashSet<byte> PreviouslySilenced = new();
    internal int Meeting;
    internal void Clear() { Targets.Clear(); PreviouslySilenced.Clear(); Meeting=0; }
    internal bool Add(byte id) { if(!PreviouslySilenced.Add(id)) return false; Targets[id]=(Meeting+1,float.PositiveInfinity); return true; }
    internal void MeetingStarted() => Meeting++;
    internal void MeetingEnded(float now)
    {
        foreach(var id in Targets.Keys.ToArray())
        {
            var entry=Targets[id];
            if(entry.meeting<=Meeting && float.IsPositiveInfinity(entry.release)) Targets[id]=(entry.meeting,now+10);
        }
    }
    internal bool Contains(byte id) => Targets.ContainsKey(id);
    internal byte[] Expired(float now) => Targets.Where(e=>now>=e.Value.release).Select(e=>e.Key).ToArray();
}

