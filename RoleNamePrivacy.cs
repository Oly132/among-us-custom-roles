using UnityEngine;
namespace Forger;
// Keep native role/intro colors, but do not disclose another living player's role.
internal static class RoleNamePrivacy
{
    internal static void Refresh()
    {
        if(!Game.Started || !Game.Alive || RoleIntro.Visible)return;
        foreach(var player in PlayerControl.AllPlayerControls)
        {
            if(!player || player.PlayerId==Game.Local!.PlayerId || player.Data?.Role==null || !player.cosmetics || !player.cosmetics.nameText)continue;
            var role=RegisteredRoles.Get(player.Data.Role.Role);
            if(role!=null && role.Alignment!=RoleAlignment.Impostor)player.cosmetics.nameText.color=Color.white;
        }
    }
}
