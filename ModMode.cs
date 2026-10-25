using BepInEx.Configuration;
using UnityEngine;
namespace Forger;
internal static class ModMode
{
    internal static ConfigEntry<bool> Enabled=null!;
    internal static bool Loaded;
    internal static bool RestartRequired=>Enabled!=null && Enabled.Value!=Loaded;
    internal static string Error="";
    internal static void Configure()
    {
        Enabled=Plugin.Instance.Config.Bind("General","Enable custom roles",true,"Restart required. OFF disables role and cosmetic patches and selects an official server; the menu and updater remain available.");Loaded=Enabled.Value;
        if(!Loaded)SelectOfficialServer();
    }
    internal static void SelectOfficialServer()
    {
        try {
            var path=ServerAddressStore.FilePath;
            var json=File.Exists(path)?File.ReadAllText(path):"{\"Regions\":[]}";
            var result=VanillaRegionPolicy.SelectOfficial(json);
            if(File.Exists(path))File.Copy(path,path+".before-vanilla-mode",true);
            File.WriteAllText(path+".vanilla-new",result);File.Move(path+".vanilla-new",path,true);
            if(ServerManager.Instance)ServerManager.Instance.LoadServers();
            Error="";
        }catch(Exception e){Error="Could not select an official server: "+e.Message;Plugin.Instance.Log.LogError(Error);}
    }
    internal static void SetEnabled(bool value)
    {
        Enabled.Value=value;Plugin.Instance.Config.Save();
        if(!value)SelectOfficialServer();
    }
}
public sealed class ModModeUI:MonoBehaviour
{
    public ModModeUI(IntPtr p):base(p){}
    static readonly string VersionLabel="Custom Roles v"+typeof(Plugin).CustomAttributes.First(a=>a.AttributeType==typeof(BepInEx.BepInPlugin)).ConstructorArguments[2].Value;
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
            var versionStyle=new GUIStyle(GUI.skin.label){fontSize=14,alignment=TextAnchor.LowerRight};
            var versionRect=new Rect(Screen.width/scale-310,Screen.height/scale-27,295,20);
            versionStyle.normal.textColor=new Color(0,0,0,.8f);
            GUI.Label(new Rect(versionRect.x+1,versionRect.y+1,versionRect.width,versionRect.height),VersionLabel,versionStyle);
            versionStyle.normal.textColor=new Color(1,1,1,.8f);
            GUI.Label(versionRect,VersionLabel,versionStyle);
            MenuInputGuard.Claim(new Rect(815,650,350,40));
            if(GUI.Button(new Rect(815,650,350,40),"Custom roles: "+(ModMode.Enabled.Value?"ON":"OFF")+"  |  click to toggle"))
            {ModMode.SetEnabled(!ModMode.Enabled.Value);}
            if(RestartPending())return;
            if(!ModMode.Loaded)GUI.Label(new Rect(815,608,350,35),"Roles OFF · official server selected");
        }
        finally{GUI.matrix=old;}
    }
    static bool RestartPending()
    {
        if(!ModMode.RestartRequired && ModMode.Error.Length==0)return false;
        MenuInputGuard.Claim(new Rect(0,0,1280,720),true);
        GUI.Box(new Rect(325,220,630,255),"RESTART REQUIRED");
        GUI.Label(new Rect(350,270,580,100),ModMode.Error.Length>0?ModMode.Error:"Your choice is saved. Close and reopen Among Us before joining a lobby.\n"+(ModMode.Enabled.Value?"Custom roles will be enabled. Select your private server in Online.":"Role and cosmetic patches will be disabled. An official server is selected."));
        if(GUI.Button(new Rect(350,395,580,50),"Close Among Us"))Application.Quit();
        return true;
    }
}
