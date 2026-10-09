namespace Forger;

// Pure game rules. All timestamps are supplied by the host's monotonic clock.
public sealed class EvidenceState
{
    public byte ForgerId { get; private set; } = byte.MaxValue;
    public byte? PendingVictim { get; private set; }
    public double Deadline { get; private set; }
    public double ReadyAt { get; private set; }
    public int Used { get; private set; }
    public int MaxUses { get; private set; }
    public float Cooldown { get; private set; }
    public float Window { get; private set; }
    readonly Dictionary<(byte suspect, byte victim), byte> forged = new();
    public void Reset(byte forger, int uses, float cooldown, float window)
    {
        ForgerId = forger; MaxUses = uses; Cooldown = cooldown; Window = window;
        PendingVictim = null; Deadline = ReadyAt = 0; Used = 0; forged.Clear();
    }
    public void Killed(byte killer, byte victim, double now)
    {
        if (killer != ForgerId) return;
        PendingVictim = victim; Deadline = now + Window;
    }
    public bool CanChange(byte player, byte victim, double now) => player == ForgerId &&
        PendingVictim == victim && now < Deadline && now >= ReadyAt && Used < MaxUses;
    public bool Change(byte player, byte victim, byte room, double now)
    {
        if (!CanChange(player, victim, now)) return false;
        forged[(player, victim)] = room; Used++; ReadyAt = now + Cooldown; PendingVictim = null;
        return true;
    }
    public void ReceiveChange(byte victim, byte room, double now)
    {
        forged[(ForgerId, victim)] = room; Used++; ReadyAt = now + Cooldown; PendingVictim = null;
    }
    public void Meeting() => PendingVictim = null;
    public bool TryGet(byte suspect, byte victim, out byte room) => forged.TryGetValue((suspect, victim), out room);
}
