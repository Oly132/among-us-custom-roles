using HarmonyLib;
using UnityEngine;
namespace Forger;
// The role's native metadata drives the unmodified team and special-role reveal.
internal static class RoleIntro
{
    internal static IntroCutscene? View;
    static bool previewPending;
    static bool introHidHud;
    internal static bool Visible=>previewPending || (View && View!.gameObject.activeInHierarchy) || CannibalUI.EatingVisible;
    static Camera? backdropCamera;
    static int oldMask;
    static CameraClearFlags oldFlags;
    static Color oldColor;
    static readonly List<GameObject> hiddenHud=new();
    internal static void BeginBackdrop()
    {
        var hud=HudManager.Instance;
        var cam=Camera.main;
        if(!backdropCamera && cam && (!hud || cam!=hud.UICamera))
        {
            backdropCamera=cam;oldMask=cam.cullingMask;oldFlags=cam.clearFlags;oldColor=cam.backgroundColor;
            cam.cullingMask=0;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=Color.black;
        }
        if(!hud)return;
        hud.SetHudActive(false);
        foreach(var obj in new[]{hud.TaskStuff,hud.SettingsButton,hud.MapButton?hud.MapButton.gameObject:null,hud.MatchInfoButton?hud.MatchInfoButton.gameObject:null,hud.TaskPanel?hud.TaskPanel.gameObject:null})
            if(obj && obj!.activeSelf){hiddenHud.Add(obj);obj.SetActive(false);}
    }
    internal static void EndBackdrop()
    {
        if(backdropCamera){backdropCamera!.cullingMask=oldMask;backdropCamera.clearFlags=oldFlags;backdropCamera.backgroundColor=oldColor;}
        backdropCamera=null;previewPending=false;
        foreach(var obj in hiddenHud)if(obj)obj.SetActive(true);
        hiddenHud.Clear();
    }
    internal static void Refresh()
    {
        if(previewPending || (View && View!.gameObject.activeInHierarchy))
        {
            if(HudManager.Instance){HudManager.Instance.SetHudActive(false);introHidHud=true;}
            return;
        }
        RestoreHud();
    }
    internal static void RestoreHud()
    {
        if(!introHidHud)return;
        if(!Game.Started){introHidHud=false;return;}
        var local=Game.Local;var hud=HudManager.Instance;
        if(!local || local!.Data?.Role==null || !hud || CannibalUI.EatingVisible || MeetingHud.Instance || Minigame.Instance || ExileController.Instance || (MapBehaviour.Instance && MapBehaviour.Instance.IsOpen))return;
        introHidHud=false;
        hud.SetHudActive(local,local.Data.Role,true);
    }
    
}
[HarmonyPatch(typeof(IntroCutscene),nameof(IntroCutscene.CoBegin))]
internal static class NativeIntroTracking {static void Prefix(IntroCutscene __instance){RoleIntro.View=__instance;}}
[HarmonyPatch(typeof(IntroCutscene),nameof(IntroCutscene.OnDestroy))]
internal static class NativeIntroEnd {static void Postfix(IntroCutscene __instance){if(RoleIntro.View && RoleIntro.View!.Pointer!=__instance.Pointer)return;RoleIntro.View=null;RoleIntro.EndBackdrop();RoleIntro.RestoreHud();}}
[HarmonyPatch(typeof(EmergencyMinigame),nameof(EmergencyMinigame.CallMeeting))]
internal static class SilenceEmergencyButton
{
    internal static bool Prefix(){if(!Game.Local || !Silencer.Muted(Game.Local!.PlayerId))return true;Game.Notify("You cannot call an emergency meeting while silenced.");return false;}
}
[HarmonyPatch(typeof(EmergencyMinigame),nameof(EmergencyMinigame.Update))]
internal static class SilenceEmergencyStatus
{
    static IntPtr hiddenCounter;
    static bool counterWasActive;
    static void Prefix(EmergencyMinigame __instance)
    {
        if(hiddenCounter==__instance.Pointer && (!Game.Local || !Silencer.Muted(Game.Local!.PlayerId)))
        {__instance.NumberText.gameObject.SetActive(counterWasActive);hiddenCounter=IntPtr.Zero;}
    }
    static void Postfix(EmergencyMinigame __instance)
    {
        if(!Game.Local || !Silencer.Muted(Game.Local!.PlayerId))return;
        __instance.ButtonActive=false;
        __instance.ClosedLid.gameObject.SetActive(true);__instance.ClosedLid.enabled=true;
        __instance.OpenLid.gameObject.SetActive(false);
        if(hiddenCounter!=__instance.Pointer){hiddenCounter=__instance.Pointer;counterWasActive=__instance.NumberText.gameObject.activeSelf;}
        __instance.NumberText.gameObject.SetActive(false);
        __instance.StatusText.text="SILENCED\n"+(Silencer.Remaining(Game.Local!.PlayerId) is int seconds?$"Emergency meetings unlock in {seconds}s.":"You cannot call an emergency meeting.");
    }
}
[HarmonyPatch(typeof(PlayerControl),nameof(PlayerControl.ReportDeadBody))]
internal static class SilenceEmergencyRequest
{
    internal static bool Prefix(PlayerControl __instance,NetworkedPlayerInfo target)=>target!=null || !Silencer.Muted(__instance.PlayerId);
}





