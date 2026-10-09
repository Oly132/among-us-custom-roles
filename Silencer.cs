using BepInEx.Configuration;
using HarmonyLib;
using Hazel;
using UnityEngine;
using UnityEngine.Events;
namespace Forger;

internal static class Silencer
{
    const byte Request=246, State=247;
    internal static byte RoleId=255;
    internal static int Used, Epoch;
    internal static float ReadyAt;
    internal static ConfigEntry<int> Chance=null!,Uses=null!;
    internal static ConfigEntry<float> Cooldown=null!;
    internal static readonly SilenceState Effect=new();
    static bool meetingSeen;
    static float nextBridge;
    static float releasedNoticeUntil;
    internal static int? Remaining(byte id)=>Effect.Targets.TryGetValue(id,out var silence)&&float.IsFinite(silence.release)?Math.Clamp((int)Math.Ceiling(silence.release-Time.time),0,10):null;
    internal static string Status(byte id)=>Remaining(id) is int seconds?$"Silence ends in {seconds}s":"Until 10 seconds after the next meeting ends";
    static void RemoveSilence(byte id)
    {
        if(Game.Local && Game.Local!.PlayerId==id && Effect.Contains(id))releasedNoticeUntil=Time.time+1;
        Effect.Targets.Remove(id);
    }
    internal static bool ShowReleased=>Time.time<releasedNoticeUntil;
    static readonly string BridgePath=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Forger","silencer.json");
    internal static bool Mine => Game.Local && Game.Local!.PlayerId==RoleId;
    internal static bool Muted(byte id) => Game.Started && Puppeteer.Living(Puppeteer.Find(id)) && Effect.Targets.TryGetValue(id,out var silence) && Time.time<silence.release;
    internal static void Configure()
    {
        Chance=Plugin.Instance.Config.Bind("Silencer","Assignment chance",100,new ConfigDescription("Chance to assign Silencer; replaces another custom role if there are too few impostors.",new AcceptableValueRange<int>(0,100)));
        Uses=Plugin.Instance.Config.Bind("Silencer","Uses per game",3,new ConfigDescription("Number of crewmates you can silence each game.",new AcceptableValueRange<int>(1,15)));
        Cooldown=Plugin.Instance.Config.Bind("Silencer","Silence cooldown",30f,new ConfigDescription("Seconds between silences.",new AcceptableValueRange<float>(0,120)));
    }
    internal static void Reset(byte id=255) { RoleId=id;Used=0;ReadyAt=0;Effect.Clear();meetingSeen=false;releasedNoticeUntil=0;WriteBridge(false);RegisteredRoles.Apply(id,3); }
    static void Send(byte op,Action<MessageWriter>? body=null) => Game.Send(State,w=>{w.Write((byte)1);w.Write(Game.Epoch);w.Write(op);body?.Invoke(w);});
    internal static PlayerControl? Target()
    {
        if(!Mine || !Game.Alive || !Game.Local!.CanMove || Game.Local.inVent || MeetingHud.Instance || Minigame.Instance || Puppeteer.Active || Faker.Active) return null;
        PlayerControl? closest=null;float distance=1.8f;
        foreach(var p in PlayerControl.AllPlayerControls)
        {
            if(!Puppeteer.Living(p) || p.PlayerId==RoleId || p.Data.Role.IsImpostor || p.inVent || Effect.PreviouslySilenced.Contains(p.PlayerId)) continue;
            var d=Vector2.Distance(Game.Local.GetTruePosition(),p.GetTruePosition());
            if(d>distance || Physics2D.Linecast(Game.Local.GetTruePosition(),p.GetTruePosition(),Constants.ShipAndObjectsMask).collider) continue;
            closest=p;distance=d;
        }
        return closest;
    }
    internal static void Press()
    {
        var t=Target();if(!t || Time.time<ReadyAt || Used>=Uses.Value) return;
        if(Game.Host) Apply(Game.Local!,t!.PlayerId);
        else Game.Send(Request,w=>{w.Write((byte)1);w.Write(Game.Epoch);w.Write(t!.PlayerId);},AmongUsClient.Instance.HostId);
    }
    static void Apply(PlayerControl sender,byte id)
    {
        var p=Puppeteer.Find(id);
        if(!Game.Host || !Game.Started || sender.PlayerId!=RoleId || !Puppeteer.Living(sender) || !sender.Data.Role.IsImpostor || sender.inVent || MeetingHud.Instance || !Puppeteer.Living(p) || p!.Data.Role.IsImpostor || p.inVent || Effect.PreviouslySilenced.Contains(id) || Used>=Uses.Value || Time.time<ReadyAt) return;
        if(Vector2.Distance(sender.GetTruePosition(),p.GetTruePosition())>1.8f || Physics2D.Linecast(sender.GetTruePosition(),p.GetTruePosition(),Constants.ShipAndObjectsMask).collider) return;
        if(!Effect.Add(id)) return;Used++;ReadyAt=Time.time+Cooldown.Value;
        Send(1,w=>{w.Write(id);w.Write(Used);w.Write(Cooldown.Value);});
        WriteBridge(Game.Local && Muted(Game.Local!.PlayerId));
        Plugin.Instance.Log.LogInfo($"SILENCER muted {id} through next meeting + 10 seconds");
    }
    internal static void MeetingStart()
    {
        if(!Game.Started || meetingSeen) return;
        meetingSeen=true;Effect.MeetingStarted();
    }
    internal static void Tick()
    {
        if(!Game.Local || !Game.Started) { if(!Game.WaitingForRoundStart && (RoleId!=255 || Effect.Targets.Count>0)) Reset();Heartbeat(false);return; }

        if(MeetingHud.Instance) MeetingStart();
        // Exile/black-screen transition is part of the meeting; the ten seconds start when gameplay returns.
        if(meetingSeen && !MeetingHud.Instance && !ExileController.Instance && Game.Local!.CanMove)
        {
            meetingSeen=false;Effect.MeetingEnded(Time.time);
            Plugin.Instance.Log.LogInfo("SILENCER meeting ended; ten-second grace period started");
        }
        if(Game.Host)
        {
            foreach(var id in Effect.Targets.Keys.ToArray())
            {
                if(!Puppeteer.Living(Puppeteer.Find(id)) || Effect.Expired(Time.time).Contains(id))
                { RemoveSilence(id);Send(2,w=>w.Write(id));Plugin.Instance.Log.LogInfo("SILENCER released "+id); }
            }
        }
        Heartbeat(Muted(Game.Local!.PlayerId));
    }
    static void Heartbeat(bool mute) { if(Time.realtimeSinceStartup<nextBridge) return;nextBridge=Time.realtimeSinceStartup+.3f;WriteBridge(mute); }
    static void WriteBridge(bool mute)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(BridgePath)!);
            var json=$"{{\"version\":1,\"pid\":{Environment.ProcessId},\"updated\":{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()},\"muted\":{(mute?"true":"false")}}}";
            File.WriteAllText(BridgePath+".tmp",json);File.Move(BridgePath+".tmp",BridgePath,true);
        }
        catch(Exception e) { Plugin.Instance.Log.LogWarning("Silencer voice bridge: "+e.Message); }
    }
    internal static bool Rpc(PlayerControl sender,byte call,MessageReader r)
    {
        if(call!=Request && call!=State) return true;
        try
        {
            if(r.ReadByte()!=1) return false;var epoch=r.ReadInt32();
            if(call==Request) { if(Game.Host && epoch==Game.Epoch) Apply(sender,r.ReadByte());return false; }
            if(Game.Host || sender.OwnerId!=AmongUsClient.Instance.HostId) return false;
            var op=r.ReadByte();
            if(op==0) { Epoch=epoch;Reset(r.ReadByte());Uses.Value=r.ReadInt32();Cooldown.Value=r.ReadSingle(); }
            else if(epoch==Epoch)
            {
                if(op==1) { Effect.Add(r.ReadByte());Used=r.ReadInt32();ReadyAt=Time.time+r.ReadSingle(); }
                else if(op==2) RemoveSilence(r.ReadByte());
            }
        }
        catch(Exception e) { Plugin.Instance.Log.LogWarning("Silencer packet: "+e.Message); }
        return false;
    }
}

[HarmonyPatch(typeof(ChatController),nameof(ChatController.SendChat))]
internal static class SilenceSendChat
{
    static bool Prefix() { if(!Game.Local || !Silencer.Muted(Game.Local!.PlayerId)) return true;Game.Notify("Silenced until 10 seconds after the next meeting ends.");return false; }
}
[HarmonyPatch(typeof(ChatController),nameof(ChatController.SendFreeChat))]
internal static class SilenceFreeChat { static bool Prefix() => !Game.Local || !Silencer.Muted(Game.Local!.PlayerId); }
[HarmonyPatch(typeof(ChatController),nameof(ChatController.SendQuickChat))]
internal static class SilenceQuickChat { static bool Prefix() => !Game.Local || !Silencer.Muted(Game.Local!.PlayerId); }
[HarmonyPatch(typeof(PlayerControl),nameof(PlayerControl.RpcSendChat))]
internal static class SilenceChatRpc { static bool Prefix(PlayerControl __instance,ref bool __result) { if(!Silencer.Muted(__instance.PlayerId)) return true;__result=false;return false; } }
[HarmonyPatch(typeof(ChatController),nameof(ChatController.AddChat))]
internal static class SilenceIncomingChat { static bool Prefix(PlayerControl sourcePlayer) => !sourcePlayer || !Silencer.Muted(sourcePlayer.PlayerId); }

public sealed class SilencerUI:MonoBehaviour
{
    static AbilityButton? button;
    public SilencerUI(IntPtr p):base(p){}
    void LateUpdate()
    {
        var h=HudManager.Instance;
        if(RoleIntro.Visible || !h || !Game.Started || !Silencer.Mine || !Game.Alive || MeetingHud.Instance || Minigame.Instance || ExileController.Instance || (MapBehaviour.Instance && MapBehaviour.Instance.IsOpen)) { if(button) button!.gameObject.SetActive(false);return; }
        if(!button)
        {
            button=UnityEngine.Object.Instantiate(h.AbilityButton,h.AbilityButton.transform.parent);button.name="SilencerButton";button.enabled=false;
            var click=button.GetComponent<PassiveButton>();click.OnClick=new UnityEngine.UI.Button.ButtonClickedEvent();click.OnClick.AddListener((UnityAction)Silencer.Press);
            var aspect=button.GetComponent<AspectPosition>();if(aspect) UnityEngine.Object.Destroy(aspect);
            if(button.glyph) button.glyph.gameObject.SetActive(false);if(button.commsDown) button.commsDown.SetActive(false);
            using var stream=typeof(Plugin).Assembly.GetManifestResourceStream("Forger.Assets.silence.png")!;using var buffer=new MemoryStream();stream.CopyTo(buffer);var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);ImageConversion.LoadImage(texture,buffer.ToArray(),false);texture.filterMode=FilterMode.Point;button.graphic.sprite=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,.5f),texture.width/h.KillButton.graphic.sprite.bounds.size.x);button.OverrideText("SILENCE");
        }
        button!.gameObject.SetActive(true);button.transform.position=h.ImpostorVentButton.transform.position+new Vector3(-1,0,0);button.transform.localScale=h.KillButton.transform.localScale;
        var ready=Silencer.Target() && Time.time>=Silencer.ReadyAt && Silencer.Used<Silencer.Uses.Value;
        if(ready) button.SetEnabled();else button.SetDisabled();
        button.SetUsesRemaining(Math.Max(0,Silencer.Uses.Value-Silencer.Used));var remaining=Math.Max(0,Silencer.ReadyAt-Time.time);
        button.SetCooldownFill(Mathf.Clamp01(remaining/Math.Max(1,Silencer.Cooldown.Value)));button.cooldownTimerText.gameObject.SetActive(remaining>0);button.cooldownTimerText.text=Math.Ceiling(remaining).ToString();
        if(Input.GetKeyDown(KeyCode.F)) Silencer.Press();
    }
    void OnGUI()
    {
        if(RoleIntro.Visible) return;
        var old=GUI.matrix;var scale=Mathf.Min(Screen.width/1280f,Screen.height/720f);GUI.matrix=Matrix4x4.TRS(Vector3.zero,Quaternion.identity,new Vector3(scale,scale,1));
        try
        {
            if(Game.Local && Silencer.Muted(Game.Local!.PlayerId)) GUI.Box(new Rect(450,70,380,55),"SILENCED · chat and voice blocked\n"+Silencer.Status(Game.Local.PlayerId));
            else if(Game.Alive && Silencer.ShowReleased)GUI.Box(new Rect(450,70,380,55),"SILENCE ENDED · 0s\nChat, voice and emergency meetings restored");
            
        }
        finally { GUI.matrix=old; }
    }
}







