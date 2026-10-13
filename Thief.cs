using AmongUs.GameOptions;
using BepInEx.Configuration;
using HarmonyLib;
using Hazel;
using UnityEngine;
namespace Forger;
internal static class Thief
{
    internal const int Index=10;
    const byte Request=234,StateRpc=235;
    internal static readonly ThiefState State=new();
    internal static readonly Dictionary<byte,RoleTypes> Transferred=new();
    static int transferFrame=-1;
    internal static bool LocalUnspent;
    internal static void RememberLocalResult(){if(Game.Started && Game.Local)LocalUnspent=Game.Local!.PlayerId==State.Owner && !State.Used;}
    internal static bool Transitioning=>Time.frameCount==transferFrame;
    internal static void FilterWinners()
    {
        for(var i=EndGameResult.CachedWinners.Count-1;i>=0;i--)
            if(RegisteredRoles.Index(EndGameResult.CachedWinners[i].RoleWhenAlive)==Index)
                EndGameResult.CachedWinners.RemoveAt(i);
    }
    internal static ConfigEntry<int> Chance=null!;
    internal static bool Mine=>Game.Local && Game.Local!.PlayerId==State.Owner && !State.Used && Game.Local.Data?.Role!=null && (CustomRoles.CompleteAssignment?CustomRoles.AssignedIds[Index]==Game.Local.PlayerId:RegisteredRoles.Index(Game.Local.Data.Role.Role)==Index);
    internal static void Configure()=>Chance=Plugin.Instance.Config.Bind("Thief","Assignment chance",0,new ConfigDescription("Chance for one neutral Thief. Steals one role per game and adopts its team and win condition.",new AcceptableValueRange<int>(0,100)));
    internal static void Reset(byte owner=255){State.Reset(owner);LocalUnspent=Game.Local && Game.Local!.PlayerId==owner;Transferred.Clear();transferFrame=-1;ThiefUI.ClearTarget();RegisteredRoles.Apply(owner,Index);}
    internal static PlayerControl? Target(PlayerControl? actor=null)
    {
        actor??=Game.Local;if(!Puppeteer.Living(actor))return null;
        var range=NormalGameOptionsV12.KillDistances[Math.Clamp(GameOptionsManager.Instance.CurrentGameOptions.GetInt(Int32OptionNames.KillDistance),0,2)];
        return PlayerControl.AllPlayerControls.ToArray().Where(p=>Puppeteer.Living(p) && p.PlayerId!=actor!.PlayerId && !p.inVent && p.Visible && p.Data.Role!=null && Vector2.Distance(actor.GetTruePosition(),p.GetTruePosition())<=range && !RolePhysics.Blocked(actor.GetTruePosition(),p.GetTruePosition())).OrderBy(p=>Vector2.Distance(actor!.GetTruePosition(),p.GetTruePosition())).ThenBy(p=>p.PlayerId).FirstOrDefault();
    }
    internal static bool CanSteal=>Mine && Game.Started && Game.Alive && Game.Local!.CanMove && !Game.Local.inVent && !MeetingHud.Instance && !Minigame.Instance && !ExileController.Instance && !RoleIntro.Visible && Target();
    internal static void Press()
    {
        if(!CanSteal)return;var target=Target()!;
        if(Game.Host)Commit(Game.Local!,target.PlayerId);
        else Game.Send(Request,w=>{w.Write((byte)1);w.Write(Game.Epoch);w.Write(target.PlayerId);},AmongUsClient.Instance.HostId);
    }
    static void Commit(PlayerControl actor,byte targetId)
    {
        if(!Game.Host || !Game.Started || !Puppeteer.Living(actor) || actor.PlayerId!=State.Owner || State.Used || actor.Data.Role==null || RegisteredRoles.Index(actor.Data.Role.Role)!=Index || !actor.CanMove || actor.inVent || MeetingHud.Instance || ExileController.Instance || RoleIntro.Visible)return;
        var target=Target(actor);if(!target || target!.PlayerId!=targetId)return;
        var role=target.Data.Role.Role;
        if(!Apply(actor.PlayerId,targetId,role))return;
        // Ownership reaches clients first, so the native alignment RPC cannot
        // restore the victim's old custom role during a later data update.
        Game.Send(StateRpc,w=>{w.Write((byte)1);w.Write(Game.Epoch);w.Write(actor.PlayerId);w.Write(targetId);w.Write((ushort)role);});
        target.RpcSetRole(RoleTypes.Crewmate,true);actor.RpcSetRole(role,true);
        Plugin.Instance.Log.LogInfo($"THIEF role transfer: thief={actor.PlayerId}, victim={targetId}, role={role}");
    }
    internal static bool Apply(byte actorId,byte victimId,RoleTypes stolen)
    {
        var actor=Puppeteer.Find(actorId);var victim=Puppeteer.Find(victimId);
        if(State.Used || !actor || !victim || actor!.Data==null || victim!.Data==null || !RoleManager.Instance.GetRole(stolen) || RegisteredRoles.Index(stolen)==Index)return false;
        var index=RegisteredRoles.Index(stolen);
        // A delayed role request must fail cleanly if its ownership snapshot is
        // no longer current, rather than throwing from the native button callback.
        var assigned=CustomRoles.AssignedIds;
        if(assigned.Length<=Index || assigned[Index]!=actorId || actorId==victimId ||
            (index>=0 && (index>=assigned.Length || assigned[index]!=victimId)))return false;
        var owners=ThiefState.TransferOwners(CustomRoles.AssignedIds,Index,actorId,victimId,index);
        if(!State.TryUse(actorId,victimId))return false;
        RememberLocalResult();
        transferFrame=Time.frameCount;
        if(Puppeteer.RoleId==victimId){Puppeteer.End(false);Puppeteer.Picker=false;if(PossessionTablet.IsOpen)PossessionTablet.Close();}
        if(Faker.RoleId==victimId)Faker.End(false);
        if(Binocular.RoleId==victimId)Binocular.Close();
        if(DogOwner.RoleId==victimId)DogOwner.Cancel();
        if(victim.Data.Role.Role==RoleTypes.Shapeshifter)victim.Shapeshift(victim,false);
        if(victim.Data.Role.Role==RoleTypes.Phantom)victim.SetRoleInvisibility(false,false,false);
        if(Game.Local && (Game.Local!.PlayerId==victimId || Game.Local.PlayerId==actorId))
        {
            if(Minigame.Instance)Minigame.Instance.ForceClose();
            if(MapBehaviour.Instance && MapBehaviour.Instance.IsOpen)MapBehaviour.Instance.Close();
            Game.PickerOpen=false;
        }
        CustomRoles.AssignedIds=owners;
        // Transfer the existing role state; do not refill powers or erase evidence.
        switch(index)
        {
            case 0:Game.Evidence.Transfer(actorId);break;
            case 1:Puppeteer.RoleId=actorId;break;
            case 2:Faker.RoleId=actorId;break;
            case 3:Silencer.RoleId=actorId;break;
            case 4:Cannibal.RoleId=actorId;break;
            case 5:FatGuy.RoleId=actorId;break;
            case 6:DogOwner.RoleId=actorId;DogOwner.LastFindings="";break;
            case 7:Terrorist.RoleId=actorId;break;
            case 8:Binocular.RoleId=actorId;break;
            case 9:EvilEngineer.RoleId=actorId;break;
        }
        Transferred[victimId]=RoleTypes.Crewmate;Transferred[actorId]=stolen;
        RoleManager.Instance.SetRole(victim!,victim!.Data.IsDead?RoleTypes.CrewmateGhost:RoleTypes.Crewmate);
        RoleManager.Instance.SetRole(actor!,actor!.Data.IsDead?(RoleManager.Instance.GetRole(stolen).IsImpostor?RoleTypes.ImpostorGhost:RoleTypes.CrewmateGhost):stolen);
        foreach(var p in new[]{actor!,victim!})if(p.AmOwner && HudManager.Instance)
        {
            // Native role instructions are tasks too; remove the old alignment's
            // wording without recreating normal tasks or resetting their progress.
            if(!p.Data.IsDead)foreach(var task in p.myTasks.ToArray())if(task && task.TryCast<ImportantTextTask>()!=null)p.RemoveTask(task);
            p.moveable=true;HudManager.Instance.PlayerCam.SetTarget(p);HudManager.Instance.PlayerCam.Locked=false;
            HudManager.Instance.SetHudActive(p,p.Data.Role,true);
            if(p.Data.Role.IsImpostor)p.SetKillTimer(GameOptionsManager.Instance.CurrentGameOptions.GetFloat(FloatOptionNames.KillCooldown));
            HudManager.Instance.Dialogue.Show(p.PlayerId==actorId?$"ROLE STOLEN\nYou are now {TranslationController.Instance.GetString(RoleManager.Instance.GetRole(stolen).StringName)}.":"ROLE STOLEN\nYou are now a Crewmate.");
        }
        ThiefUI.ClearTarget();return true;
    }
    internal static bool Rpc(PlayerControl sender,byte call,MessageReader r)
    {
        if(call!=Request && call!=StateRpc)return true;
        try
        {
            if(r.ReadByte()!=1 || r.ReadInt32()!=Game.Epoch)return false;
            if(call==Request){if(Game.Host)Commit(sender,r.ReadByte());return false;}
            if(Game.Host || sender.OwnerId!=AmongUsClient.Instance.HostId)return false;
            var actor=r.ReadByte();var victim=r.ReadByte();var role=(RoleTypes)r.ReadUInt16();
            if(!State.Used)Apply(actor,victim,role);
        }
        catch(Exception e){Plugin.Instance.Log.LogWarning("Thief packet: "+e.Message);}
        return false;
    }
}

[HarmonyPatch(typeof(EndGameManager),nameof(EndGameManager.Start))]
internal static class NeutralThiefWinners {static void Prefix()=>Thief.FilterWinners();}
[HarmonyPatch(typeof(EndGameManager),nameof(EndGameManager.SetEverythingUp))]
internal static class NeutralThiefDefeat
{
    static void Postfix(EndGameManager __instance)
    {
        if(!Thief.LocalUnspent)return;
        __instance.WinText.text="Defeat";__instance.WinText.color=Palette.ImpostorRed;
    }
}
[HarmonyPatch(typeof(RoleManager),nameof(RoleManager.SetRole))]
internal static class PreserveStolenCustomRole
{
    static void Prefix(PlayerControl targetPlayer,ref RoleTypes roleType)
    {
        if(!Game.Assigned || !Game.Started || !targetPlayer || targetPlayer.Data==null || !Thief.Transferred.TryGetValue(targetPlayer.PlayerId,out var role))return;
        if(targetPlayer.Data.IsDead && (roleType==role || roleType==RoleTypes.Crewmate || roleType==RoleTypes.Impostor))
        {roleType=RoleManager.Instance.GetRole(role).IsImpostor?RoleTypes.ImpostorGhost:RoleTypes.CrewmateGhost;return;}
        if(RegisteredRoles.Index(role)>=0 && roleType==RegisteredRoles.WireType(role))roleType=role;
    }
}
