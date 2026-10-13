using HarmonyLib;
namespace Forger;
[HarmonyPatch(typeof(TaskPanelBehaviour),nameof(TaskPanelBehaviour.SetTaskText))]
internal static class RoleTaskHints
{
    static string previousHeader="";
    static float nextRefresh;
    internal static int LocalIndex()
    {
        if(!Game.Local)return -1;
        var index=System.Array.IndexOf(CustomRoles.AssignedIds,Game.Local!.PlayerId);
        return index>=0?index:Game.Local.Data?.Role!=null?RegisteredRoles.Index(Game.Local.Data.Role.Role):-1;
    }
    static string Compose(string str,int index)
    {
        var role=CustomRoles.Roles[index];
        if(previousHeader.Length>0 && str.StartsWith(previousHeader,System.StringComparison.Ordinal))str=str.Substring(previousHeader.Length);
        var color=role.Alignment==RoleAlignment.Impostor?"FF1919":role.Alignment==RoleAlignment.Neutral?"B842E6":"8CFFFF";
        previousHeader="<color=#"+color+">"+role.Name+"</color>\n"+RegisteredRoles.Blurb(index)+"\n\n";
        if(index==Cannibal.Index)return previousHeader+"Living players: "+Cannibal.LivingCount+"\nEaten: "+Cannibal.Eaten.Count;
        return str.Contains(role.Name,System.StringComparison.Ordinal)?str:previousHeader+str;
    }
    [HarmonyPriority(Priority.Last)]
    static void Prefix(ref string str)
    {
        if(!Game.Started || !Game.Local || RoleIntro.Visible)return;
        var index=LocalIndex();if(index<0||index>=CustomRoles.Roles.Length){if(previousHeader.Length>0 && str.StartsWith(previousHeader,System.StringComparison.Ordinal))str=str.Substring(previousHeader.Length);previousHeader="";return;}
        str=Compose(str,index);
    }
    internal static void Refresh()
    {
        Thief.RememberLocalResult();
        if(!Game.Started || !Game.Local || RoleIntro.Visible || UnityEngine.Time.time<nextRefresh || !HudManager.Instance || !HudManager.Instance.TaskPanel)return;
        nextRefresh=UnityEngine.Time.time+.5f;
        var panel=HudManager.Instance.TaskPanel;
        if(panel.taskText)panel.SetTaskText(panel.taskText.text);
    }
}
