using BepInEx.Configuration;
using HarmonyLib;
using Hazel;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Events;
using Object=UnityEngine.Object;
namespace Forger.Radio;

public sealed class RadioReceiver:MonoBehaviour
{
    public RadioReceiver(IntPtr p):base(p){}
    static ConfigEntry<float> volume=null!;
    static ECDsa? verifier;
    static GameObject? prop;
    static AudioSource? sound;
    static AudioClip? clip;
    static Texture2D? texture;
    static Sprite? sprite;
    static Guid stream;
    static byte owner=255;
    static int lobby,highest=-1,clearSequence=-1;
    static double lastSeen,offset,bestRtt=double.MaxValue,nextPing,baseSourceTime,playAt;
    static long nonce;
    static bool synchronized,scheduled;
    static readonly Dictionary<long,double> pings=new();
    static readonly HashSet<int> received=new();
    static readonly List<RadioWire.Frame> waiting=new();
    static float saveAt=-1;
    static int errors;
    static bool receiverEnabled;
    const int RingBlocks=200;
    public static double Now=>Time.realtimeSinceStartupAsDouble;
    public static bool IsEnabled=>receiverEnabled;
    public static bool InLobby=>receiverEnabled && LobbyBehaviour.Instance && AmongUsClient.Instance && PlayerControl.LocalPlayer && AmongUsClient.Instance.GameState==InnerNet.InnerNetClient.GameStates.Joined;
    public static void Configure()
    {
        receiverEnabled=true;
        volume=Plugin.Instance.Config.Bind("Audio","Radio volume",.7f,new ConfigDescription("Nearby lobby radio volume. Independent of music and effects.",new AcceptableValueRange<float>(0,1)));
        try{verifier=RadioWire.ReceiverKey();}catch(Exception e){Plugin.Instance.Log.LogError("Radio unavailable: "+e.Message);}
    }
    public static bool Rpc(PlayerControl sender,byte call,MessageReader reader)
    {
        if(call!=RadioWire.AudioRpc && call!=RadioWire.ClockReplyRpc && call!=RadioWire.ClockRequestRpc)return true;
        if(call==RadioWire.ClockRequestRpc)return false;
        try
        {
            int count=reader.ReadUInt16();if(count<101 || count>RadioWire.MaxPacket)return false;
            var bytes=new byte[count];for(int i=0;i<count;i++)bytes[i]=reader.ReadByte();
            Accept(sender,bytes);
        }
        catch(Exception e){if(errors++<3)Plugin.Instance.Log.LogWarning("Radio packet ignored: "+e.Message);}
        return false;
    }
    public static void Accept(PlayerControl sender,byte[] bytes)
    {
        if(!InLobby || verifier==null || !sender || sender.Data==null || sender.Data.Disconnected)return;
        var f=RadioWire.Verify(bytes,verifier,AmongUsClient.Instance.GameId,sender.PlayerId);
        if(f==null)return;
        if(f.Kind==2){if(stream==f.Stream && owner==f.Player)Stop();return;}
        if(f.Kind==3)
        {
            if(f.Stream!=stream || owner!=f.Player)return;
            var n=BitConverter.ToInt64(f.Data,0);var sent=BitConverter.ToDouble(f.Data,8);
            if(!pings.Remove(n,out var expected) || expected!=sent)return;
            double rtt=Now-sent;if(rtt<0 || rtt>3)return;
            if(!synchronized || rtt<bestRtt+.015){offset=f.Time+rtt/2-Now;bestRtt=Math.Min(bestRtt,rtt);synchronized=true;}
            return;
        }
        if(stream!=f.Stream || lobby!=f.Lobby)
        {
            // One authenticated publisher per lobby; a new signed session replaces it.
            Stop();stream=f.Stream;owner=f.Player;lobby=f.Lobby;bestRtt=double.MaxValue;
            synchronized=sender.AmOwner;offset=0;nextPing=0;
        }
        lastSeen=Now;
        if(f.Kind==0)return;
        if(received.Contains(f.Sequence) || f.Sequence<highest-RingBlocks || (highest>=0 && f.Sequence>highest+500))return;
        if(!synchronized){if(waiting.Count<30)waiting.Add(f);return;}
        Queue(f);
    }
    static void Queue(RadioWire.Frame f)
    {
        if(received.Contains(f.Sequence))return;
        EnsureProp();if(!clip || !sound)return;
        double deadline=f.Time-offset+RadioWire.BufferSeconds;
        if(deadline<Now+.025)return; // Drop late audio instead of permanently increasing latency.
        if(!scheduled)
        {
            baseSourceTime=f.Time-f.Sequence*.04;playAt=baseSourceTime-offset+RadioWire.BufferSeconds;
            sound!.timeSamples=(f.Sequence%RingBlocks)*RadioWire.Samples;
            sound.PlayScheduled(AudioSettings.dspTime+deadline-Now);scheduled=true;
            clearSequence=f.Sequence-1;
        }
        // Do not allow a discontinuity to overwrite audio that is still being played.
        double expected=baseSourceTime+f.Sequence*.04;
        if(Math.Abs(expected-f.Time)>.02 || deadline-Now>3)return;
        var pcm=RadioAdpcm.Decode(f.Data);
        clip!.SetData(new Il2CppStructArray<float>(pcm),(f.Sequence%RingBlocks)*RadioWire.Samples);
        highest=Math.Max(highest,f.Sequence);received.Add(f.Sequence);
        received.RemoveWhere(n=>n<highest-RingBlocks);
    }
    void Update()
    {
        try{Tick();}catch(Exception e){if(errors++<3)Plugin.Instance.Log.LogError("Radio: "+e.Message);Stop();}
    }
    static void Tick()
    {
        if(saveAt>0 && Time.unscaledTime>=saveAt){saveAt=-1;Plugin.Instance.Config.Save();}
        if(!InLobby || (stream!=Guid.Empty && (lobby!=AmongUsClient.Instance.GameId || Now-lastSeen>2.5))){Stop();return;}
        if(stream==Guid.Empty)return;
        EnsureProp();
        if(!PlayerControl.AllPlayerControls.ToArray().Any(p=>p && p.PlayerId==owner && p.Data!=null && !p.Data.Disconnected)){Stop();return;}
        if(Now>=nextPing)
        {
            nextPing=Now+(synchronized?3:.25);
            var publisher=PlayerControl.AllPlayerControls.ToArray().FirstOrDefault(p=>p && p.PlayerId==owner);
            if(publisher && !publisher!.AmOwner && pings.Count<12)
            {
                var local=PlayerControl.LocalPlayer;long n=++nonce;double t=Now;pings[n]=t;
                var w=AmongUsClient.Instance.StartRpcImmediately(local.NetId,RadioWire.ClockRequestRpc,SendOption.None,publisher.OwnerId);
                foreach(var b in stream.ToByteArray())w.Write(b);
                foreach(var b in BitConverter.GetBytes(n))w.Write(b);
                foreach(var b in BitConverter.GetBytes(t))w.Write(b);
                AmongUsClient.Instance.FinishRpcImmediately(w);
            }
            foreach(var old in pings.Where(p=>Now-p.Value>3).Select(p=>p.Key).ToArray())pings.Remove(old);
        }
        if(synchronized && waiting.Count>0){foreach(var f in waiting.OrderBy(f=>f.Sequence))Queue(f);waiting.Clear();}
        if(prop && sound)
        {
            var delta=(Vector2)prop!.transform.position-PlayerControl.LocalPlayer.GetTruePosition();
            float distance=delta.magnitude;
            float fade=Mathf.Clamp01((7-distance)/5.5f);fade*=fade;
            sound!.volume=volume.Value*fade;sound.panStereo=Mathf.Clamp(delta.x/4,-.85f,.85f);
            if(scheduled && Now>playAt)
            {
                int played=(int)Math.Floor((Now-playAt)/.04);
                // Clear consumed slots, so a lost packet is silence, never an eight-second echo.
                int through=played-2;
                if(through-clearSequence>RingBlocks)clearSequence=through-RingBlocks;
                var silence=new Il2CppStructArray<float>(RadioWire.Samples);
                for(int n=clearSequence+1;n<=through;n++)if(n>=0)clip!.SetData(silence,(n%RingBlocks)*RadioWire.Samples);
                clearSequence=Math.Max(clearSequence,through);
                double expectedSample=((Now-playAt)*RadioWire.Rate)%(RadioWire.Samples*RingBlocks);
                double error=Math.Abs(sound.timeSamples-expectedSample);
                error=Math.Min(error,RadioWire.Samples*RingBlocks-error);
                if(sound.isPlaying && error>RadioWire.Rate*.08)sound.timeSamples=(int)expectedSample;
            }
        }
    }
    static void EnsureProp()
    {
        if(prop || !LobbyBehaviour.Instance)return;
        prop=new GameObject("LobbyRadio");prop.layer=LayerMask.NameToLayer("Ship");
        prop.transform.SetParent(LobbyBehaviour.Instance.transform,false);
        prop.transform.localPosition=new Vector3(0,-1,-.001f);
        if(!sprite)
        {
            using var input=typeof(RadioReceiver).Assembly.GetManifestResourceStream("Forger.Assets.lobby-radio.png");
            if(input==null)throw new IOException("Radio texture missing");
            using var data=new MemoryStream();input.CopyTo(data);texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
            if(!ImageConversion.LoadImage(texture,data.ToArray()))throw new IOException("Radio texture could not be loaded");
            texture.filterMode=FilterMode.Bilinear;sprite=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,.15f),texture.width/1.25f);
        }
        var renderer=prop.AddComponent<SpriteRenderer>();renderer.sprite=sprite;
        renderer.sortingOrder=0;renderer.material=new Material(Shader.Find("Sprites/Default"));
        var collider=prop.AddComponent<BoxCollider2D>();collider.size=new Vector2(1.0f,.38f);collider.offset=new Vector2(0,.13f);collider.isTrigger=false;
        sound=prop.AddComponent<AudioSource>();sound.playOnAwake=false;sound.loop=true;sound.spatialBlend=0;sound.dopplerLevel=0;
        clip=AudioClip.Create("Lobby radio",RadioWire.Samples*RingBlocks,1,RadioWire.Rate,false);
        sound.clip=clip;sound.volume=0;
    }
    static void Stop()
    {
        if(sound)sound!.Stop();if(prop)Object.Destroy(prop);if(clip)Object.Destroy(clip);
        prop=null;sound=null;clip=null;stream=Guid.Empty;owner=255;highest=-1;clearSequence=-1;scheduled=false;synchronized=false;
        received.Clear();waiting.Clear();pings.Clear();
    }
    public static void AddSlider(OptionsMenuBehaviour menu)
    {
        if(!menu.MusicSlider || !menu.SoundSlider || menu.MusicSlider.transform.parent.Find("RadioVolume"))return;
        var music=menu.MusicSlider;var effects=menu.SoundSlider;
        var clone=Object.Instantiate(music,music.transform.parent);clone.name="RadioVolume";
        clone.OnValueChange=new UnityEvent();clone.OnValueChange.AddListener((UnityAction)(()=>{volume.Value=Mathf.Clamp01(clone.Value);saveAt=Time.unscaledTime+.5f;}));
        // Native slider, text, colliders and controller navigation retain their layout/scaling.
        var musicPosition=music.transform.localPosition;var effectsPosition=effects.transform.localPosition;
        float spacing=Mathf.Max(.5f,Mathf.Abs(musicPosition.y-effectsPosition.y));
        clone.transform.localPosition=new Vector3(musicPosition.x,Mathf.Min(musicPosition.y,effectsPosition.y)-spacing,musicPosition.z);
        if(clone.Title && !clone.Title.transform.IsChildOf(clone.transform))
        {
            var title=Object.Instantiate(music.Title,music.Title.transform.parent);
            title.transform.position=music.Title.transform.position+(clone.transform.position-music.transform.position);
            title.transform.SetParent(clone.transform,true);clone.Title=title;
        }
        if(clone.Title)
        {
            foreach(var tr in clone.Title.GetComponents<TextTranslatorTMP>())Object.Destroy(tr);
            clone.Title.text="Radio";
        }
        clone.SetValue(volume.Value);clone.gameObject.SetActive(music.gameObject.activeSelf);
    }
}
[HarmonyPatch(typeof(OptionsMenuBehaviour),nameof(OptionsMenuBehaviour.Start))]
internal static class RadioSettingsPatch
{
    static void Postfix(OptionsMenuBehaviour __instance){try{RadioReceiver.AddSlider(__instance);}catch(Exception e){Plugin.Instance.Log.LogWarning("Radio slider: "+e.Message);}}
}
