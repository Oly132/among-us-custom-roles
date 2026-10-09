using BepInEx.Configuration;
using HarmonyLib;
using Hazel;
using UnityEngine;
using UnityEngine.Events;
namespace Forger;

internal static class Faker
{
    const byte Request=248,State=249;
    internal static byte RoleId=255;
    internal static bool Used,Active;
    internal static TaskTypes Task;
    internal static NormalPlayerTask? FakeTask;
    static Minigame? mini;
    static float diagnosticAt;
    static int epoch;
    static ShipStatus? ship;
    static Console[] consoles=Array.Empty<Console>();
    internal static ConfigEntry<int> Chance=null!;
    internal static bool Mine => Game.Local && Game.Local!.PlayerId==RoleId;
    internal static void Configure() => Chance=Plugin.Instance.Config.Bind("Faker","Assignment chance",100,new ConfigDescription("Chance to assign a Faker. One completed fake visual task per game; canceling keeps the use. With too few impostors this replaces an existing custom role.",new AcceptableValueRange<int>(0,100)));
    internal static void Reset(byte id=255) { End(false);RoleId=id;Used=false;RegisteredRoles.Apply(id,2); }
    static void Send(byte op,Action<MessageWriter>? body=null) => Game.Send(State,w=>{w.Write((byte)1);w.Write(Game.Epoch);w.Write(op);body?.Invoke(w);});
    static void Ask(byte op,Action<MessageWriter>? body=null) => Game.Send(Request,w=>{w.Write((byte)1);w.Write(Game.Epoch);w.Write(op);body?.Invoke(w);},AmongUsClient.Instance.HostId);
    static bool Has(Console c,TaskTypes t) => (c.TaskTypes!=null && c.TaskTypes.Contains(t)) || (c.ValidTasks!=null && c.ValidTasks.Any(v=>v.taskType==t));
    internal static List<(Console console,TaskTypes task)> Stations()
    {
        if(ship!=ShipStatus.Instance) { ship=ShipStatus.Instance;consoles=UnityEngine.Object.FindObjectsOfType<Console>().ToArray(); }
        var list=new List<(Console,TaskTypes)>();if(!ship) return list;
        foreach(var c in consoles)
        {
            if(!c || !c.gameObject.activeInHierarchy) continue;
            if(ship!.MedScanner && Has(c,TaskTypes.SubmitScan)) list.Add((c,TaskTypes.SubmitScan));
            else if(ship.WeaponsImage && Has(c,TaskTypes.ClearAsteroids)) list.Add((c,TaskTypes.ClearAsteroids));
            else if(ship.Hatch && c.Room==SystemTypes.Storage && (Has(c,TaskTypes.EmptyGarbage)||Has(c,TaskTypes.EmptyChute))) list.Add((c,TaskTypes.EmptyGarbage));
        }
        return list;
    }
    internal static Console? Nearby(PlayerControl p,TaskTypes type)
    {
        foreach(var entry in Stations().Where(e=>e.task==type).OrderBy(e=>Vector2.Distance(p.GetTruePosition(),e.console.transform.position)))
        {
            var c=entry.console;var pos=p.GetTruePosition();
            if(Vector2.Distance(pos,c.transform.position)>c.UsableDistance+.15f) continue;
            if(c.onlyFromBelow && pos.y>c.transform.position.y) continue;
            if(c.onlySameRoom && RoomLookup.Locate(p)!=Game.RoomName((byte)c.Room)) continue;
            if(c.checkWalls && Physics2D.Linecast(pos,c.transform.position,Constants.ShipAndObjectsMask).collider) continue;
            return c;
        }
        return null;
    }
    internal static TaskTypes? Available()
    {
        if(!Mine || Used || Active || !Game.Alive || !Game.Local!.CanMove || Game.Local.inVent || MeetingHud.Instance || Minigame.Instance || Puppeteer.Active) return null;
        foreach(var t in new[]{TaskTypes.SubmitScan,TaskTypes.ClearAsteroids,TaskTypes.EmptyGarbage}) if(Nearby(Game.Local,t)) return t;
        return null;
    }
    internal static void Press()
    {
        var t=Available();if(t==null) return;
        if(Game.Host) Begin(Game.Local!,t.Value);else Ask(0,w=>w.Write((byte)t.Value));
    }
    internal static void Begin(PlayerControl p,TaskTypes t)
    {
        if(!Game.Host || !Game.Started || Used || Active || !Puppeteer.Living(p) || p.PlayerId!=RoleId || !p.Data.Role.IsImpostor || p.inVent || MeetingHud.Instance || Puppeteer.Active || !Nearby(p,t)) return;
        if(t==TaskTypes.SubmitScan)
        {
            var system=ScanSystem();
            if(system!=null) { if(!system.UsersList.Contains(RoleId)) system.UsersList.Add(RoleId);system.IsDirty=true;system.Deteriorate(0); }
        }
        Send(1,w=>w.Write((byte)t));ApplyBegin(t);
    }
    static MedScanSystem? ScanSystem() => ShipStatus.Instance && ShipStatus.Instance.Systems.ContainsKey(SystemTypes.MedBay) ? ShipStatus.Instance.Systems[SystemTypes.MedBay].TryCast<MedScanSystem>() : null;
    static void ApplyBegin(TaskTypes t)
    {
        Task=t;Active=true;
        if(!Mine) return;
        try
        {
            var c=Nearby(Game.Local!,t);if(!c) throw new Exception("Task station is no longer in range.");
            var prefab=ShipStatus.Instance.GetAllTasks().FirstOrDefault(x=>x.TaskType==t && x.GetMinigamePrefab());
            if(!prefab && t==TaskTypes.EmptyGarbage) prefab=ShipStatus.Instance.GetAllTasks().FirstOrDefault(x=>x.TaskType==TaskTypes.EmptyChute && x.GetMinigamePrefab());
            if(!prefab) throw new Exception("Native task prefab unavailable.");
            FakeTask=UnityEngine.Object.Instantiate(prefab!,Game.Local!.transform).TryCast<NormalPlayerTask>();
            if(!FakeTask) throw new Exception("Native task is not a normal task.");
            FakeTask!.name="FakerTemporaryTask";FakeTask.Owner=Game.Local;FakeTask.Id=uint.MaxValue;FakeTask.Initialize();
            if(t==TaskTypes.EmptyGarbage) FakeTask.taskStep=Math.Max(0,FakeTask.MaxStep-1);
            mini=UnityEngine.Object.Instantiate(FakeTask!.GetMinigamePrefab(),HudManager.Instance.transform);mini.Console=c;
            mini.transform.localPosition=new Vector3(0,0,-50);
            if(t==TaskTypes.SubmitScan) Game.Local!.NetTransform.SnapTo((Vector2)ShipStatus.Instance.MedScanner.Position);
            mini.Begin(FakeTask);diagnosticAt=Time.time+2;
            if(t==TaskTypes.SubmitScan) { var scan=mini.TryCast<MedScanMinigame>();Plugin.Instance.Log.LogInfo($"FAKER scan: current={scan?.medscan?.CurrentUser}, queue={scan?.medscan?.UsersList.Count}, enabled={mini.enabled}, duration={scan?.ScanDuration}"); }
            Plugin.Instance.Log.LogInfo($"FAKER opened real task: {t}, steps={FakeTask.taskStep}/{FakeTask.MaxStep}, use remains available");
        }
        catch(Exception e) { Plugin.Instance.Log.LogError("Faker task: "+e);Cancel(); }
    }
    internal static bool IsFake(PlayerTask task) => FakeTask && task && task.Pointer==FakeTask!.Pointer;
    internal static void Complete()
    {
        if(!Mine || !Active || Used) return;
        if(Game.Host) Consume();else Ask(1);
    }
    static void Consume()
    {
        if(!Active || Used) return;Used=true;Send(2);
        Plugin.Instance.Log.LogInfo($"FAKER completed: {Task}; the single use is now consumed");
    }
    static void Cancel() { if(Game.Host) End();else Ask(2); }
    internal static void End(bool sync=true)
    {
        if(!Active && !FakeTask) return;
        var p=Puppeteer.Find(RoleId);
        if(p && Task==TaskTypes.SubmitScan) p!.SetScanner(false,++p.scannerCount);
        if(Game.Host && Task==TaskTypes.SubmitScan) { var system=ScanSystem();if(system!=null) { system.UsersList.Remove(RoleId);system.IsDirty=true;system.Deteriorate(0); } }
        Active=false;
        if(mini) mini!.ForceClose();mini=null;
        if(FakeTask) UnityEngine.Object.Destroy(FakeTask!.gameObject);FakeTask=null;
        if(Mine && Game.Alive && !MeetingHud.Instance) { Game.Local!.moveable=true;if(HudManager.InstanceExists) HudManager.Instance.SetHudActive(Game.Local,Game.Local.Data.Role,true); }
        if(sync && Game.Host) Send(3);
        Plugin.Instance.Log.LogInfo("FAKER task closed; used="+Used);
    }
    internal static void Tick()
    {
        if(!Game.Local || !Game.Started) { if(!Game.WaitingForRoundStart) Reset();return; }

        if(!Active) return;
        if(Mine && Task==TaskTypes.SubmitScan && diagnosticAt>0 && Time.time>=diagnosticAt) { diagnosticAt=0;var scan=mini?.TryCast<MedScanMinigame>();Plugin.Instance.Log.LogInfo($"FAKER scan progress: state={scan?.state}, walking={scan?.walking!=null}, timer={scan?.ScanTimer}, current={scan?.medscan?.CurrentUser}, position={Game.Local!.GetTruePosition()}, pad={ShipStatus.Instance.MedScanner.Position}"); }
        if(Game.Host && Task==TaskTypes.SubmitScan) ScanSystem()?.Deteriorate(Time.deltaTime);
        if(!Puppeteer.Living(Puppeteer.Find(RoleId))||MeetingHud.Instance) { End();return; }
        if(Mine && !mini) Cancel();
    }
    internal static bool Rpc(PlayerControl p,byte call,MessageReader r)
    {
        if(call!=Request && call!=State) return true;
        try
        {
            if(r.ReadByte()!=1) return false;var received=r.ReadInt32();
            if(call==Request)
            {
                if(Game.Host && received==Game.Epoch && p.PlayerId==RoleId)
                { var op=r.ReadByte();if(op==0) Begin(p,(TaskTypes)r.ReadByte());else if(op==1) Consume();else if(op==2) End(); }
                return false;
            }
            if(Game.Host || p.OwnerId!=AmongUsClient.Instance.HostId) return false;
            var code=r.ReadByte();
            if(code==0) { epoch=received;Reset(r.ReadByte()); }
            else if(received==epoch) { if(code==1) ApplyBegin((TaskTypes)r.ReadByte());else if(code==2) Used=true;else if(code==3) End(false); }
        }
        catch(Exception e) { Plugin.Instance.Log.LogWarning("Faker packet: "+e.Message); }
        return false;
    }
}

[HarmonyPatch(typeof(NormalPlayerTask),nameof(NormalPlayerTask.NextStep))]
internal static class FakerStepPatch
{
    static bool Prefix(NormalPlayerTask __instance)
    {
        if(!Faker.IsFake(__instance)) return true;
        __instance.taskStep=Math.Min(__instance.MaxStep,__instance.taskStep+1);
        if(__instance.IsComplete) Faker.Complete();
        return false;
    }
}
[HarmonyPatch(typeof(NormalPlayerTask),nameof(NormalPlayerTask.Complete))]
internal static class FakerCompletePatch { static bool Prefix(NormalPlayerTask __instance) { if(!Faker.IsFake(__instance)) return true;Faker.Complete();return false; } }
[HarmonyPatch(typeof(PlayerControl),nameof(PlayerControl.RpcCompleteTask))]
internal static class FakerTaskRpcPatch { static bool Prefix(uint idx) => !(Faker.FakeTask && idx==uint.MaxValue); }
public sealed class FakerUI:MonoBehaviour
{
    static AbilityButton? button;static Sprite? icon;
    public FakerUI(IntPtr p):base(p){}
    void LateUpdate()
    {
        try
        {
            var task=Faker.Available();var hud=HudManager.Instance;
            if(RoleIntro.Visible || !hud || !Game.Started || !Faker.Mine || !Game.Alive || MeetingHud.Instance || Minigame.Instance || (MapBehaviour.Instance && MapBehaviour.Instance.IsOpen)) { if(button) button!.gameObject.SetActive(false);return; }
            if(!button)
            {
                button=UnityEngine.Object.Instantiate(hud.AbilityButton,hud.AbilityButton.transform.parent);button.name="FakerFakeTask";button.enabled=false;
                var click=button.GetComponent<PassiveButton>();click.OnClick=new UnityEngine.UI.Button.ButtonClickedEvent();click.OnClick.AddListener((UnityAction)Faker.Press);
                var aspect=button.GetComponent<AspectPosition>();if(aspect) UnityEngine.Object.Destroy(aspect);
                if(button.glyph) button.glyph.gameObject.SetActive(false);if(button.commsDown) button.commsDown.SetActive(false);
                using var stream=typeof(Plugin).Assembly.GetManifestResourceStream("Forger.Assets.fake-task.png")!;
                using var buffer=new MemoryStream();stream.CopyTo(buffer);var tex=new Texture2D(2,2,TextureFormat.RGBA32,false);ImageConversion.LoadImage(tex,buffer.ToArray(),false);tex.filterMode=FilterMode.Point;
                icon=Sprite.Create(tex,new Rect(0,0,tex.width,tex.height),new Vector2(.5f,.5f),tex.width/hud.KillButton.graphic.sprite.bounds.size.x);button.graphic.sprite=icon;
                button.OverrideText("FAKE TASK");button.buttonLabelText.fontSize=hud.KillButton.buttonLabelText.fontSize;
            }
            button!.gameObject.SetActive(true);button.transform.position=hud.ImpostorVentButton.transform.position+new Vector3(-1,0,0);button.transform.localScale=hud.KillButton.transform.localScale;
            button.SetUsesRemaining(Faker.Used?0:1);button.SetCooldownFill(0);
            if(task!=null) button.SetEnabled();else button.SetDisabled();
            button.cooldownTimerText.gameObject.SetActive(false);
            if(Faker.Active) { hud.KillButton.gameObject.SetActive(false);hud.SabotageButton.gameObject.SetActive(false);hud.UseButton.gameObject.SetActive(false);hud.ReportButton.gameObject.SetActive(false);hud.ImpostorVentButton.gameObject.SetActive(false); }
            if(Input.GetKeyDown(KeyCode.F)) Faker.Press();
        }
        catch(Exception e) { Plugin.Instance.Log.LogError("Faker UI: "+e); }
    }
    
}








