using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Hazel;
using InnerNet;
using UnityEngine;

namespace Forger;

[BepInPlugin("local.olivi.forger", "Forger", "0.14.1")]
public sealed class Plugin : BasePlugin
{
    internal static Plugin Instance = null!;
    internal static ConfigEntry<int> Chance = null!, Uses = null!;
    internal static ConfigEntry<float> Cooldown = null!, Window = null!;
    public override void Load()
    {
        Instance = this;
        ModMode.Configure();AddComponent<ModModeUI>();AddComponent<AutoUpdateUI>();
        if(!ModMode.Loaded){Log.LogInfo("Custom roles disabled. Gameplay patches were not loaded; vanilla mode active.");return;}
        Chance = Config.Bind("Forger", "Assignment chance", 100, new ConfigDescription("Chance to assign one Forger among the impostors.", new AcceptableValueRange<int>(0, 100)));
        Cooldown = Config.Bind("Forger", "Change evidence cooldown", 20f, new ConfigDescription("Seconds between successful changes.", new AcceptableValueRange<float>(0, 120)));
        Uses = Config.Bind("Forger", "Uses per game", 3, new ConfigDescription("Maximum successful changes per game.", new AcceptableValueRange<int>(1, 15)));
        Window = Config.Bind("Forger", "Change evidence window", 10f, new ConfigDescription("Seconds after your kill to select a false room. Reports close this window.", new AcceptableValueRange<float>(1, 60)));
        new Harmony("local.olivi.forger").PatchAll();
        AddComponent<ForgerUI>();
        
        Silencer.Configure(); AddComponent<SilencerUI>(); Faker.Configure(); AddComponent<FakerUI>();
        Cannibal.Configure();FatGuy.Configure();AddComponent<CannibalUI>();
        DogOwner.Configure();AddComponent<DogOwnerUI>();
        EvilEngineer.Configure();Terrorist.Configure();Binocular.Configure();AddComponent<ExtraRoleUI>();Puppeteer.Configure(); CustomRoles.Configure(); AddComponent<CustomRolesUI>();
        AddComponent<PuppeteerUI>();
        Log.LogInfo("Forger loaded. All players require this mod. Host can edit four settings in the lobby.");
    }
}

internal static class Game
{
    internal const byte RequestRpc = 252, StateRpc = 253;
    internal static readonly EvidenceState Evidence = new();
    internal static int Epoch;
    internal static bool AssignmentQueued, Assigned;
    internal static float AssignAt;
    internal static float AssignmentReceivedAt;
    internal static bool RoundHasStarted;
    internal static bool WaitingForRoundStart => Assigned && !RoundHasStarted && Time.time<AssignmentReceivedAt+15;
    internal static bool PickerOpen, SettingsOpen;
    internal static string Status = "";
    internal static float StatusUntil;
    internal static bool Host => TutorialManager.InstanceExists || (AmongUsClient.Instance && AmongUsClient.Instance.AmHost);
    internal static bool Started => TutorialManager.InstanceExists || (AmongUsClient.Instance && AmongUsClient.Instance.GameState == InnerNetClient.GameStates.Started);
    internal static PlayerControl? Local => PlayerControl.LocalPlayer;
    internal static bool IsForger { get { var p = Local; return p is not null && p && p.Data != null && p.Data.Role != null && p.Data.Role.IsImpostor && p.PlayerId == Evidence.ForgerId; } }
    internal static bool Alive { get { var p = Local; return p is not null && p && p.Data != null && !p.Data.IsDead && !p.Data.Disconnected; } }
    internal static List<PlainShipRoom> Rooms()
    {
        if (!ShipStatus.Instance) return new();
        return ShipStatus.Instance.AllRooms.Where(r => r && r.roomArea && r.RoomId != SystemTypes.Hallway)
            .GroupBy(r => r.RoomId).Select(g => g.First()).ToList();
    }
    internal static string RoomName(byte room) => TranslationController.Instance.GetString((SystemTypes)room);
    internal static void Notify(string message) { Status = message; StatusUntil = Time.time + 4; }
    internal static void Send(byte rpc, Action<MessageWriter> write, int target = -1)
    {
        var player = Local;
        if (player is null || !player || !AmongUsClient.Instance || TutorialManager.InstanceExists) return;
        var writer = AmongUsClient.Instance.StartRpcImmediately(player.NetId, rpc, SendOption.Reliable, target);
        write(writer); AmongUsClient.Instance.FinishRpcImmediately(writer);
    }
    internal static void Broadcast(byte op, Action<MessageWriter>? body = null) => Send(StateRpc, w =>
    {
        w.Write((byte)1); w.Write(op); w.Write(Epoch); body?.Invoke(w);
    });
    internal static void QueueAssignment()
    {
        if (!Host) return;
        Evidence.Reset(byte.MaxValue, Plugin.Uses.Value, Plugin.Cooldown.Value, Plugin.Window.Value);
        Assigned = false; AssignmentQueued = true; AssignAt = Time.time;
        RoundHasStarted=false;
        Epoch = System.Random.Shared.Next(1, int.MaxValue);
    }
    internal static void Tick()
    {
        
        var local = Local;
        if (TutorialManager.InstanceExists && local is not null && local && local.Data != null && local.Data.Role != null && local.Data.Role.IsImpostor && Evidence.ForgerId != local.PlayerId)
        {
            Evidence.Reset(local.PlayerId, Plugin.Uses.Value, Plugin.Cooldown.Value, Plugin.Window.Value);
            Assigned = true;
        }
        if (!Started) { PickerOpen = false; if (Assigned && !WaitingForRoundStart) { Assigned = false; Evidence.Reset(byte.MaxValue, 0, 0, 0); } return; }
        RoundHasStarted=true;
        if (Host && !Assigned && !AssignmentQueued) QueueAssignment();
        TryAssign();
    }
    internal static void TryAssign()
    {
        if (!Host || !AssignmentQueued || Time.time < AssignAt) return;
        var impostors = new List<PlayerControl>();
        foreach (var p in PlayerControl.AllPlayerControls)
            if (p && p.Data != null && p.Data.Role != null && p.Data.Role.IsImpostor && !p.Data.Disconnected) impostors.Add(p);
        if (impostors.Count == 0) return;
        AssignmentQueued = false; Assigned = true;
        AssignmentReceivedAt=Time.time;
        CustomRoles.Assign(impostors);
    }
    internal static void Killed(PlayerControl killer, PlayerControl victim)
    {
        if (!Host || killer.PlayerId != Evidence.ForgerId) return;
        Evidence.Killed(killer.PlayerId, victim.PlayerId, Time.time);
        Broadcast(1, w => w.Write(victim.PlayerId));
    }
    internal static void RequestRoom(byte room)
    {
        var player = Local;
        if (player is null || !player || Evidence.PendingVictim is not byte victim) return;
        PickerOpen = false;
        if (MapBehaviour.Instance) MapBehaviour.Instance.Close();
        if (Host) Commit(player, victim, room);
        else { Send(RequestRpc, w => { w.Write((byte)1); w.Write(Epoch); w.Write(victim); w.Write(room); }, AmongUsClient.Instance.HostId); Notify("Changing evidence…"); }
    }
    internal static void Commit(PlayerControl sender, byte victim, byte room)
    {
        if (!Host || !Started || MeetingHud.Instance || sender.Data == null || sender.Data.IsDead || sender.Data.Disconnected) return;
        if (!Rooms().Any(r => (byte)r.RoomId == room)) return;
        if (!Evidence.Change(sender.PlayerId, victim, room, Time.time)) return;
        Broadcast(2, w => { w.Write(victim); w.Write(room); });
        if (sender.PlayerId == Local?.PlayerId) Notify("Evidence changed: " + RoomName(room));
        Plugin.Instance.Log.LogInfo($"Evidence changed for victim {victim}, suspect {sender.PlayerId}, room {room}");
    }
    internal static bool Rpc(PlayerControl sender, byte callId, MessageReader reader)
    {
        if (callId != RequestRpc && callId != StateRpc) return true;
        try
        {
            if (reader.ReadByte() != 1) return false;
            if (callId == RequestRpc)
            {
                var epoch = reader.ReadInt32(); var victim = reader.ReadByte(); var room = reader.ReadByte();
                if (Host && epoch == Epoch) Commit(sender, victim, room);
                return false;
            }
            if (sender.OwnerId != AmongUsClient.Instance.HostId || Host) return false;
            var op = reader.ReadByte(); var receivedEpoch = reader.ReadInt32();
            if (op == 0)
            {
                Epoch = receivedEpoch; Evidence.Reset(reader.ReadByte(), reader.ReadInt32(), reader.ReadSingle(), reader.ReadSingle()); Assigned = true;
                AssignmentReceivedAt=Time.time;RoundHasStarted=Started;
                RegisteredRoles.Apply(Evidence.ForgerId,0);
            }
            else if (receivedEpoch == Epoch)
            {
                if (op == 1) Evidence.Killed(Evidence.ForgerId, reader.ReadByte(), Time.time);
                else if (op == 2) { var victim = reader.ReadByte(); var room = reader.ReadByte(); Evidence.ReceiveChange(victim, room, Time.time); if (IsForger) Notify("Evidence changed: " + RoomName(room)); }
            }
        }
        catch (Exception e) { Plugin.Instance.Log.LogWarning("Invalid Forger packet: " + e.Message); }
        return false;
    }
}

[HarmonyPatch(typeof(RoleManager), nameof(RoleManager.SelectRoles))]
internal static class AssignmentPatch { static void Prefix(RoleManager __instance) { RegisteredRoles.Ensure(__instance); } static void Postfix() { Game.QueueAssignment(); Game.TryAssign(); } }
[HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
internal static class UpdatePatch { static bool Prefix()=>Game.Local && Game.Local!.Data!=null; static void Postfix() { Game.Tick(); Cannibal.Tick(); DogOwner.Tick(); Puppeteer.Tick(); Faker.Tick(); Silencer.Tick(); } }
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.HandleRpc))]
internal static class RpcPatch { static bool Prefix(PlayerControl __instance, byte callId, MessageReader reader) => Terrorist.Rpc(__instance,callId,reader) && DogOwner.Rpc(__instance,callId,reader) && Cannibal.Rpc(__instance,callId,reader) && CustomRoles.Rpc(__instance, callId, reader) && Silencer.Rpc(__instance, callId, reader) && Faker.Rpc(__instance, callId, reader) && Puppeteer.Rpc(__instance, callId, reader) && Game.Rpc(__instance, callId, reader); }
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.MurderPlayer))]
internal static class KillPatch
{
    static void Prefix(PlayerControl target, ref bool __state) => __state = target && target.Data != null && !target.Data.IsDead;
    static void Postfix(PlayerControl __instance, PlayerControl target, bool __state)
    {
        if(!__state || !target || target.Data==null)return;
        Plugin.Instance.Log.LogInfo($"Death state: host={Game.Host}, killer={__instance.PlayerId}, victim={target.PlayerId}, dead={target.Data.IsDead}, role={target.Data.RoleType}, fatProtectionSpent={FatGuy.StabbingSpent}");
        if(target.Data.IsDead)Game.Killed(__instance,target);
    }
}
[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
internal static class MeetingPatch
{
    static void Postfix() { Game.Evidence.Meeting(); Game.PickerOpen = false; Puppeteer.End(); Faker.End(); Silencer.MeetingStart(); DogOwner.Meeting(); HungerEvidence.Clear(); }
}
[HarmonyPatch(typeof(DetectiveRole), nameof(DetectiveRole.GetPlayerLocation))]
internal static class DetectivePatch
{
    static bool Prefix(byte playerID, byte victimID, ref string __result)
    {
        if (!Game.Evidence.TryGet(playerID, victimID, out var room)) return true;
        __result = Game.RoomName(room); return false;
    }
}

[HarmonyPatch(typeof(DetectiveDeadBodyInfo), nameof(DetectiveDeadBodyInfo.GetPlayerLocation))]
internal static class DetectiveCasePatch
{
    static bool Prefix(DetectiveDeadBodyInfo __instance, byte playerID, ref string __result)
    {
        if (!Game.Evidence.TryGet(playerID, __instance.victimID, out var room)) return true;
        __result = Game.RoomName(room); return false;
    }
}










