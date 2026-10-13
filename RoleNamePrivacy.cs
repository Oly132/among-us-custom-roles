using UnityEngine;
using HarmonyLib;
namespace Forger;
// Keep native role/intro colors, but do not disclose another living player's role.
internal static class RoleNamePrivacy
{
    internal static void Refresh()
    {
        if(!Game.Started || !Game.Local || Game.Local!.Data==null || RoleIntro.Visible)return;
        var viewerImpostor=Game.Local!.Data?.Role?.IsImpostor==true;
        foreach(var player in PlayerControl.AllPlayerControls)
        {
            if(!player || player.Data?.Role==null || !player.cosmetics || !player.cosmetics.nameText)continue;
            player.cosmetics.nameText.color=viewerImpostor && player.Data.Role.IsImpostor
                ? Palette.ImpostorRed : Color.white;
        }
    }
}

[HarmonyPatch(typeof(MeetingHud),nameof(MeetingHud.Update))]
internal static class MeetingNamePrivacy
{
    static void Postfix(MeetingHud __instance)
    {
        if(!Game.Local || Game.Local!.Data?.Role==null)return;
        bool impostor=Game.Local.Data.Role.IsImpostor;
        foreach(var area in __instance.playerStates)
        {
            if(!area || !area.NameText)continue;
            var player=Puppeteer.Find(area.PlayerId.Value);
            area.NameText.color=impostor && player && player!.Data?.Role?.IsImpostor==true?Palette.ImpostorRed:Color.white;
        }
    }
}
