using AmongUs.GameOptions;
using HarmonyLib;
using UnityEngine;
namespace Forger;

internal enum DogCause { Normal, Cannibal, Viper }
internal sealed record DogDeath(byte VictimId,string VictimName,Vector2 Position,string Room,DogCause Cause,byte KillerId,string[] Nearby,int Round,string VictimRole);
internal static class DogEvidence
{
    internal static int Round;
    internal static readonly Dictionary<byte,DogDeath> Deaths=new();
    internal static void Reset(){Round=0;Deaths.Clear();}
    internal static void NextRound(){Round++;Deaths.Clear();}
    internal static string Clean(string name)=>name.Replace("<","").Replace(">","").Replace("\n"," ").Replace("\r"," ");
    internal static DogDeath Capture(PlayerControl killer,PlayerControl victim)
    {
        var position=victim.GetTruePosition();
        var room=Game.Rooms().FirstOrDefault(r=>r.roomArea.OverlapPoint(position));
        var nearby=PlayerControl.AllPlayerControls.ToArray().Where(p=>Puppeteer.Living(p) && p.PlayerId!=victim.PlayerId)
            .OrderBy(p=>Vector2.SqrMagnitude(p.GetTruePosition()-position)).ThenBy(p=>p.PlayerId).Take(3).Select(p=>Clean(p.Data.PlayerName)).ToArray();
        var cause=Cannibal.Eaten.Contains(victim.PlayerId)?DogCause.Cannibal:killer.Data?.Role?.Role==RoleTypes.Viper?DogCause.Viper:DogCause.Normal;
        var role=RegisteredRoles.Get(victim.Data.Role.Role)?.Name ?? TranslationController.Instance.GetString(victim.Data.Role.StringName);
        return new(victim.PlayerId,Clean(victim.Data.PlayerName),position,room?Game.RoomName((byte)room!.RoomId):"Hallway",cause,killer.PlayerId,nearby,Round,Clean(role));
    }
    internal static bool BodyPresent(byte id)=>UnityEngine.Object.FindObjectsOfType<DeadBody>().Any(b=>b.ParentId==id && b.gameObject.activeInHierarchy);
    internal static DogDeath? Closest(Vector2 origin)
    {
        var current=Deaths.Values.Where(d=>d.Round==Round).ToArray();
        return current.Where(d=>d.Cause!=DogCause.Normal).OrderBy(d=>Vector2.SqrMagnitude(d.Position-origin)).ThenBy(d=>d.VictimId).FirstOrDefault()
            ?? current.Where(d=>BodyPresent(d.VictimId)).OrderBy(d=>Vector2.SqrMagnitude(d.Position-origin)).ThenBy(d=>d.VictimId).FirstOrDefault();
    }
    internal static string Findings(DogDeath? death)
    {
        if(death==null)return "<color=#70CFFF>Your dog found nothing.</color>\n\nIt sank into the floor.\nYour dog is gone.";
        var cause=death.Cause switch{DogCause.Cannibal=>"Eaten by a Cannibal",DogCause.Viper=>"Killed by a Viper",_=>"Killed by an impostor"};
        var result="<color=#70CFFF>DOG'S FINDINGS</color>\n\nVictim: "+death.VictimName+"\nRole: "+death.VictimRole+"\nCause: "+cause+"\nRoom: "+death.Room+"\n\nClosest players at the time of death:\n"+(death.Nearby.Length==0?"No other living players":string.Join("  •  ",death.Nearby.OrderBy(n=>n,StringComparer.OrdinalIgnoreCase)));
        if(death.Cause==DogCause.Cannibal && HungerEvidence.TryIdentify(death.VictimId,out var killer))
        {
            var p=Puppeteer.Find(killer);if(p && p!.Data!=null)result+="\n\n<color=#B842E6>Cannibal: "+Clean(p.Data.PlayerName)+"</color>";
        }
        return result;
    }
}
[HarmonyPatch(typeof(PlayerControl),nameof(PlayerControl.MurderPlayer))]
internal static class DogDeathCapture
{
    static void Prefix(PlayerControl __instance,PlayerControl target,ref DogDeath? __state)
    {
        if(Game.Host && Puppeteer.Living(target))__state=DogEvidence.Capture(__instance,target);
    }
    static void Postfix(PlayerControl target,DogDeath? __state)
    {
        if(__state!=null && target && target.Data.IsDead)DogEvidence.Deaths[target.PlayerId]=__state;
    }
}
