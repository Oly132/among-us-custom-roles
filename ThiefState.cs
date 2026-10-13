namespace Forger;
internal sealed class ThiefState
{
    internal byte Owner=255;
    internal bool Used;
    internal void Reset(byte owner){Owner=owner;Used=false;}
    internal bool TryUse(byte actor,byte target)
    {
        if(Used || Owner==255 || actor!=Owner || target==255 || target==actor)return false;
        Used=true;return true;
    }
    internal static byte[] TransferOwners(byte[] owners,int thiefIndex,byte actor,byte victim,int stolenIndex)
    {
        if(thiefIndex<0 || thiefIndex>=owners.Length || owners[thiefIndex]!=actor || actor==victim || actor==255 || victim==255 || stolenIndex>=owners.Length || stolenIndex< -1 || stolenIndex==thiefIndex || (stolenIndex>=0 && owners[stolenIndex]!=victim))throw new ArgumentException("Invalid role transfer");
        var next=(byte[])owners.Clone();
        for(int i=0;i<next.Length;i++)if(next[i]==actor || next[i]==victim)next[i]=255;
        if(stolenIndex>=0)next[stolenIndex]=actor;
        return next;
    }
}
