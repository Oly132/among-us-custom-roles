using AmongUs.GameOptions;
using BepInEx.Configuration;
using Hazel;
using UnityEngine;
namespace Forger;
internal static class Terrorist
{
    internal const int Index=7;
    internal static byte RoleId=255;
    internal static bool Exploding,Used,Won;
    internal static ConfigEntry<int> Chance=null!;
    internal static bool Mine=>Game.Local && Game.Local!.PlayerId==RoleId;
    internal static bool CanUse=>Mine && Game.Started && Game.Alive && Game.Local!.CanMove && !Game.Local.inVent && !MeetingHud.Instance && !Minigame.Instance && !RoleIntro.Visible && !Used;
    internal static void Configure()=>Chance=Plugin.Instance.Config.Bind("Terrorist","Assignment chance",0,new ConfigDescription("Chance for a Terrorist impostor.",new AcceptableValueRange<int>(0,100)));
    internal static void Reset(byte id=255){RoleId=id;Used=Won=Exploding=false;RegisteredRoles.Apply(id,Index);}
    internal static PlayerControl[] Targets(PlayerControl source)=>PlayerControl.AllPlayerControls.ToArray().Where(p=>Puppeteer.Living(p)&&p.PlayerId!=source.PlayerId&&!p.inVent&&Vector2.Distance(source.GetTruePosition(),p.GetTruePosition())<=NormalGameOptionsV12.KillDistances[0]&&!Physics2D.Linecast(source.GetTruePosition(),p.GetTruePosition(),Constants.ShipAndObjectsMask).collider).ToArray();
    internal static void Press(){if(!CanUse)return;if(Game.Host)Commit(Game.Local!);else Game.Send(236,w=>{w.Write((byte)1);w.Write(Game.Epoch);},AmongUsClient.Instance.HostId);}
    internal static bool Commit(PlayerControl source)
    {
        if(!Game.Host||!Game.Started||Used||source.PlayerId!=RoleId||!Puppeteer.Living(source)||!source.CanMove||source.inVent||MeetingHud.Instance)return false;
        var targets=Targets(source);Won=targets.Length==PlayerControl.AllPlayerControls.ToArray().Count(p=>Puppeteer.Living(p)&&p.PlayerId!=RoleId);
        Plugin.Instance.Log.LogInfo("TERRORIST blast: targets="+targets.Length+", full-lobby win="+Won);
        var ids=targets.Select(p=>p.PlayerId).ToArray();Used=true;
        Game.Send(237,w=>{w.Write((byte)1);w.Write(Game.Epoch);w.Write(Won);w.Write((byte)ids.Length);foreach(var id in ids)w.Write(id);});
        Apply(ids,Won);
        if(Won)GameManager.Instance.RpcEndGame(GameOverReason.ImpostorsByKill,false);
        return true;
    }
    static void Apply(byte[] targets,bool won)
    {
        Used=true;Won=won;var source=Puppeteer.Find(RoleId);if(!source)return;
        Exploding=true;
        try{foreach(var id in targets){var p=Puppeteer.Find(id);if(Puppeteer.Living(p))BlastKill(source!,p!);}if(!won&&Puppeteer.Living(source))BlastKill(source!,source!);}
        finally{Exploding=false;}
        Game.Notify(won?"Explosion — impostors win!":"Explosion!");
    }
    static void BlastKill(PlayerControl killer,PlayerControl victim)
    {
        // An explosion is simultaneous; the native stabbing coroutine would move
        // the attacker from victim to victim. Keep native death, corpse and clues.
        var snapshot=Game.Host?DogEvidence.Capture(killer,victim):null;
        if(DetectiveLocationsController.Instance)DetectiveLocationsController.Instance.AddBodyInfo(new DetectiveDeadBodyInfo(victim.PlayerId));
        var body=UnityEngine.Object.Instantiate(GameManager.Instance.GetDeadBody(RoleManager.Instance.GetRole(RoleTypes.Impostor)));
        body.ParentId=victim.PlayerId;body.transform.position=victim.transform.position;body.gameObject.SetActive(true);
        foreach(var renderer in body.bodyRenderers)PlayerMaterial.SetColors(victim.Data.DefaultOutfit.ColorId,renderer);
        victim.Die(DeathReason.Kill,true);
        if(snapshot!=null)DogEvidence.Deaths[victim.PlayerId]=snapshot;
        if(victim.AmOwner)CannibalUI.ShowEating("CAUGHT IN THE BLAST");
    }
    internal static bool Rpc(PlayerControl sender,byte call,MessageReader r)
    {
        if(call!=236&&call!=237)return true;
        try{if(r.ReadByte()!=1||r.ReadInt32()!=Game.Epoch)return false;if(call==236){if(Game.Host)Commit(sender);return false;}if(Game.Host||sender.OwnerId!=AmongUsClient.Instance.HostId)return false;var won=r.ReadBoolean();var count=r.ReadByte();if(count>15)return false;var ids=new byte[count];for(int i=0;i<count;i++)ids[i]=r.ReadByte();Apply(ids,won);}
        catch(Exception e){Plugin.Instance.Log.LogWarning("Terrorist RPC: "+e.Message);}return false;
    }
}
