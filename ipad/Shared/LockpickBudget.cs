namespace Forger;

// Pure charge/cooldown rules, shared by the host and HUD.
internal sealed class LockpickBudget
{
    internal int Spent;
    internal float ReadyAt;
    internal void Reset(){Spent=0;ReadyAt=0;}
    internal bool Ready(float now,bool enabled)=>!enabled || now>=ReadyAt;
    internal int Remaining(float now,bool enabled,int limit)=>!enabled?int.MaxValue:now<ReadyAt?0:now>=ReadyAt&&ReadyAt>0?limit:Math.Max(0,limit-Spent);
    internal bool Use(float now,bool enabled,int limit,float cooldown)
    {
        if(!Ready(now,enabled))return false;
        if(!enabled){Reset();return true;}
        if(ReadyAt>0){Spent=0;ReadyAt=0;}
        if(++Spent>=Math.Max(1,limit)){Spent=0;ReadyAt=now+Math.Max(0,cooldown);}
        return true;
    }
}
