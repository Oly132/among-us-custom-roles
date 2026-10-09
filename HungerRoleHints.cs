using HarmonyLib;
using UnityEngine;
namespace Forger;
[HarmonyPatch(typeof(TaskPanelBehaviour),nameof(TaskPanelBehaviour.SetTaskText))]
internal static class CannibalTaskGoal
{
    static void Prefix(ref string str)
    {
        if(!Cannibal.Mine)return;
        str="<color=#B842E6>Cannibal</color>\nEat crewmates and impostors.\nWin alone with one other survivor.\n\nLiving players: "+Cannibal.LivingCount+"\nEaten: "+Cannibal.Eaten.Count;
    }
}
[HarmonyPatch(typeof(RoleManager),nameof(RoleManager.AssignRoleOnDeath))]
internal static class CannibalGhostAlignment
{
    static void Postfix(PlayerControl player)
    {
        if(player.PlayerId!=Cannibal.RoleId || player.Data?.Role==null || !player.Data.IsDead)return;
        RoleManager.Instance.SetRole(player,AmongUs.GameOptions.RoleTypes.CrewmateGhost);
        player.Data.Role.TasksCountTowardProgress=false;
    }
}

