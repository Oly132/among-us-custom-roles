using BepInEx;
using System.Net.Http;
using System.Text.Json;
using System.Diagnostics;
using UnityEngine;
namespace Forger;

public sealed class AutoUpdateUI:MonoBehaviour
{
    const string Repository="Oly132/among-us-custom-roles";
    string? manifest;
    string version="",message="";
    string previousStatus="";
    bool checkedOnce,declined,starting;
    public AutoUpdateUI(IntPtr p):base(p){}
    void Update()
    {
        if(checkedOnce)return;checkedOnce=true;
        try{var status=Path.Combine(Paths.GameRootPath,"ForgerSetup","Updater","status.json");if(File.Exists(status)){using var json=JsonDocument.Parse(File.ReadAllText(status));previousStatus=json.RootElement.GetProperty("message").GetString()??"";}}catch{}
        _=Check();
    }
    async Task Check()
    {
        try
        {
            using var http=new HttpClient{Timeout=TimeSpan.FromSeconds(15)};
            http.DefaultRequestHeaders.UserAgent.ParseAdd("AmongUs-CustomRoles-Updater/1.0");
            var url=$"https://github.com/{Repository}/releases/latest/download/update-manifest.json";
            using var response=await http.GetAsync(url,HttpCompletionOption.ResponseHeadersRead);
            if(response.StatusCode==System.Net.HttpStatusCode.NotFound)return;
            response.EnsureSuccessStatusCode();
            if(response.Content.Headers.ContentLength>1024*1024)throw new Exception("Manifest too large");
            var bytes=await response.Content.ReadAsByteArrayAsync();if(bytes.Length>1024*1024)throw new Exception("Manifest too large");
            using var json=JsonDocument.Parse(bytes);var root=json.RootElement;
            if(root.GetProperty("schema").GetInt32()!=1 || root.GetProperty("repository").GetString()!=Repository)return;
            var installed=Version.Parse(typeof(Plugin).CustomAttributes.First(a=>a.AttributeType==typeof(BepInPlugin)).ConstructorArguments[2].Value!.ToString()!);
            if(!Version.TryParse(root.GetProperty("version").GetString(),out var available) || available<=installed)return;
            version=available.ToString();manifest=System.Text.Encoding.UTF8.GetString(bytes);
        }
        catch(Exception e){Plugin.Instance.Log.LogInfo("Update check unavailable: "+e.Message);}
    }
    void OnGUI()
    {
        if(Game.Local||Game.Started||!UnityEngine.Object.FindObjectOfType<MainMenuManager>())return;
        var scale=Mathf.Min(Screen.width/1280f,Screen.height/720f);var old=GUI.matrix;
        GUI.matrix=Matrix4x4.TRS(Vector3.zero,Quaternion.identity,new Vector3(scale,scale,1));
        try
        {
            if(previousStatus.Length>0)GUI.Box(new Rect(45,610,700,50),previousStatus);
            if((manifest==null && message.Length==0)||declined)return;
            GUI.Box(new Rect(365,230,550,210),"CUSTOM ROLES UPDATE");
            GUI.Label(new Rect(390,275,500,85),message.Length>0?message:$"Update {version} available. Download now?\nOnly changed files will download.\nAmong Us will close; close Silencer CrewLink too.");
            if(!starting && GUI.Button(new Rect(400,377,225,40),message.Length>0?"Retry":"Yes · update and close"))StartUpdate();
            if(!starting && GUI.Button(new Rect(655,377,225,40),"Not now"))declined=true;
        }
        finally{GUI.matrix=old;}
    }
    void StartUpdate()
    {
        try
        {
            var game=Paths.GameRootPath;
            var node=Path.Combine(game,"ForgerSetup","Voice","runtime","node.exe");
            if(!File.Exists(node))throw new Exception("Updater runtime missing. Install the next full release first.");
            // Run a cached bootstrap copy so this release can replace its own runtime.
            string hash;
            using(var stream=File.OpenRead(node))using(var sha=System.Security.Cryptography.SHA256.Create())hash=Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant();
            var bootstrap=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Forger","Updater",hash);
            Directory.CreateDirectory(bootstrap);var runner=Path.Combine(bootstrap,"node.exe");
            if(!File.Exists(runner))File.Copy(node,runner);
            var folder=Path.Combine(game,"ForgerSetup","Updater");Directory.CreateDirectory(folder);
            var script=Path.Combine(folder,"apply-update.mjs");
            using(var source=typeof(Plugin).Assembly.GetManifestResourceStream("Forger.Assets.apply-update.mjs")!)
            using(var output=File.Create(script))source.CopyTo(output);
            var plan=Path.Combine(folder,"manifest.json");File.WriteAllText(plan,manifest!);
            var start=new ProcessStartInfo(runner){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=folder};
            foreach(var arg in new[]{script,"--apply",game,Environment.ProcessId.ToString(),plan})start.ArgumentList.Add(arg);
            var process=Process.Start(start)??throw new Exception("Updater could not start");
            starting=true;Application.Quit();
        }
        catch(Exception e){message="Update could not start: "+e.Message;Plugin.Instance.Log.LogWarning(message);}
    }
}
