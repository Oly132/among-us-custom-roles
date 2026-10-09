using BepInEx.Configuration;
using Hazel;
using UnityEngine;
namespace Forger;

internal sealed record RoleSetting(string Label,Func<float> Get,Action<float> Set,float Min,float Max,float Step,string Unit="",bool Toggle=false);
internal enum RoleAlignment { Impostor, Crew, Neutral }
internal sealed record CustomRole(string Name,string Description,string Icon,ConfigEntry<int> Count,ConfigEntry<int> Chance,RoleSetting[] Settings,RoleAlignment Alignment=RoleAlignment.Impostor);
internal static class CustomRoles
{
    internal static CustomRole[] Roles=Array.Empty<CustomRole>();
    internal static byte[] AssignedIds=Array.Empty<byte>();
    static float nextSync;
    internal static RoleSetting Int(string label,ConfigEntry<int> entry,int min,int max,int step=1,string unit="") => new(label,()=>entry.Value,v=>entry.Value=(int)v,min,max,step,unit);
    internal static RoleSetting Float(string label,ConfigEntry<float> entry,float min,float max,float step=1,string unit="s") => new(label,()=>entry.Value,v=>entry.Value=v,min,max,step,unit);
    internal static void Configure()
    {
        ConfigEntry<int> Count(string name)=>Plugin.Instance.Config.Bind(name,"Role count",1,new ConfigDescription("Enable one player with this role, or disable it with zero. Each custom role currently supports one player per round.",new AcceptableValueRange<int>(0,1)));
        Roles=new[]{
            new CustomRole("Forger","Forge the room the Detective hears about after your kill. Select false evidence before its window closes.","forge-stamp.png",Count("Forger"),Plugin.Chance,new[]{Float("Evidence cooldown",Plugin.Cooldown,0,120,5),Int("Forgeries per game",Plugin.Uses,1,15),Float("Evidence change window",Plugin.Window,1,60)}),
            new CustomRole("Puppeteer","Control a crewmate remotely and kill through them. Return whenever you choose; your real body stays behind. Sabotage is unavailable while controlling.","possess.png",Count("Puppeteer"),Puppeteer.Chance,new[]{Float("Possession duration",Puppeteer.Duration,3,30),Float("Possession cooldown",Puppeteer.Cooldown,0,120,5),Int("Possessions per game",Puppeteer.Uses,1,15)}),
            new CustomRole("Faker","Complete one real visual task to fake being crew: MedBay scan, Weapons asteroids, or Storage trash. Cancelling keeps your single use.","fake-task.png",Count("Faker"),Faker.Chance,Array.Empty<RoleSetting>()),
            new CustomRole("Silencer","Silence a nearby crewmate's chat and outgoing voice through the next meeting and ten more seconds. They can still hear others. Each target may be silenced once per game.","silence.png",Count("Silencer"),Silencer.Chance,new[]{Float("Silence cooldown",Silencer.Cooldown,0,120,5),Int("Silences per game",Silencer.Uses,1,15)}),
            new CustomRole("Cannibal","Evil neutral. Eat crewmates and impostors. Win alone when you and at most one other living player remain. Eating leaves scent; The Fat Guy leaves reportable remains.","eat.png",Count("Cannibal"),Cannibal.Chance,new[]{Float("Eat cooldown",Cannibal.Cooldown,5,120,5)},RoleAlignment.Neutral),
            new CustomRole("The Fat Guy","Crewmate. Survive one non-Viper impostor attack silently per game. Cannibal and Viper bypass that protection and leave half your body. The Dog identifies the Cannibal only from your remaining half body.","native:guardian",Count("The Fat Guy"),FatGuy.Chance,Array.Empty<RoleSetting>(),RoleAlignment.Crew),
            new CustomRole("Dog Owner","Crewmate. Unleash your dog twice per game. It tracks this round's Cannibal or Viper scent first, otherwise the nearest remaining body. Only you receive the victim, cause, room and three closest players at death. The Fat Guy's Cannibal remains reveal the eater.","unleash.png",Count("Dog Owner"),DogOwner.Chance,new[]{new RoleSetting("Dog visible to everyone",()=>DogOwner.VisibleToEveryone.Value?1:0,v=>DogOwner.VisibleToEveryone.Value=v>=.5f,0,1,1,"",true)},RoleAlignment.Crew),
            new CustomRole("Terrorist","Explode once, killing everyone within close kill range, including teammates. Win for impostors if nobody else remains; otherwise you die too. You can also kill normally.","explode.png",Count("Terrorist"),Terrorist.Chance,Array.Empty<RoleSetting>()),
            new CustomRole("Binocular","Use binoculars anywhere to inspect the map. Move the view using native binocular controls. F closes the view.","binocular.png",Count("Binocular"),Binocular.Chance,new[]{Float("Viewing duration",Binocular.Duration,3,60),Float("Viewing cooldown",Binocular.Cooldown,0,120,5)},RoleAlignment.Crew),
            new CustomRole("Evil Engineer","Impostor. Travel across the whole map through a connected vent network. Follow the native vent arrows from stop to stop; every vent is reachable, with at most three neighbors per vent. You still kill and sabotage normally.","native:engineer",Count("Evil Engineer"),EvilEngineer.Chance,Array.Empty<RoleSetting>())
        };

    }
    internal static void Assign(List<PlayerControl> impostors)
    {
        var ids=Enumerable.Repeat((byte)255,Roles.Length).ToArray();
        foreach(var player in PlayerControl.AllPlayerControls)
        {
            if(!player || player.Data?.Role==null || player.Data.Disconnected)continue;
            var index=RegisteredRoles.Index(player.Data.Role.Role);
            if(index>=0)ids[index]=player.PlayerId;
        }
        AssignedIds=ids;
        Game.Evidence.Reset(ids[0],Plugin.Uses.Value,Plugin.Cooldown.Value,Plugin.Window.Value);
        Puppeteer.Reset(ids[1]);Puppeteer.Epoch=Game.Epoch;Faker.Reset(ids[2]);Silencer.Reset(ids[3]);Silencer.Epoch=Game.Epoch;
        Game.Broadcast(0,w=>{w.Write(ids[0]);w.Write(Plugin.Uses.Value);w.Write(Plugin.Cooldown.Value);w.Write(Plugin.Window.Value);});
        Puppeteer.Broadcast(0,w=>{w.Write(ids[1]);w.Write(Puppeteer.Duration.Value);w.Write(Puppeteer.Cooldown.Value);w.Write(Puppeteer.Uses.Value);});
        Game.Send(249,w=>{w.Write((byte)1);w.Write(Game.Epoch);w.Write((byte)0);w.Write(ids[2]);});
        Game.Send(247,w=>{w.Write((byte)1);w.Write(Game.Epoch);w.Write((byte)0);w.Write(ids[3]);w.Write(Silencer.Uses.Value);w.Write(Silencer.Cooldown.Value);});
        Cannibal.Reset(ids[4]);FatGuy.Reset(ids[5]);DogOwner.Reset(ids[6]);Terrorist.Reset(ids[7]);Binocular.Reset(ids[8]);EvilEngineer.Reset(ids[9]);
        Game.Send(243,w=>{w.Write((byte)1);w.Write(Game.Epoch);w.Write((byte)ids.Length);foreach(var id in ids)w.Write(id);});
        Plugin.Instance.Log.LogInfo("CUSTOM ROLES assigned: "+string.Join(", ",Roles.Select((r,i)=>r.Name+"="+ids[i])));
    }
    internal static void Sync()
    {
        if(!Game.Host || Game.Started || Time.realtimeSinceStartup<nextSync) return;
        nextSync=Time.realtimeSinceStartup+1;
        Game.Send(244,w=>{w.Write((byte)1);foreach(var role in Roles){w.Write(role.Count.Value);w.Write(role.Chance.Value);foreach(var option in role.Settings)w.Write(option.Get());}});
    }
    internal static void Changed() { nextSync=0;Sync();NativeRoles.Refresh(); }
    internal static bool Rpc(PlayerControl sender,byte call,MessageReader r)
    {
        if(call==243)
        {
            if(Game.Host || sender.OwnerId!=AmongUsClient.Instance.HostId)return false;
            try
            {
                if(r.ReadByte()!=1)return false;var epoch=r.ReadInt32();var count=r.ReadByte();if(count!=Roles.Length)return false;
                var ids=new byte[count];for(int i=0;i<count;i++)ids[i]=r.ReadByte();
                AssignedIds=ids;
                Game.Epoch=epoch;Cannibal.Reset(ids[4]);FatGuy.Reset(ids[5]);DogOwner.Reset(ids[6]);Terrorist.Reset(ids[7]);Binocular.Reset(ids[8]);EvilEngineer.Reset(ids[9]);
                for(int i=0;i<count;i++)RegisteredRoles.Apply(ids[i],i);
            }
            catch(Exception e){Plugin.Instance.Log.LogWarning("Role registry assignment RPC: "+e.Message);}
            return false;
        }
        if(call!=244) return true;
        if(Game.Host || Game.Started || sender.OwnerId!=AmongUsClient.Instance.HostId) return false;
        try
        {
            if(r.ReadByte()!=1)return false;
            var values=new List<float>();foreach(var role in Roles){values.Add(r.ReadInt32());values.Add(r.ReadInt32());foreach(var option in role.Settings)values.Add(r.ReadSingle());}
            if(values.Any(v=>!float.IsFinite(v))) return false;
            int n=0;foreach(var role in Roles){role.Count.Value=Math.Clamp((int)values[n++],0,1);role.Chance.Value=Math.Clamp((int)values[n++],0,100);foreach(var option in role.Settings)option.Set(Math.Clamp(values[n++],option.Min,option.Max));}
            NativeRoles.Refresh();
        }
        catch(Exception e) {Plugin.Instance.Log.LogWarning("Custom role settings packet: "+e.Message);}
        return false;
    }
}



