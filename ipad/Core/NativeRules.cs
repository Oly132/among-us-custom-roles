using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Forger.IPad;

// Platform-neutral rules only. The eventual signed native game adapter must
// validate host authority, actual roles, phases, distances, doors and RPC senders.
// None of these exports load, hook, modify or authenticate the base game.
[StructLayout(LayoutKind.Sequential,Pack=8)]
public struct RulesConfig
{
    public uint Size,Abi;
    public byte Forger,Silencer,Thief,FatGuy,Lockpicker,DoorCooldownEnabled;
    public ushort Reserved;
    public int ForgeUses;
    public float ForgeCooldown,ForgeWindow;
    public int DoorsPerUse;
    public float DoorCooldown;
}
[StructLayout(LayoutKind.Sequential,Pack=8)]
public struct RulesSnapshot
{
    public uint Size,Epoch;
    public byte PendingVictim,FatGuyProtectionSpent,ThiefUsed,Silenced;
    public int ForgeUsesRemaining,SilenceSecondsRemaining,DoorUsesRemaining;
    public double EvidenceDeadline,ForgeReadyAt,DoorReadyAt;
}
public enum RulesEvent:int
{
    ForgerKilled=1,Forge=2,ApplySilence=3,MeetingStarted=4,MeetingEnded=5,
    Tick=6,OrdinaryStab=7,OpenSabotagedDoor=8,TrySteal=9
}
public static unsafe class NativeRules
{
    sealed class Context
    {
        internal RulesConfig Config;
        internal uint Epoch;
        internal readonly EvidenceState Evidence=new();
        internal readonly SilenceState Silence=new();
        internal readonly LockpickBudget Doors=new();
        internal readonly ThiefState Thief=new();
        internal bool ProtectionSpent;
        internal double LastTime;
    }
    static readonly object gate=new();
    static readonly Dictionary<uint,Context> contexts=new();
    static uint next=1;
    public const int InvalidArgument=-1,UnknownContext=-2,InternalFailure=-3;
    [UnmanagedCallersOnly(EntryPoint="forger_rules_abi",CallConvs=new[]{typeof(CallConvCdecl)})]
    public static uint Abi()=>1;

    [UnmanagedCallersOnly(EntryPoint="forger_rules_create",CallConvs=new[]{typeof(CallConvCdecl)})]
    public static uint Create(RulesConfig* input,uint epoch)
    {
        try
        {
            if(input==null || input->Size!=sizeof(RulesConfig) || input->Abi!=1 || input->Reserved!=0 || epoch==0)return 0;
            var config=*input;
            var owners=new[]{config.Forger,config.Silencer,config.Thief,config.FatGuy,config.Lockpicker};
            if(!RoleRoster.Valid(owners,owners.Length) || config.ForgeUses<1 || config.ForgeUses>15 ||
                !float.IsFinite(config.ForgeCooldown) || config.ForgeCooldown<0 || config.ForgeCooldown>120 ||
                !float.IsFinite(config.ForgeWindow) || config.ForgeWindow<1 || config.ForgeWindow>60 ||
                config.DoorsPerUse<1 || config.DoorsPerUse>100 || config.DoorCooldownEnabled>1 ||
                !float.IsFinite(config.DoorCooldown) || config.DoorCooldown<0 || config.DoorCooldown>600)return 0;
            lock(gate)
            {
                if(contexts.Count>=32 || next==0)return 0;
                var state=new Context{Config=config,Epoch=epoch};
                state.Evidence.Reset(config.Forger,config.ForgeUses,config.ForgeCooldown,config.ForgeWindow);
                state.Thief.Reset(config.Thief);uint handle=next++;contexts.Add(handle,state);return handle;
            }
        }
        catch{return 0;}
    }
    [UnmanagedCallersOnly(EntryPoint="forger_rules_destroy",CallConvs=new[]{typeof(CallConvCdecl)})]
    public static int Destroy(uint handle)
    {
        try{lock(gate)return contexts.Remove(handle)?1:UnknownContext;}catch{return InternalFailure;}
    }
    [UnmanagedCallersOnly(EntryPoint="forger_rules_event",CallConvs=new[]{typeof(CallConvCdecl)})]
    public static int Event(uint handle,int kind,byte actor,byte target,byte value,double now)
    {
        try
        {
            if(!double.IsFinite(now) || now<0 || now>float.MaxValue)return InvalidArgument;
            lock(gate)
            {
                if(!contexts.TryGetValue(handle,out var s))return UnknownContext;
                if(now<s.LastTime)return InvalidArgument;
                s.LastTime=now;
                switch((RulesEvent)kind)
                {
                    case RulesEvent.ForgerKilled:
                        if(actor==255 || target==255 || actor==target || actor!=s.Config.Forger)return 0;
                        s.Evidence.Killed(actor,target,now);return 1;
                    case RulesEvent.Forge:
                        if(actor==255 || target==255)return 0;
                        return s.Evidence.Change(actor,target,value,now)?1:0;
                    case RulesEvent.ApplySilence:
                        if(actor==255 || target==255 || actor==target || actor!=s.Config.Silencer)return 0;
                        return s.Silence.Add(target)?1:0;
                    case RulesEvent.MeetingStarted:s.Evidence.Meeting();s.Silence.MeetingStarted();return 1;
                    case RulesEvent.MeetingEnded:s.Silence.MeetingEnded((float)now);return 1;
                    case RulesEvent.Tick:
                        foreach(var id in s.Silence.Expired((float)now))s.Silence.Targets.Remove(id);
                        return 1;
                    case RulesEvent.OrdinaryStab:
                        // The game adapter calls this only for a validated ordinary
                        // impostor stabbing, never Viper, Cannibal or an explosion.
                        return StabbingProtection.TryConsume(ref s.ProtectionSpent,target!=255 && target==s.Config.FatGuy)?1:0;
                    case RulesEvent.OpenSabotagedDoor:
                        if(actor==255 || actor!=s.Config.Lockpicker)return 0;
                        return s.Doors.Use((float)now,s.Config.DoorCooldownEnabled!=0,s.Config.DoorsPerUse,s.Config.DoorCooldown)?1:0;
                    case RulesEvent.TrySteal:return s.Thief.TryUse(actor,target)?1:0;
                    default:return InvalidArgument;
                }
            }
        }
        catch{return InternalFailure;}
    }
    [UnmanagedCallersOnly(EntryPoint="forger_rules_snapshot",CallConvs=new[]{typeof(CallConvCdecl)})]
    public static int Snapshot(uint handle,byte player,double now,RulesSnapshot* output)
    {
        try
        {
            if(output==null || output->Size!=sizeof(RulesSnapshot) || !double.IsFinite(now) || now<0 || now>float.MaxValue)return InvalidArgument;
            lock(gate)
            {
                if(!contexts.TryGetValue(handle,out var s))return UnknownContext;
                bool silenced=s.Silence.Targets.TryGetValue(player,out var mute) && now<mute.release;
                *output=new RulesSnapshot
                {
                    Size=(uint)sizeof(RulesSnapshot),Epoch=s.Epoch,PendingVictim=s.Evidence.PendingVictim??255,
                    FatGuyProtectionSpent=(byte)(s.ProtectionSpent?1:0),ThiefUsed=(byte)(s.Thief.Used?1:0),Silenced=(byte)(silenced?1:0),
                    ForgeUsesRemaining=Math.Max(0,s.Evidence.MaxUses-s.Evidence.Used),
                    SilenceSecondsRemaining=!silenced?0:float.IsPositiveInfinity(mute.release)?-1:(int)Math.Ceiling(mute.release-now),
                    DoorUsesRemaining=s.Doors.Remaining((float)now,s.Config.DoorCooldownEnabled!=0,s.Config.DoorsPerUse),
                    EvidenceDeadline=s.Evidence.Deadline,ForgeReadyAt=s.Evidence.ReadyAt,DoorReadyAt=s.Doors.ReadyAt
                };
                return 1;
            }
        }
        catch{return InternalFailure;}
    }
}
