using AmongUs.GameOptions;
using BepInEx.Configuration;
using HarmonyLib;
using Hazel;
using UnityEngine;
namespace Forger;
internal static class Cannibal
{
    internal const int Index=4;
    const byte Request=240,State=241;
    internal static byte RoleId=255;
    internal static int Epoch;
    internal static float ReadyAt;
    internal static bool Won;
    internal static CachedPlayerData? Winner;
    internal static readonly HashSet<byte> Eaten=new();
    internal static readonly HashSet<byte> EatingAnimations=new();
    internal static ConfigEntry<int> Chance=null!;
    internal static ConfigEntry<float> Cooldown=null!;
    internal static bool Mine=>Game.Local && Game.Local!.PlayerId==RoleId;
    internal static bool Alive=>Puppeteer.Living(Puppeteer.Find(RoleId));
    internal static readonly Color Color=new(.72f,.26f,.9f,1);
    internal static void Configure()
    {
        Chance=Plugin.Instance.Config.Bind("Cannibal","Assignment chance",0,new ConfigDescription("Chance for an independent Cannibal. Wins alone with at most one other survivor.",new AcceptableValueRange<int>(0,100)));
        Cooldown=Plugin.Instance.Config.Bind("Cannibal","Eat cooldown",30f,new ConfigDescription("Seconds between successful eats.",new AcceptableValueRange<float>(5,120)));
    }
    internal static void Reset(byte id=255){CannibalUI.ClearTarget();RoleId=id;Epoch=Game.Epoch;ReadyAt=Time.time+10;Won=false;Winner=null;Eaten.Clear();EatingAnimations.Clear();HungerEvidence.Clear();RegisteredRoles.Apply(id,Index);}
    internal static void Broadcast(byte op,Action<MessageWriter>? write=null)=>Game.Send(State,w=>{w.Write((byte)1);w.Write(Game.Epoch);w.Write(op);write?.Invoke(w);});
    internal static PlayerControl? Nearest(PlayerControl? source=null)
    {
        source??=Game.Local;if(!Puppeteer.Living(source))return null;
        var distance=NormalGameOptionsV12.KillDistances[Math.Clamp(GameOptionsManager.Instance.CurrentGameOptions.GetInt(Int32OptionNames.KillDistance),0,2)];
        return PlayerControl.AllPlayerControls.ToArray().Where(p=>Puppeteer.Living(p) && p.PlayerId!=source!.PlayerId && !p.inVent && Vector2.Distance(source.GetTruePosition(),p.GetTruePosition())<=distance && !Physics2D.Linecast(source.GetTruePosition(),p.GetTruePosition(),Constants.ShipAndObjectsMask).collider).OrderBy(p=>Vector2.Distance(source!.GetTruePosition(),p.GetTruePosition())).ThenBy(p=>p.PlayerId).FirstOrDefault();
    }
    internal static bool CanEat=>Mine && Game.Started && Game.Alive && !Game.Local!.inVent && Game.Local.moveable && !MeetingHud.Instance && !Minigame.Instance && !RoleIntro.Visible && Time.time>=ReadyAt && Nearest();
    internal static void Eat()
    {
        if(!CanEat)return;var target=Nearest()!;
        if(Game.Host)Commit(Game.Local!,target.PlayerId);
        else Game.Send(Request,w=>{w.Write((byte)1);w.Write(Game.Epoch);w.Write(target.PlayerId);},AmongUsClient.Instance.HostId);
    }
    internal static bool Commit(PlayerControl sender,byte victim)
    {
        if(!Game.Host || !Game.Started || sender.PlayerId!=RoleId || !Puppeteer.Living(sender) || sender.inVent || !sender.moveable || MeetingHud.Instance || Time.time<ReadyAt)return false;
        var target=Nearest(sender);if(!target || target!.PlayerId!=victim)return false;
        var pos=target.GetTruePosition();var half=FatGuy.Is(target);
        ApplyEat(sender,target,pos,half);
        Broadcast(1,w=>{w.Write(sender.PlayerId);w.Write(victim);w.Write(pos.x);w.Write(pos.y);w.Write(half);w.Write(Cooldown.Value);});
        Plugin.Instance.Log.LogInfo($"CANNIBAL eat: killer={sender.PlayerId}, victim={victim}, half body={half}");return true;
    }
    internal static void ApplyEat(PlayerControl killer,PlayerControl victim,Vector2 position,bool half)
    {
        HungerEvidence.Add(victim.PlayerId,killer.PlayerId,position,half,true);Eaten.Add(victim.PlayerId);
        if(Puppeteer.Living(victim)){EatingAnimations.Add(victim.PlayerId);killer.MurderPlayer(victim,MurderResultFlags.Succeeded);}
        ReadyAt=Time.time+Cooldown.Value;
        CannibalUI.ClearTarget();
    }
    internal static void CompleteAnimation(PlayerControl source,PlayerControl target)
    {
        EatingAnimations.Remove(target.PlayerId);
        var local=Game.Local;
        if(!local||!HudManager.Instance||(!source.AmOwner&&!target.AmOwner)||MeetingHud.Instance||ExileController.Instance||Minigame.Instance||Puppeteer.Active)return;
        HudManager.Instance.PlayerCam.SetTarget(local!);HudManager.Instance.PlayerCam.Locked=false;
    }
    internal static int LivingCount=>PlayerControl.AllPlayerControls.ToArray().Count(Puppeteer.Living);
    internal static bool MeetsWin=>Alive && LivingCount<=2;
    internal static void SetWin()
    {
        Won=true;var p=Puppeteer.Find(RoleId);if(p && p!.Data!=null)Winner=new CachedPlayerData(p.Data);
    }
    internal static void Tick()
    {
        if(!Game.Started)return;
        foreach(var body in UnityEngine.Object.FindObjectsOfType<DeadBody>())
        {
            if(!Eaten.Contains(body.ParentId))continue;
            if(!HungerEvidence.Traces.TryGetValue(body.ParentId,out var trace) || !trace.HalfBody)
            {
                // Native kill animation still references this corpse before it
                // unlocks the camera. Hide it immediately, destroy after cleanup.
                body.Reported=true;if(body.myCollider)body.myCollider.enabled=false;
                foreach(var renderer in body.bodyRenderers)if(renderer)renderer.enabled=false;
                if(body.bloodSplatter)body.bloodSplatter.enabled=false;
                if(!EatingAnimations.Contains(body.ParentId)){body.gameObject.SetActive(false);UnityEngine.Object.Destroy(body.gameObject);}
            }
        }
        if(Game.Host && MeetsWin && !Won && !MeetingHud.Instance && !ExileController.Instance)
        {
            SetWin();Broadcast(3);GameManager.Instance.RpcEndGame(GameOverReason.ImpostorsByKill,false);
        }
    }
    internal static void FilterWinners()
    {
        if(Won && Winner!=null){EndGameResult.CachedWinners.Clear();EndGameResult.CachedWinners.Add(Winner);return;}
        var p=Puppeteer.Find(RoleId);if(!p || p!.Data==null)return;
        for(int i=EndGameResult.CachedWinners.Count-1;i>=0;i--)
        {
            var winner=EndGameResult.CachedWinners[i];
            if(RegisteredRoles.Index(winner.RoleWhenAlive)==Index || (winner.PlayerName==p.Data.PlayerName && winner.ColorId==p.Data.DefaultOutfit.ColorId))EndGameResult.CachedWinners.RemoveAt(i);
        }
    }
    internal static bool BlocksVanillaEnd(GameOverReason reason)=>Alive && !Won && reason is GameOverReason.CrewmatesByVote or GameOverReason.ImpostorsByVote or GameOverReason.ImpostorsByKill or GameOverReason.ImpostorDisconnect or GameOverReason.CrewmateDisconnect;
    internal static bool Rpc(PlayerControl sender,byte call,MessageReader r)
    {
        if(call!=Request && call!=State)return true;
        try
        {
            if(r.ReadByte()!=1)return false;var epoch=r.ReadInt32();
            if(call==Request){if(Game.Host && epoch==Game.Epoch && sender.PlayerId==RoleId)Commit(sender,r.ReadByte());return false;}
            if(Game.Host || sender.OwnerId!=AmongUsClient.Instance.HostId || epoch!=Epoch)return false;
            var op=r.ReadByte();
            if(op==1){var killer=Puppeteer.Find(r.ReadByte());var victim=Puppeteer.Find(r.ReadByte());var x=r.ReadSingle();var y=r.ReadSingle();var half=r.ReadBoolean();var cd=r.ReadSingle();if(float.IsFinite(x)&&float.IsFinite(y)&&killer&&victim){ApplyEat(killer!,victim!,new Vector2(x,y),half);ReadyAt=Time.time+cd;}}
            else if(op==2){var attacker=r.ReadByte();var target=r.ReadByte();if(target==FatGuy.RoleId)FatGuy.ApplyAbsorb(attacker);}
            else if(op==3)SetWin();
        }
        catch(Exception e){Plugin.Instance.Log.LogWarning("Cannibal/Fat Guy RPC: "+e.Message);}
        return false;
    }
}
[HarmonyPatch(typeof(KillAnimation._CoPerformKill_d__2),nameof(KillAnimation._CoPerformKill_d__2.MoveNext))]
internal static class CannibalNativeKillCleanup
{
    static void Postfix(KillAnimation._CoPerformKill_d__2 __instance,bool __result){if(!__result)Complete(__instance);}
    static Exception? Finalizer(KillAnimation._CoPerformKill_d__2 __instance,Exception? __exception){if(__exception!=null)Complete(__instance);return __exception;}
    static void Complete(KillAnimation._CoPerformKill_d__2 animation){if(animation.source&&animation.target&&animation.source.PlayerId==Cannibal.RoleId&&Cannibal.Eaten.Contains(animation.target.PlayerId))Cannibal.CompleteAnimation(animation.source,animation.target);}
}
[HarmonyPatch(typeof(GameManager),nameof(GameManager.RpcEndGame))]
internal static class NeutralEndGate {static bool Prefix(GameOverReason endReason)=>!Cannibal.BlocksVanillaEnd(endReason);}
[HarmonyPatch(typeof(EndGameManager),nameof(EndGameManager.Start))]
internal static class CannibalWinnerScreen
{
    static void Prefix()=>Cannibal.FilterWinners();
}
[HarmonyPatch(typeof(EndGameManager),nameof(EndGameManager.SetEverythingUp))]
internal static class CannibalWinnerTint {static void Postfix(EndGameManager __instance){if(Cannibal.Won){__instance.BackgroundBar.material.color=Cannibal.Color;__instance.WinText.text=Cannibal.Mine?"Victory":"Defeat";__instance.WinText.color=Cannibal.Mine?Cannibal.Color:Palette.ImpostorRed;}else if(Cannibal.Mine){__instance.WinText.text="Defeat";__instance.WinText.color=Palette.ImpostorRed;}}}
[HarmonyPatch(typeof(IntroCutscene),nameof(IntroCutscene.BeginCrewmate))]
internal static class CannibalNativeIntro
{
    static void Prefix(ref Il2CppSystem.Collections.Generic.List<PlayerControl> teamToDisplay){if(!Cannibal.Mine)return;teamToDisplay=new();teamToDisplay.Add(Game.Local!);}
    static void Postfix(IntroCutscene __instance){if(!Cannibal.Mine)return;__instance.TeamTitle.text="NEUTRAL";__instance.TeamTitle.color=Cannibal.Color;__instance.BackgroundBar.material.color=Cannibal.Color;__instance.ImpostorText.text="Eat everyone. Win alone.";}
}



