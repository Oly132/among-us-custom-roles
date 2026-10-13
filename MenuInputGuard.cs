using HarmonyLib;
using UnityEngine;
namespace Forger;
internal static class MenuInputGuard
{
    static int claimedUntil=-1;
    internal static void Claim(Rect rect,bool modal=false)
    {
        var scale=Mathf.Min(Screen.width/1280f,Screen.height/720f);
        var mouse=Input.mousePosition;
        if(modal || rect.Contains(new Vector2(mouse.x/scale,(Screen.height-mouse.y)/scale)))
            claimedUntil=Time.frameCount+2;
    }
    internal static bool Blocked
    {
        get
        {
            if(Time.frameCount<=claimedUntil)return true;
            if(Game.Local || Game.Started)return false;
            var menu=UnityEngine.Object.FindObjectOfType<MainMenuManager>();
            if(!menu || !menu.isActiveAndEnabled)return false;
            var scale=Mathf.Min(Screen.width/1280f,Screen.height/720f);
            var mouse=Input.mousePosition;
            var point=new Vector2(mouse.x/scale,(Screen.height-mouse.y)/scale);
            return new Rect(450,650,330,40).Contains(point) || new Rect(815,650,350,40).Contains(point);
        }
    }
}
[HarmonyPatch(typeof(PassiveButtonManager),nameof(PassiveButtonManager.Update))]
internal static class BlockNativeMouse {static bool Prefix()=>!MenuInputGuard.Blocked;}
[HarmonyPatch(typeof(ControllerManager),nameof(ControllerManager.Update))]
internal static class BlockNativeController {static bool Prefix()=>!MenuInputGuard.Blocked;}
