using UnityEngine;
using HarmonyLib;
namespace Forger;

// Unspawned, disabled meeting prefab: artwork and cards only, no voting or network registration.
internal static class PossessionTablet
{
    static GameObject? root;
    static MeetingHud? view;
    static readonly List<(PlayerVoteArea card,byte id)> cards=new();
    static bool wasMoveable;
    static int openedFrame;
    static SpriteRenderer? closeIcon;
    internal static bool IsOpen => root is not null && root;
    internal static void Open()
    {
        wasMoveable=Game.Local ? Game.Local!.moveable : true;
        try { Build(); } catch(Exception ex) { Plugin.Instance.Log.LogError("Possession tablet: "+ex);Close(); }
    }
    static void Build()
    {
        if (!Puppeteer.CanPossess || IsOpen || Minigame.Instance || MeetingHud.Instance) return;
        var hud=HudManager.Instance;
        root=new GameObject("PuppeteerPlayerTablet");root.SetActive(false);
        root.transform.SetParent(hud.transform,false);root.transform.localPosition=new Vector3(0,0,-50);
        view=UnityEngine.Object.Instantiate(hud.MeetingPrefab,root.transform);view.name="PuppeteerTabletView";view.enabled=false;
        view.transform.localPosition=Vector3.zero;
        view.TitleText.text="CHOOSE A CREWMATE";
        foreach(var translator in view.TitleText.GetComponents<TextTranslatorTMP>()) translator.enabled=false;
        view.SkipVoteButton.gameObject.SetActive(false);view.ProceedButton.gameObject.SetActive(false);
        view.TimerText.gameObject.SetActive(false);view.MeetingAbilityButton.gameObject.SetActive(false);
        view.MeetingIntro.gameObject.SetActive(false);view.SkippedVoting.SetActive(false);view.HostIcon.gameObject.SetActive(false);
        if(view.judgeOverruleDisplay) view.judgeOverruleDisplay.SetActive(false);
        if(view.judgeUsesRemaining) view.judgeUsesRemaining.gameObject.SetActive(false);
        view.meetingContents.gameObject.SetActive(true);
        var scientist=RoleManager.Instance.GetRole(AmongUs.GameOptions.RoleTypes.Scientist).TryCast<ScientistRole>();
        var nativeButtons=scientist?.VitalsPrefab.GetComponentsInChildren<PassiveButton>(true);
        var nativeClose=nativeButtons?.FirstOrDefault(b=>b.name=="CloseButton");
        if(nativeClose)
        {
            var close=UnityEngine.Object.Instantiate(nativeClose!.gameObject,view.transform);
            close.name="PuppeteerTabletClose";close.SetActive(true);
            var bounds=view.Glass.bounds;
            close.transform.position=new Vector3(bounds.min.x+.35f,bounds.max.y-.35f,view.Glass.transform.position.z-1);
            foreach(var click in close.GetComponentsInChildren<PassiveButton>(true))click.enabled=false;
            closeIcon=close.GetComponentInChildren<SpriteRenderer>(true);
        }
        var actual=MeetingHud.Instance;root.SetActive(true);MeetingHud.Instance=actual;
        foreach(var p in PlayerControl.AllPlayerControls)
        {
            if(!p || p.Data==null) continue;
            var i=cards.Count;var card=UnityEngine.Object.Instantiate(view.PlayerButtonPrefab,view.ButtonParent);card.enabled=false;
            card.name="PuppeteerCard_"+p.PlayerId;
            card.transform.localPosition=view.VoteOrigin+new Vector3((i%3)*Mathf.Abs(view.VoteButtonOffsets.x),-(i/3)*Mathf.Abs(view.VoteButtonOffsets.y),0);
            card.SetPlayerId(p.PlayerId);card.SetMaskLayer(i+1);card.SetCosmetics(p.Data);card.NameText.text=p.Data.PlayerName;
            card.Buttons.SetActive(false);card.Flag.gameObject.SetActive(false);card.Megaphone.gameObject.SetActive(false);
            card.Overlay.gameObject.SetActive(p.Data.IsDead);card.XMark.gameObject.SetActive(p.Data.IsDead);card.ThumbsDown.gameObject.SetActive(false);
            card.HighlightedFX.gameObject.SetActive(false);card.LevelNumberText.gameObject.SetActive(false);card.ColorBlindName.gameObject.SetActive(false);
            foreach(var t in card.GetComponentsInChildren<Transform>(true)) if(t.name.Contains("Level",StringComparison.OrdinalIgnoreCase)) t.gameObject.SetActive(false);
            foreach(var click in card.GetComponentsInChildren<PassiveButton>(true)) click.enabled=false;
            card.gameObject.SetActive(true);cards.Add((card,p.PlayerId));
        }

        openedFrame=Time.frameCount;wasMoveable=Game.Local!.moveable;Game.Local.moveable=false;HideHud();
        Plugin.Instance.Log.LogInfo($"Puppeteer voting tablet: {cards.Count} cards, origin={view.VoteOrigin}, offsets={view.VoteButtonOffsets}");
    }
    static void HideHud()
    {
        var h=HudManager.Instance;h.KillButton.gameObject.SetActive(false);h.SabotageButton.gameObject.SetActive(false);
        h.UseButton.gameObject.SetActive(false);h.ReportButton.gameObject.SetActive(false);h.ImpostorVentButton.gameObject.SetActive(false);h.AbilityButton.gameObject.SetActive(false);
    }
    internal static void Close()
    {
        if(root) UnityEngine.Object.Destroy(root);root=null;view=null;closeIcon=null;cards.Clear();
        if(Game.Local && !MeetingHud.Instance) { Game.Local!.moveable=wasMoveable;HudManager.Instance.SetHudActive(Game.Local,Game.Local.Data.Role,true); }
    }
    internal static void Update()
    {
        if(!IsOpen) return;
        if(!Game.Started || !Puppeteer.Mine || !Game.Alive || Puppeteer.Active || MeetingHud.Instance) { Close();return; }
        HideHud();if(Input.GetKeyDown(KeyCode.Escape) || (Time.frameCount>openedFrame && Input.GetKeyDown(KeyCode.F))) Close();
    }
    internal static void DrawClicks(float scale)
    {
        if(!IsOpen || !view) return;
        var camera=Camera.main;if(!camera) return;
        if(closeIcon)
        {
            var a=camera.WorldToScreenPoint(closeIcon!.bounds.min);var b=camera.WorldToScreenPoint(closeIcon.bounds.max);
            if(GUI.Button(new Rect(a.x/scale,(Screen.height-b.y)/scale,(b.x-a.x)/scale,(b.y-a.y)/scale),"",GUIStyle.none)){Close();return;}
        }
        foreach(var entry in cards)
        {
            var card=entry.card;var bounds=card.Background.bounds;
            var a=camera.WorldToScreenPoint(bounds.min);var b=camera.WorldToScreenPoint(bounds.max);
            var rect=new Rect(a.x/scale,(Screen.height-b.y)/scale,(b.x-a.x)/scale,(b.y-a.y)/scale);
            var p=Puppeteer.Find(entry.id);var eligible=Puppeteer.Living(p) && !p!.Data.Role.IsImpostor && !p.inVent;
            card.Background.color=Color.white;
            card.HighlightedFX.enabled=true;card.HighlightedFX.color=new Color(1,1,0,.75f);
            card.HighlightedFX.sortingOrder=card.Background.sortingOrder+1;
            card.HighlightedFX.gameObject.SetActive(eligible && rect.Contains(Event.current.mousePosition));
            card.Overlay.gameObject.SetActive(p && p!.Data.IsDead);card.XMark.gameObject.SetActive(p && p!.Data.IsDead);
            GUI.enabled=eligible;
            if(GUI.Button(rect,"",GUIStyle.none)) { var id=entry.id;Close();Puppeteer.Select(id);break; }
        }
        GUI.enabled=true;
    }
}

[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Awake))]
internal static class PuppetTabletAwakePatch { static bool Prefix(MeetingHud __instance) => __instance.name!="PuppeteerTabletView"; }
[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.OnDestroy))]
internal static class PuppetTabletDestroyPatch { static bool Prefix(MeetingHud __instance) => __instance.name!="PuppeteerTabletView"; }
[HarmonyPatch(typeof(PlayerVoteArea), nameof(PlayerVoteArea.OnDestroy))]
internal static class PuppetCardDestroyPatch { static bool Prefix(PlayerVoteArea __instance) => !__instance.name.StartsWith("PuppeteerCard_"); }




