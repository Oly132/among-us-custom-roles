using AmongUs.GameOptions;
using Hazel;
using UnityEngine;
namespace Forger;
internal static class MurderAuthority
{
    const byte Request=233,Decision=232;
    static int sequence,pending;
    static byte pendingTarget=255;
    static float pendingUntil;
    internal static bool Approved;
    static readonly Dictionary<byte,int> seen=new();
    static readonly Dictionary<byte,float> ready=new();
    internal static void Reset(){pending=0;pendingTarget=255;Approved=false;seen.Clear();ready.Clear();}
    internal static bool BeforeRpc(PlayerControl actor,PlayerControl target,bool succeeded)
    {
        if(!succeeded || Approved || !Game.Started || TutorialManager.InstanceExists)return true;
        if(Game.Host)return !FatGuy.TryAbsorb(actor,target);
        if(!actor.AmOwner)return true;
        if(pending!=0 && Time.time<pendingUntil)return false;
        if(!Puppeteer.Living(actor)||!Puppeteer.Living(target)||(actor.Data.Role==null || !actor.Data.Role.IsImpostor))return false;
        pending=++sequence;pendingTarget=target.PlayerId;pendingUntil=Time.time+5;
        Game.Send(Request,w=>{w.Write((byte)1);w.Write(Game.Epoch);w.Write(pending);w.Write(target.PlayerId);},AmongUsClient.Instance.HostId);
        return false;
    }
    static void Respond(PlayerControl actor,byte target,int ticket,bool allow,float cooldown)
    {
        Game.Send(Decision,w=>{w.Write((byte)1);w.Write(Game.Epoch);w.Write(actor.PlayerId);w.Write(target);w.Write(ticket);w.Write(allow);w.Write(cooldown);},actor.OwnerId);
    }
    static void Check(PlayerControl actor,byte targetId,int ticket)
    {
        if(ticket<=0 || (seen.TryGetValue(actor.PlayerId,out var old)&&ticket<=old))return;
        seen[actor.PlayerId]=ticket;
        var target=Puppeteer.Find(targetId);
        var cd=GameOptionsManager.Instance.CurrentGameOptions.GetFloat(FloatOptionNames.KillCooldown);
        var remaining=ready.TryGetValue(actor.PlayerId,out var at)?Math.Max(0,at-Time.time):0;
        var distance=NormalGameOptionsV12.KillDistances[Math.Clamp(GameOptionsManager.Instance.CurrentGameOptions.GetInt(Int32OptionNames.KillDistance),0,2)];
        if(!Game.Started || MeetingHud.Instance || !Puppeteer.Living(actor) || !Puppeteer.Living(target) || actor==target || (actor.Data.Role==null || !actor.Data.Role.IsImpostor) || actor.inVent || target!.inVent || (target.Data.Role==null || target.Data.Role.IsImpostor) || remaining>0 || Vector2.Distance(actor.GetTruePosition(),target.GetTruePosition())>distance+.2f || RolePhysics.Blocked(actor.GetTruePosition(),target.GetTruePosition()))
        {Respond(actor,targetId,ticket,false,remaining);return;}
        ready[actor.PlayerId]=Time.time+cd;
        // A protected attack sends no successful native murder to anybody.
        var protectedHit=FatGuy.TryAbsorb(actor,target);
        Respond(actor,targetId,ticket,!protectedHit,protectedHit?cd:0);
        Plugin.Instance.Log.LogInfo($"Host murder decision: actor={actor.PlayerId}, target={targetId}, ticket={ticket}, absorbed={protectedHit}");
    }
    internal static bool Rpc(PlayerControl sender,byte call,MessageReader reader)
    {
        if(call!=Request&&call!=Decision)return true;
        try
        {
            if(reader.ReadByte()!=1)return false;
            var epoch=reader.ReadInt32();
            if(call==Request)
            {
                var ticket=reader.ReadInt32();var target=reader.ReadByte();
                if(Game.Host && epoch==Game.Epoch)Check(sender,target,ticket);
                return false;
            }
            if(Game.Host || sender.OwnerId!=AmongUsClient.Instance.HostId || epoch!=Game.Epoch)return false;
            var actor=reader.ReadByte();var victim=reader.ReadByte();var nonce=reader.ReadInt32();var allow=reader.ReadBoolean();var cooldown=reader.ReadSingle();
            if(!float.IsFinite(cooldown)||!Game.Local||actor!=Game.Local!.PlayerId||nonce!=pending||victim!=pendingTarget||Time.time>pendingUntil)return false;
            pending=0;pendingTarget=255;
            if(!allow){Game.Local.SetKillTimer(Math.Max(0,cooldown));return false;}
            var targetPlayer=Puppeteer.Find(victim);
            if(!Puppeteer.Living(Game.Local)||!Puppeteer.Living(targetPlayer)||!Game.Local.Data.Role.IsImpostor||MeetingHud.Instance)return false;
            Approved=true;
            try{Game.Local.RpcMurderPlayer(targetPlayer!,true);}
            finally{Approved=false;}
        }
        catch(Exception error){Plugin.Instance.Log.LogWarning("Murder authority RPC: "+error.Message);}
        return false;
    }
}
