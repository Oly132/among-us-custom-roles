using BepInEx.Configuration;
using HarmonyLib;
using Hazel;
using UnityEngine;
using AmongUs.GameOptions;

namespace Forger;

internal static class Puppeteer
{
    const byte Request = 250, State = 251;
    internal static ConfigEntry<float> Duration = null!, Cooldown = null!;
    internal static ConfigEntry<int> Uses = null!, Chance = null!;
    internal static byte RoleId = 255, TargetId = 255;
    internal static int Used, Epoch;
    internal static float EndsAt, ReadyAt, KillReadyAt;
    static float nextInput, nextPosition, inputAt;
    static Vector2 direction;
    internal static bool Picker;
    static List<PlayerControl> Players() { var list = new List<PlayerControl>(); foreach (var p in PlayerControl.AllPlayerControls) if (p) list.Add(p); return list; }
    internal static PlayerControl? Find(byte id) => Players().FirstOrDefault(p => p && p.PlayerId == id);
    internal static bool Living(PlayerControl? p) => p is not null && p && p.Data != null && !p.Data.IsDead && !p.Data.Disconnected;
    internal static bool Active => TargetId != 255;
    internal static bool Mine => Game.Local && Game.Local!.PlayerId == RoleId;
    internal static bool ControlledLocal => Active && Game.Local && Game.Local!.PlayerId == TargetId;
    internal static void EvictTask()
    {
        if (!ControlledLocal) return;
        if (Minigame.Instance)
        {
            var task = Minigame.Instance;
            task.ForceClose();
            Plugin.Instance.Log.LogInfo("PUPPETEER closed controlled player's task: " + task.name);
        }
        if (MapBehaviour.Instance && MapBehaviour.Instance.IsOpen) MapBehaviour.Instance.Close();
        Game.Local!.moveable = false;
    }
    internal static bool CanPossess => Mine && !Active && Game.Alive && !MeetingHud.Instance && Time.time >= ReadyAt && Used < Uses.Value;
    internal static void Configure()
    {
        Duration = Plugin.Instance.Config.Bind("Puppeteer", "Possession duration", 10f, new ConfigDescription("Seconds controlling a crewmate.", new AcceptableValueRange<float>(3, 30)));
        Cooldown = Plugin.Instance.Config.Bind("Puppeteer", "Possession cooldown", 30f, new ConfigDescription("Seconds after possession ends.", new AcceptableValueRange<float>(0, 120)));
        Uses = Plugin.Instance.Config.Bind("Puppeteer", "Possessions per game", 3, new ConfigDescription("Maximum possessions per game.", new AcceptableValueRange<int>(1, 15)));
        Chance = Plugin.Instance.Config.Bind("Puppeteer", "Assignment chance", 100, new ConfigDescription("Chance to assign Puppeteer. With one impostor this replaces Forger; set 0 to play Forger instead.", new AcceptableValueRange<int>(0, 100)));
    }
    internal static void Reset(byte id = 255)
    {
        End(false); RoleId = id; Used = 0; ReadyAt = 0; KillReadyAt = 0; Picker = false;
        RegisteredRoles.Apply(id,1);
    }
    static void RecordBody(PlayerControl victim)
    {
        if (!DetectiveLocationsController.Instance)
        {
            foreach (var p in Players())
            { var detective = p.Data?.Role?.TryCast<DetectiveRole>(); if (detective != null) { detective.CreateMapLocations(); break; } }
        }
        if (!DetectiveLocationsController.Instance) return;
        var body = new DetectiveDeadBodyInfo(victim.PlayerId);
        foreach (var p in Players()) if (p.Data != null) body.PlayerLocationsAtDeath[p.PlayerId] = RoomLookup.Locate(p);
        DetectiveLocationsController.Instance.AddBodyInfo(body);
        
    }
    internal static void Broadcast(byte op, Action<MessageWriter>? write = null) => Game.Send(State, w => { w.Write((byte)1); w.Write(Game.Epoch); w.Write(op); write?.Invoke(w); });
    static void Ask(byte op, Action<MessageWriter>? write = null)
    {
        if (!Game.Local) return;
        if (Game.Host) { if (op == 0) return; }
        Game.Send(Request, w => { w.Write((byte)1); w.Write(Game.Epoch); w.Write(op); write?.Invoke(w); }, AmongUsClient.Instance.HostId);
    }
    internal static void Select(byte target)
    {
        Picker = false;
        if (!CanPossess) return;
        if (Game.Host) Start(Game.Local!, target); else Ask(0, w => w.Write(target));
    }
    internal static void Return()
    {
        if (!Mine || !Active) return;
        if (Game.Host) End(); else Ask(3);
    }
    internal static void Start(PlayerControl sender, byte target)
    {
        var pawn = Find(target);
        if (!Game.Host || Active || !Game.Started || MeetingHud.Instance || sender.PlayerId != RoleId || !Living(sender) ||
            sender.inVent || !Living(pawn) || pawn!.inVent || pawn.Data.Role.IsImpostor || Time.time < ReadyAt || Used >= Uses.Value) return;
        TargetId = target; Used++; EndsAt = Time.time + Duration.Value; direction = Vector2.zero;
        EvictTask();
        sender.MyPhysics.SetNormalizedVelocity(Vector2.zero);
        Broadcast(1, w => { w.Write(target); w.Write(Duration.Value); w.Write(Used); });
        Plugin.Instance.Log.LogInfo($"PUPPETEER start: controller={RoleId}, pawn={target}, duration={Duration.Value}");
    }
    internal static void End(bool sync = true)
    {
        if (!Active) return;
        PuppeteerVisual.Restore();
        var target = Find(TargetId); if (target) target!.MyPhysics.SetNormalizedVelocity(Vector2.zero);
        var restoreMovement = ControlledLocal;
        TargetId = 255; direction = Vector2.zero; ReadyAt = Time.time + Cooldown.Value;
        if (restoreMovement && Game.Alive && !MeetingHud.Instance) Game.Local!.moveable = true;
        if (Mine && Game.Local!.lightSource) Game.Local.lightSource.transform.SetParent(Game.Local.transform, false);
        if (Game.Local && HudManager.InstanceExists) HudManager.Instance.PlayerCam.Target = Game.Local;
        if (sync && Game.Host) Broadcast(2, w => w.Write(Cooldown.Value));
        Plugin.Instance.Log.LogInfo("PUPPETEER possession ended");
    }
    internal static void Tick()
    {
        if (!Game.Local || !Game.Started) { if(!Game.WaitingForRoundStart) Reset(); return; }

        if (!Active) return;
        EvictTask();
        var pawn = Find(TargetId); var controller = Find(RoleId);
        if (Time.time >= EndsAt || MeetingHud.Instance || !Living(pawn) || !Living(controller) || pawn!.inVent || controller!.inVent)
        { End(); return; }
        if (Mine)
        {
            HudManager.Instance.PlayerCam.Target = pawn;
            if (controller!.lightSource) controller.lightSource.transform.SetParent(pawn!.transform, false);
            var input = HudManager.Instance.joystick.DeltaL;
            if (Time.time >= nextInput)
            {
                nextInput = Time.time + .05f;
                if (Game.Host) { direction = Vector2.ClampMagnitude(input, 1); inputAt = Time.time; }
                else Ask(1, w => { w.Write(input.x); w.Write(input.y); });
            }
            var victim = Nearest();
            HudManager.Instance.KillButton.SetTarget(victim);
            HudManager.Instance.KillButton.SetCoolDown(Math.Max(controller!.killTimer,KillReadyAt-Time.time), GameOptionsManager.Instance.CurrentGameOptions.GetFloat(FloatOptionNames.KillCooldown));
        }
        if (Game.Host && Time.time >= nextPosition)
        {
            nextPosition = Time.time + .05f;
            var pos = pawn!.NetTransform.transform.position;
            Broadcast(3, w => { w.Write(TargetId); w.Write(pos.x); w.Write(pos.y); });
        }
    }
    internal static PlayerControl? Nearest()
    {
        var pawn = Find(TargetId); if (!pawn) return null;
        var range = GameOptionsManager.Instance.CurrentGameOptions.GetInt(Int32OptionNames.KillDistance);
        var distance = NormalGameOptionsV12.KillDistances[Math.Clamp(range, 0, 2)];
        return Players().Where(p => Living(p) && p.PlayerId != TargetId && p.PlayerId != RoleId && !p.inVent && !p.Data.Role.IsImpostor)
            .Where(p => Vector2.Distance(pawn!.GetTruePosition(), p.GetTruePosition()) <= distance &&
                !Physics2D.Linecast(pawn.GetTruePosition(), p.GetTruePosition(), Constants.ShipAndObjectsMask).collider)
            .OrderBy(p => Vector2.Distance(pawn!.GetTruePosition(), p.GetTruePosition())).FirstOrDefault();
    }
    internal static void Kill()
    {
        if (!Active || !Mine) return;
        var victim = Nearest(); if (!victim) return;
        if (Game.Host) CommitKill(Game.Local!, victim!.PlayerId); else Ask(2, w => w.Write(victim!.PlayerId));
    }
    static void CommitKill(PlayerControl sender, byte victimId)
    {
        if (!Active || !Game.Host || sender.PlayerId != RoleId || !Living(sender) || MeetingHud.Instance || Time.time >= EndsAt || Time.time < KillReadyAt || (sender.AmOwner && sender.killTimer > .05f)) return;
        var victim = Nearest(); var pawn = Find(TargetId);
        if (!victim || victim!.PlayerId != victimId || !pawn) return;
        if(FatGuy.TryAbsorb(sender,victim))return;
        RecordBody(victim);
        pawn!.MurderPlayer(victim, MurderResultFlags.Succeeded);
        KillReadyAt = Time.time + GameOptionsManager.Instance.CurrentGameOptions.GetFloat(FloatOptionNames.KillCooldown);
        if (sender.AmOwner) sender.SetKillTimer(KillReadyAt-Time.time);
        Broadcast(4, w => { w.Write(TargetId); w.Write(victimId); });
        Plugin.Instance.Log.LogInfo($"PUPPETEER kill: true killer={RoleId}, apparent killer={TargetId}, pawn remains impostor={pawn.Data.Role.IsImpostor}, victim={victimId}");
    }
    internal static bool Rpc(PlayerControl sender, byte call, MessageReader r)
    {
        if (call != Request && call != State) return true;
        try
        {
            if (r.ReadByte() != 1) return false;
            var epoch = r.ReadInt32(); var op = r.ReadByte();
            if (call == Request)
            {
                if (!Game.Host || epoch != Game.Epoch || sender.PlayerId != RoleId) return false;
                if (op == 0) Start(sender, r.ReadByte());
                else if (op == 1 && Active) { var x = r.ReadSingle(); var y = r.ReadSingle(); if (float.IsFinite(x) && float.IsFinite(y)) { direction = Vector2.ClampMagnitude(new Vector2(x,y),1); inputAt = Time.time; } }
                else if (op == 2) CommitKill(sender, r.ReadByte());
                else if (op == 3 && Active) End();
                return false;
            }
            if (Game.Host || sender.OwnerId != AmongUsClient.Instance.HostId) return false;
            if (op == 0) { Epoch = epoch; Reset(r.ReadByte()); Duration.Value = r.ReadSingle(); Cooldown.Value = r.ReadSingle(); Uses.Value = r.ReadInt32(); }
            else if (epoch == Epoch)
            {
                if (op == 1) { TargetId = r.ReadByte(); EndsAt = Time.time + r.ReadSingle(); Used = r.ReadInt32(); EvictTask(); }
                else if (op == 2) { var cooldown = r.ReadSingle(); End(false); ReadyAt = Time.time + cooldown; }
                else if (op == 3) { var id = r.ReadByte(); var x = r.ReadSingle(); var y = r.ReadSingle(); if (Active && id == TargetId) { var pawn = Find(id); if (pawn) { direction = (new Vector2(x,y) - (Vector2)pawn!.transform.position).normalized; inputAt = Time.time; pawn.NetTransform.SnapTo(new Vector2(x,y)); } } }
                else if (op == 4) { var id = r.ReadByte(); var victim = r.ReadByte(); var p = Find(id); var v = Find(victim); if (p && Living(v)) { RecordBody(v!); p!.MurderPlayer(v!, MurderResultFlags.Succeeded); } KillReadyAt = Time.time + GameOptionsManager.Instance.CurrentGameOptions.GetFloat(FloatOptionNames.KillCooldown); if (Mine) Game.Local!.SetKillTimer(KillReadyAt-Time.time); }
            }
        }
        catch (Exception e) { Plugin.Instance.Log.LogWarning("Puppeteer packet: " + e.Message); }
        return false;
    }
    internal static bool Physics(PlayerPhysics physics)
    {
        if (!Active || !physics.myPlayer) return true;
        if (physics.myPlayer.PlayerId == RoleId)
        {
            physics.SetNormalizedVelocity(Vector2.zero);
            physics.HandleAnimation(false);
            if(physics.Animations && physics.Animations.IsPlayingRunAnimation())physics.Animations.PlayIdleAnimation();
            return false;
        }
        if (physics.myPlayer.PlayerId != TargetId) return true;
        physics.SetNormalizedVelocity(Time.time - inputAt < .3f ? direction : Vector2.zero);
        physics.HandleAnimation(false);
        if (!Game.Host) physics.SetNormalizedVelocity(Vector2.zero);
        return false;
    }
}

[HarmonyPatch(typeof(Console), nameof(Console.Use))]
internal static class PuppetTaskUsePatch { internal static bool Prefix() => !Puppeteer.ControlledLocal && !(Faker.Active && Faker.Mine); }

[HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.FixedUpdate))]
internal static class PuppetPhysicsPatch { static bool Prefix(PlayerPhysics __instance) => Puppeteer.Physics(__instance); }
[HarmonyPatch(typeof(KillButton), nameof(KillButton.DoClick))]
internal static class PuppetKillPatch { static bool Prefix() { if (Faker.Active && Faker.Mine) return false; if (!Puppeteer.Active || !Puppeteer.Mine) return true; Puppeteer.Kill(); return false; } }
[HarmonyPatch(typeof(SabotageButton), nameof(SabotageButton.DoClick))]
internal static class PuppetSabotagePatch { static bool Prefix() => !(Faker.Active && Faker.Mine) && (!Puppeteer.Active || !Puppeteer.Mine); }
[HarmonyPatch(typeof(CustomNetworkTransform), nameof(CustomNetworkTransform.FixedUpdate))]
internal static class PuppetNetworkPatch
{
    static bool Prefix(CustomNetworkTransform __instance) => !Puppeteer.Active || !__instance.myPlayer || __instance.myPlayer.PlayerId != Puppeteer.TargetId;
}

public sealed class PuppeteerUI : MonoBehaviour
{

    public PuppeteerUI(IntPtr p) : base(p) { }
    void LateUpdate()
    {
        try { PuppeteerVisual.Update(); PuppeteerButton.Update(); PossessionTablet.Update(); }
        catch (Exception e) { Plugin.Instance.Log.LogError("Puppeteer UI: " + e); }
    }
    void OnGUI()
    {
        if(RoleIntro.Visible) return;
        if (!Game.Local || MeetingHud.Instance) return;
        var old = GUI.matrix;
        var scale = Mathf.Min(Screen.width/1280f, Screen.height/720f);
        GUI.matrix = Matrix4x4.TRS(Vector3.zero,Quaternion.identity,new Vector3(scale,scale,1));
        try
        {
            if (!Game.Started) return;
            
            if (!Puppeteer.Mine || !Game.Alive) return;
            PossessionTablet.DrawClicks(scale);
        }
        finally { GUI.enabled=true; GUI.matrix=old; }
    }
    static float Setting(float y,string label,float value,float min,float max,float step)
    {
        GUI.Label(new Rect(405,y,300,30),label);
        GUI.Label(new Rect(715,y,60,30),value.ToString("0"));
        if (GUI.Button(new Rect(775,y,40,30),"−")) value=Math.Max(min,value-step);
        if (GUI.Button(new Rect(830,y,40,30),"+")) value=Math.Min(max,value+step);
        return value;
    }
}













