using BepInEx.Configuration;
using Hazel;
using UnityEngine;
namespace Forger;

internal static class DogOwner
{
    internal const int Index=6,MaxUses=2;
    const byte Request=238,State=239;
    internal static byte RoleId=255;
    internal static int Used,Sequence;
    internal static bool Active;
    internal static ConfigEntry<int> Chance=null!;
    internal static ConfigEntry<bool> VisibleToEveryone=null!;
    internal static string LastFindings="";
    static DogDeath? pending;
    static float arrivesAt;
    static int pendingRound;
    internal static bool Mine=>Game.Local && Game.Local!.PlayerId==RoleId;
    internal static bool CanUnleash=>Mine && Game.Started && Game.Alive && Game.Local!.CanMove && !Game.Local.inVent && Used<MaxUses && !Active && !RoleIntro.Visible && !MeetingHud.Instance && !Minigame.Instance && !ExileController.Instance;
    internal static void Configure()
    {
        Chance=Plugin.Instance.Config.Bind("Dog Owner","Assignment chance",0,new ConfigDescription("Chance for one Dog Owner crewmate.",new AcceptableValueRange<int>(0,100)));
        VisibleToEveryone=Plugin.Instance.Config.Bind("Dog Owner","Dog visible to everyone",true,"Checked: everyone sees the dog. Unchecked: only its owner sees it. Findings are always private.");
    }
    internal static void Reset(byte id=255)
    {
        RoleId=id;Used=0;Sequence=0;Active=false;LastFindings="";pending=null;DogEvidence.Reset();DogVisual.Cancel();RegisteredRoles.Apply(id,Index);
    }
    static void Send(byte op,Action<MessageWriter>? body=null,int target=-1)=>Game.Send(State,w=>{w.Write((byte)1);w.Write(Game.Epoch);w.Write(op);w.Write(Sequence);body?.Invoke(w);},target);
    internal static void Unleash()
    {
        if(!CanUnleash)return;
        if(Game.Host)Commit(Game.Local!);
        else Game.Send(Request,w=>{w.Write((byte)1);w.Write(Game.Epoch);},AmongUsClient.Instance.HostId);
    }
    internal static bool Commit(PlayerControl sender)
    {
        if(!Game.Host || !Game.Started || !Puppeteer.Living(sender) || sender.PlayerId!=RoleId || !sender.CanMove || sender.inVent || Used>=MaxUses || Active || MeetingHud.Instance || ExileController.Instance)return false;
        var start=sender.GetTruePosition();pending=DogEvidence.Closest(start);pendingRound=DogEvidence.Round;
        var route=pending==null?new[]{start}:DogPath.Find(start,pending.Position);
        float distance=0;for(int i=1;i<route.Length;i++)distance+=Vector2.Distance(route[i-1],route[i]);
        var duration=pending==null?2.8f:Math.Max(1.2f,distance/3.2f+.5f);
        Used++;Sequence++;Active=true;arrivesAt=Time.time+duration;
        DogVisual.Launch(RoleId,Sequence,route,duration,pending==null,VisibleToEveryone.Value);
        Send(1,w=>{w.Write(RoleId);w.Write((byte)Used);w.Write(VisibleToEveryone.Value);w.Write(pending==null);w.Write(duration);w.Write((ushort)route.Length);foreach(var point in route){w.Write(point.x);w.Write(point.y);}});
        return true;
    }
    internal static void Tick()
    {
        if(!Active || !Game.Host)return;
        if(!Game.Started || MeetingHud.Instance || pendingRound!=DogEvidence.Round || !Puppeteer.Living(Puppeteer.Find(RoleId))){Cancel();return;}
        if(Time.time<arrivesAt)return;
        Active=false;var findings=DogEvidence.Findings(pending);pending=null;
        var owner=Puppeteer.Find(RoleId);
        if(owner && owner!.AmOwner)ReceiveFindings(findings);
        else if(owner)Send(2,w=>w.Write(findings),owner!.OwnerId);
        Send(3);DogVisual.Finish();
    }
    internal static void ReceiveFindings(string text)
    {
        if(!Mine)return;LastFindings=text;
        if(HudManager.Instance && !MeetingHud.Instance)HudManager.Instance.Dialogue.Show(text);
    }
    internal static void Cancel(){if(Active && Game.Host)Send(4);Active=false;pending=null;DogVisual.Cancel();}
    internal static void Meeting(){Cancel();DogEvidence.NextRound();}
    internal static bool Rpc(PlayerControl sender,byte call,MessageReader reader)
    {
        if(call!=Request && call!=State)return true;
        try
        {
            if(reader.ReadByte()!=1 || reader.ReadInt32()!=Game.Epoch)return false;
            if(call==Request){if(Game.Host && sender.PlayerId==RoleId)Commit(sender);return false;}
            if(Game.Host || sender.OwnerId!=AmongUsClient.Instance.HostId)return false;
            var op=reader.ReadByte();var sequence=reader.ReadInt32();
            if(op==1)
            {
                var owner=reader.ReadByte();var used=reader.ReadByte();var visible=reader.ReadBoolean();var empty=reader.ReadBoolean();var duration=reader.ReadSingle();var count=reader.ReadUInt16();
                if(owner!=RoleId || sequence<=Sequence || used>MaxUses || count<1 || count>512 || !float.IsFinite(duration) || duration<.1f || duration>300)return false;
                var route=new Vector2[count];for(int i=0;i<count;i++){route[i]=new(reader.ReadSingle(),reader.ReadSingle());if(!float.IsFinite(route[i].x)||!float.IsFinite(route[i].y))return false;}
                Sequence=sequence;Used=used;Active=true;DogVisual.Launch(owner,sequence,route,duration,empty,visible);
            }
            else if(sequence==Sequence)
            {
                if(op==2){var text=reader.ReadString();if(text.Length<1600)ReceiveFindings(text);}
                else if(op==3){Active=false;DogVisual.Finish();}
                else if(op==4){Active=false;DogVisual.Cancel();}
            }
        }
        catch(Exception e){Plugin.Instance.Log.LogWarning("Dog Owner packet: "+e.Message);}
        return false;
    }
}
