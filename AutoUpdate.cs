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
    bool checkedOnce,noticeRead,declined,starting;
    string progressPath="";
    float nextProgressRead;
    bool downloaded;
    long downloadedBytes,totalBytes;
    public AutoUpdateUI(IntPtr p):base(p){}
    void Update()
    {
        if(starting && Time.unscaledTime>=nextProgressRead)
        {
            nextProgressRead=Time.unscaledTime+.25f;
            try
            {
                using var reader=new StreamReader(new FileStream(progressPath,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete));
                using var state=JsonDocument.Parse(reader.ReadToEnd());var root=state.RootElement;
                if(root.TryGetProperty("version",out var v)&&v.GetString()==version)
                {
                    message=root.GetProperty("message").GetString()??"Downloading update…";
                    if(root.TryGetProperty("downloadedBytes",out var bytes))downloadedBytes=bytes.GetInt64();
                    if(root.TryGetProperty("totalBytes",out var total))totalBytes=total.GetInt64();
                    var phase=root.GetProperty("phase").GetString();downloaded=phase=="downloaded";
                    if(phase=="failed")starting=false;
                }
            }
            catch(IOException){}catch(JsonException){}
        }
        if(!noticeRead)
        {
            var menu=UnityEngine.Object.FindObjectOfType<MainMenuManager>();
            if(menu && menu.isActiveAndEnabled)
            {
                noticeRead=true;
                try{previousStatus=UpdateNotice.Consume(Path.Combine(Paths.GameRootPath,"ForgerSetup","Updater","status.json"),typeof(Plugin).CustomAttributes.First(a=>a.AttributeType==typeof(BepInPlugin)).ConstructorArguments[2].Value!.ToString()!);}catch(Exception e){Plugin.Instance.Log.LogInfo("Update notice unavailable: "+e.Message);}
            }
        }
        if(checkedOnce)return;checkedOnce=true;
        _=Check();
    }
    [Il2CppInterop.Runtime.Attributes.HideFromIl2Cpp]
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
            MenuInputGuard.Claim(new Rect(0,0,1280,720),true);
            GUI.Box(new Rect(345,210,590,285),"CUSTOM ROLES UPDATE");
            GUI.Label(new Rect(370,255,540,90),message.Length>0?message:$"Update {version} available. Download now?\nOnly changed files will download.\nClose Silencer CrewLink before installing.");
            if(starting)
            {
                var fraction=totalBytes>0?Mathf.Clamp01((float)downloadedBytes/totalBytes):0;
                GUI.Box(new Rect(370,350,540,32),"");
                if(fraction>0)GUI.DrawTexture(new Rect(374,354,532*fraction,24),Texture2D.whiteTexture);
                GUI.Label(new Rect(370,389,540,30),totalBytes>0?$"{fraction*100:0}%  ·  {downloadedBytes/1048576d:0.0} / {totalBytes/1048576d:0.0} MB":"Preparing download…");
                if(downloaded && GUI.Button(new Rect(390,435,500,40),"Download complete · close game to install"))Application.Quit();
            }
            else
            {
                if(GUI.Button(new Rect(380,435,245,40),message.Length>0?"Retry":"Yes · download update"))StartUpdate();
                if(GUI.Button(new Rect(655,435,245,40),"Not now"))declined=true;
            }
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
            progressPath=Path.Combine(folder,"status.json");previousStatus="";downloaded=false;downloadedBytes=totalBytes=0;
            File.WriteAllText(progressPath,JsonSerializer.Serialize(new{version,phase="preparing",message="Preparing download…"}));
            var start=new ProcessStartInfo(runner){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=folder};
            foreach(var arg in new[]{script,"--apply",game,Environment.ProcessId.ToString(),plan})start.ArgumentList.Add(arg);
            var process=Process.Start(start)??throw new Exception("Updater could not start");
            starting=true;message="Preparing download…";
        }
        catch(Exception e){message="Update could not start: "+e.Message;Plugin.Instance.Log.LogWarning(message);}
    }
}
