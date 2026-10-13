using System.Text.Json.Nodes;
using UnityEngine;
namespace Forger;
internal static class ServerAddressStore
{
    internal static string Normalize(string value)
    {
        if(!Uri.TryCreate(value.Trim(),UriKind.Absolute,out var uri)||uri.Scheme!="https"||string.IsNullOrEmpty(uri.Host)||uri.AbsolutePath!="/"||uri.Query.Length>0||uri.Fragment.Length>0||uri.UserInfo.Length>0)
            throw new ArgumentException("Paste the full HTTPS address from your host, without a path.");
        return uri.GetLeftPart(UriPartial.Authority);
    }
    internal static string FilePath=>Path.Combine(Application.persistentDataPath,"regionInfo.json");
    internal static string Read()
    {
        if(!File.Exists(FilePath))return "";
        var root=JsonNode.Parse(File.ReadAllText(FilePath).TrimStart('\uFEFF'));
        foreach(var region in root?["Regions"]?.AsArray()??new JsonArray())
            if(region?["Name"]?.GetValue<string>()=="modded server")return region?["Servers"]?[0]?["Ip"]?.GetValue<string>()??"";
        return "";
    }
    internal static string Merge(string json,string address)
    {
        var value=Normalize(address);var uri=new Uri(value);
        var root=JsonNode.Parse(json.TrimStart('\uFEFF'))?.AsObject()??new JsonObject();
        var regions=root["Regions"]?.AsArray();if(regions==null){regions=new JsonArray();root["Regions"]=regions;}
        int index=-1;
        for(int i=regions.Count-1;i>=0;i--)if(regions[i]?["Name"]?.GetValue<string>()=="modded server"){if(index<0)index=i;else{regions.RemoveAt(i);index--;}}
        var region=new JsonObject{["$type"]="StaticHttpRegionInfo, Assembly-CSharp",["Name"]="modded server",["PingServer"]=uri.Host,["TranslateName"]=1003,["TargetServer"]=null,
            ["Servers"]=new JsonArray(new JsonObject{["Name"]="Http-1",["Ip"]=value,["Port"]=uri.Port,["UseDtls"]=false,["Players"]=0,["ConnectionFailures"]=0})};
        if(index<0){index=regions.Count;regions.Add(region);}else regions[index]=region;
        root["CurrentRegionIdx"]=index;
        return root.ToJsonString(new System.Text.Json.JsonSerializerOptions{WriteIndented=true});
    }
    internal static void Save(string address)
    {
        var path=FilePath;var json=File.Exists(path)?File.ReadAllText(path):"{}";
        var result=Merge(json,address);Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if(File.Exists(path))File.Copy(path,path+".before-address-change",true);
        var pending=path+".new";File.WriteAllText(pending,result);File.Move(pending,path,true);
        if(ServerManager.Instance)ServerManager.Instance.LoadServers();
    }
}
public sealed class ServerAddressUI:MonoBehaviour
{
    bool open,editing;string address="",message="";
    public ServerAddressUI(IntPtr p):base(p){}
    void OnGUI()
    {
        if(Game.Local||Game.Started||RoleIntro.Visible||Minigame.Instance||
            (AmongUsClient.Instance&&AmongUsClient.Instance.GameState!=InnerNet.InnerNetClient.GameStates.NotJoined)){open=false;return;}
        var menu=UnityEngine.Object.FindObjectOfType<MainMenuManager>();if(!menu||!menu.isActiveAndEnabled){open=false;return;}
        var old=GUI.matrix;var scale=Mathf.Min(Screen.width/1280f,Screen.height/720f);
        GUI.matrix=Matrix4x4.TRS(Vector3.zero,Quaternion.identity,new Vector3(scale,scale,1));
        try
        {
            MenuInputGuard.Claim(new Rect(450,650,330,40),open);
            if(!open){if(GUI.Button(new Rect(450,650,330,40),"Server address")){try{address=ServerAddressStore.Read();message="";}catch{address="";message="Could not read the saved address.";}open=true;}return;}
            GUI.Box(new Rect(245,175,790,365),"MODDED SERVER ADDRESS");
            GUI.Label(new Rect(270,215,730,50),"Paste the HTTPS address your host sent you.\nYour other server regions stay saved.");
            // GUI.TextField is stripped from the shipping game; draw the field explicitly.
            if(GUI.Button(new Rect(270,280,590,45),address.Length==0?"Click here to type, or use Paste":address)){editing=true;}
            if(editing && Event.current.type==EventType.KeyDown)
            {
                var e=Event.current;
                if(e.keyCode==KeyCode.Backspace){if(address.Length>0)address=address.Substring(0,address.Length-1);e.Use();}
                else if(e.keyCode==KeyCode.Return||e.keyCode==KeyCode.KeypadEnter){editing=false;e.Use();}
                else if(e.control&&e.keyCode==KeyCode.V){try{address=GUIUtility.systemCopyBuffer;message="";}catch(Exception error){message="Paste failed: "+error.Message;}e.Use();}
                else if(e.control&&e.keyCode==KeyCode.A){address="";e.Use();}
                else if(!char.IsControl(e.character)&&e.character!=0&&address.Length<300){address+=e.character;e.Use();}
            }
            if(GUI.Button(new Rect(875,280,135,45),"Paste")){try{address=GUIUtility.systemCopyBuffer;message="";editing=false;}catch(Exception error){message="Paste failed: "+error.Message;}}
            GUI.Label(new Rect(270,340,740,75),message);
            if(GUI.Button(new Rect(270,445,350,50),"Save and select server"))
            {try{ServerAddressStore.Save(address);address=ServerAddressStore.Normalize(address);message="Saved! Choose modded server in Online, then enter the room code.";}catch(Exception e){message="Could not save: "+e.Message;}}
            if(GUI.Button(new Rect(655,445,350,50),"Close"))open=false;
        }
        finally{GUI.matrix=old;}
    }
}
