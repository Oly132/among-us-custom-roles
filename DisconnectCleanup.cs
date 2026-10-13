using HarmonyLib;
using InnerNet;
using UnityEngine;
namespace Forger;
// A server-confirmed departure must remain disconnected even when a late
// player-info packet or rejected despawn leaves its object on this client.
internal static class DisconnectCleanup
{
    static readonly Dictionary<int,byte> departed=new();
    internal static void Reset()=>departed.Clear();
    internal static void Left(ClientData data,DisconnectReasons reason)
    {
        if(data==null)return;
        var player=data.Character;
        if(!player)player=PlayerControl.AllPlayerControls.ToArray().FirstOrDefault(p=>p && p.OwnerId==data.Id);
        if(!player)return;
        departed[data.Id]=player!.PlayerId;
        if(player.Data!=null)player.Data.Disconnected=true;
        player.Visible=false;
        if(MeetingHud.Instance)MeetingHud.Instance.HandleDisconnectedPlayerVoteArea(player,reason);
        Plugin.Instance.Log.LogInfo("Disconnect cleanup: owner="+data.Id+", player="+player.PlayerId+", reason="+reason);
    }
    internal static void Refresh()
    {
        if(departed.Count==0)return;
        foreach(var player in PlayerControl.AllPlayerControls)
        {
            if(!player || !departed.ContainsKey(player.OwnerId))continue;
            if(player.Data!=null)player.Data.Disconnected=true;
            player.Visible=false;
        }
        if(!MeetingHud.Instance)return;
        foreach(var area in MeetingHud.Instance.playerStates)
        {
            if(!area)continue;
            var player=Puppeteer.Find(area.PlayerId.Value);
            bool left=player?departed.ContainsKey(player!.OwnerId):departed.Values.Contains(area.PlayerId.Value);
            if(left && !area.AmDead)area.SetDead(true);
        }
    }
}
[HarmonyPatch(typeof(AmongUsClient),nameof(AmongUsClient.OnPlayerLeft))]
internal static class RememberDisconnect
{
    static void Prefix(ClientData data,DisconnectReasons reason)=>DisconnectCleanup.Left(data,reason);
    static void Postfix()=>DisconnectCleanup.Refresh();
}
[HarmonyPatch(typeof(AmongUsClient),nameof(AmongUsClient.OnGameJoined))]
internal static class ClearDisconnectHistory {static void Prefix()=>DisconnectCleanup.Reset();}
[HarmonyPatch(typeof(MeetingHud),nameof(MeetingHud.Update))]
internal static class RemoveDisconnectedVoters {static void Prefix()=>DisconnectCleanup.Refresh();}
