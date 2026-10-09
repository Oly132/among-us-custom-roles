using BepInEx.Configuration;
using UnityEngine;
namespace Forger;
internal static class ModMode
{
    internal static ConfigEntry<bool> Enabled=null!;
    internal static bool Loaded;
    internal static void Configure(){Enabled=Plugin.Instance.Config.Bind("General","Enable custom roles",true,"Enable custom roles. Restart Among Us after changing this setting. Disabled mode loads no custom gameplay patches.");Loaded=Enabled.Value;}
}
public sealed class ModModeUI:MonoBehaviour
{
    public ModModeUI(IntPtr p):base(p){}
    void OnGUI()
    {
        if(Game.Started || Game.Local || RoleIntro.Visible || Minigame.Instance ||
            (AmongUsClient.Instance && AmongUsClient.Instance.GameState!=InnerNet.InnerNetClient.GameStates.NotJoined))return;
        var mainMenu=UnityEngine.Object.FindObjectOfType<MainMenuManager>();
        if(!mainMenu || !mainMenu.isActiveAndEnabled)return;
        var scale=Mathf.Min(Screen.width/1280f,Screen.height/720f);var old=GUI.matrix;
        GUI.matrix=Matrix4x4.TRS(Vector3.zero,Quaternion.identity,new Vector3(scale,scale,1));
        try
        {
            if(GUI.Button(new Rect(815,650,350,40),"Custom roles: "+(ModMode.Enabled.Value?"ON":"OFF")+"  |  click to toggle"))
            {ModMode.Enabled.Value=!ModMode.Enabled.Value;Plugin.Instance.Config.Save();}
            if(ModMode.Enabled.Value!=ModMode.Loaded)GUI.Box(new Rect(815,601,350,43),"Restart Among Us to apply\n"+(ModMode.Enabled.Value?"Custom roles enabled":"Vanilla mode enabled"));
        }
        finally{GUI.matrix=old;}
    }
}
