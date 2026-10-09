using UnityEngine;
using UnityEngine.Events;
using HarmonyLib;
using System.Reflection;
namespace Forger;
public sealed class CannibalUI:MonoBehaviour
{
    static AbilityButton? button;
    static Sprite? icon;
    static GameObject? eatingCard;
    static float cardEnds;
    internal static PlayerControl? HighlightedTarget;
    static readonly List<GameObject> hiddenHud=new();
    internal static bool EatingVisible=>eatingCard && eatingCard!.activeInHierarchy;
    public CannibalUI(IntPtr pointer):base(pointer){}
    internal static void ClearTarget(){if(HighlightedTarget)HighlightedTarget!.ToggleHighlight(false,RoleTeamTypes.Impostor);HighlightedTarget=null;}
    internal static void RefreshTarget()
    {
        var target=Cannibal.CanEat?Cannibal.Nearest():null;
        if(HighlightedTarget&&(!target||HighlightedTarget!.Pointer!=target!.Pointer))ClearTarget();
        HighlightedTarget=target;
        if(target)target!.ToggleHighlight(true,RoleTeamTypes.Impostor);
    }
    internal static void ShowEating(string message="BEING EATEN")
    {
        if(eatingCard)UnityEngine.Object.Destroy(eatingCard);
        var hud=HudManager.Instance;if(!hud)return;
        hud.SetHudActive(false);
        foreach(var obj in new[]{hud.TaskStuff,hud.SettingsButton,hud.MapButton?hud.MapButton.gameObject:null,hud.MatchInfoButton?hud.MatchInfoButton.gameObject:null,hud.TaskPanel?hud.TaskPanel.gameObject:null})
            if(obj && obj!.activeSelf){hiddenHud.Add(obj);obj.SetActive(false);}
        eatingCard=new GameObject("CannibalBeingEatenOverlay");eatingCard.transform.SetParent(hud.transform,false);eatingCard.transform.localPosition=new Vector3(0,0,-70);
        var background=UnityEngine.Object.Instantiate(hud.KillOverlay.background,eatingCard.transform);background.transform.localPosition=Vector3.zero;background.gameObject.SetActive(true);background.color=Color.white;
        if(hud.KillOverlay.flameParent){var flames=UnityEngine.Object.Instantiate(hud.KillOverlay.flameParent,eatingCard.transform);flames.transform.localPosition=new Vector3(0,0,-1);flames.SetActive(true);}
        var text=UnityEngine.Object.Instantiate(hud.IntroPrefab.RoleText,eatingCard.transform);text.transform.localPosition=new Vector3(0,1,-2);text.gameObject.SetActive(true);text.color=Color.white;text.fontSize=5.5f;text.enableAutoSizing=false;text.alignment=TMPro.TextAlignmentOptions.Center;text.rectTransform.sizeDelta=new Vector2(9,2);text.text=message;
        cardEnds=Time.time+2;
    }
    
    void LateUpdate()
    {
        if(eatingCard && Time.time>=cardEnds){UnityEngine.Object.Destroy(eatingCard);eatingCard=null;foreach(var obj in hiddenHud)if(obj)obj.SetActive(true);hiddenHud.Clear();if(Game.Local && HudManager.Instance)HudManager.Instance.SetHudActive(Game.Local,Game.Local!.Data.Role,true);}
        try
        {
            var hud=HudManager.Instance;
            if(!hud || !Cannibal.Mine || !Game.Started || !Game.Alive || RoleIntro.Visible || MeetingHud.Instance || Minigame.Instance || ExileController.Instance || (MapBehaviour.Instance && MapBehaviour.Instance.IsOpen)) {ClearTarget();if(button)button!.gameObject.SetActive(false);return;}
            hud.KillButton.gameObject.SetActive(false);hud.SabotageButton.gameObject.SetActive(false);hud.ImpostorVentButton.gameObject.SetActive(false);
            if(!button)
            {
                button=UnityEngine.Object.Instantiate(hud.AbilityButton,hud.AbilityButton.transform.parent);button.name="CannibalEat";button.enabled=false;
                var passive=button.GetComponent<PassiveButton>();passive.OnClick=new UnityEngine.UI.Button.ButtonClickedEvent();passive.OnClick.AddListener((UnityAction)(()=>Cannibal.Eat()));
                var aspect=button.GetComponent<AspectPosition>();if(aspect)UnityEngine.Object.Destroy(aspect);
                if(button.glyph)button.glyph.gameObject.SetActive(false);if(button.commsDown)button.commsDown.SetActive(false);
                if(!icon)
                {
                    using var stream=typeof(Plugin).Assembly.GetManifestResourceStream("Forger.Assets.eat.png")!;using var buffer=new MemoryStream();stream.CopyTo(buffer);
                    var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);ImageConversion.LoadImage(texture,buffer.ToArray(),false);texture.filterMode=FilterMode.Point;
                    icon=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,.5f),texture.width/hud.KillButton.graphic.sprite.bounds.size.x);
                }
                button.graphic.sprite=icon;button.buttonLabelText.fontSize=hud.KillButton.buttonLabelText.fontSize;
            }
            button!.gameObject.SetActive(true);button.transform.position=hud.KillButton.transform.position;button.transform.localScale=hud.KillButton.transform.localScale;button.OverrideText("EAT");
            var ready=Cannibal.CanEat;if(ready)button.SetEnabled();else button.SetDisabled();button.SetInfiniteUses();
            RefreshTarget();
            var remaining=Math.Max(0,Cannibal.ReadyAt-Time.time);button.SetCooldownFill(Mathf.Clamp01(remaining/Cannibal.Cooldown.Value));button.cooldownTimerText.gameObject.SetActive(remaining>0);button.cooldownTimerText.text=Math.Ceiling(remaining).ToString();button.graphic.color=ready?Color.white:new Color(.5f,.5f,.5f,.65f);
            if(Input.GetKeyDown(KeyCode.Q))Cannibal.Eat();
        }
        catch(Exception e){if(button)button!.gameObject.SetActive(false);Plugin.Instance.Log.LogWarning("Cannibal UI: "+e.Message);}
    }
    
}
[HarmonyPatch]
internal static class CannibalEatingOverlay
{
    static IEnumerable<MethodBase> TargetMethods()=>typeof(KillOverlay).GetMethods().Where(m=>m.Name=="ShowKillAnimation" && m.GetParameters().Any(p=>p.Name=="victim"));
    static bool Prefix(object[] __args)
    {
        var players=__args.OfType<NetworkedPlayerInfo>().ToArray();
        if(Terrorist.Exploding&&players.Length>=2&&players[0].PlayerId==Terrorist.RoleId){if(Game.Local&&players[1].PlayerId==Game.Local!.PlayerId)CannibalUI.ShowEating("CAUGHT IN THE BLAST");return false;}
        if(players.Length<2 || players[0].PlayerId!=Cannibal.RoleId || !Cannibal.Eaten.Contains(players[1].PlayerId))return true;
        if(Game.Local && players[1].PlayerId==Game.Local!.PlayerId)CannibalUI.ShowEating();return false;
    }
}


