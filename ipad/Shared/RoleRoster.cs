namespace Forger;
// One owner per role and one role per player. 255 means unassigned, never a player.
internal static class RoleRoster
{
    internal static bool Valid(byte[] owners,int roleCount)=>owners.Length==roleCount && owners.Where(id=>id!=255).Distinct().Count()==owners.Count(id=>id!=255);
    internal static int Index(byte[] owners,byte player)=>player==255?-1:Array.IndexOf(owners,player);
    internal static bool Owns(byte[] owners,int role,byte player)=>player!=255 && role>=0 && role<owners.Length && owners[role]==player;
}
