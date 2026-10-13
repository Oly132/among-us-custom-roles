using HarmonyLib;
using AmongUs.GameOptions;
using UnityEngine;
namespace Forger;

internal static class NeutralRoleSections
{
    const string HeaderName="CustomNeutralRoleHeader";
    internal static RoleAlignment Alignment(RoleBehaviour role)=>RegisteredRoles.Get(role.Role)?.Alignment ?? (role.IsImpostor?RoleAlignment.Impostor:RoleAlignment.Crew);
    internal static void Arrange(RolesSettingsMenu menu)
    {
        var root=menu.RoleChancesSettings.transform;
        var headers=menu.RoleChancesSettings.GetComponentsInChildren<CategoryHeaderEditRole>(true)
            .Where(h=>h && h.Pointer!=menu.categoryHeaderEditRoleOrigin.Pointer).ToArray();
        foreach(var old in headers.Where(h=>h.name==HeaderName)){old.gameObject.SetActive(false);UnityEngine.Object.Destroy(old.gameObject);}
        var native=headers.Where(h=>h.name!=HeaderName && h.gameObject.activeSelf)
            .OrderByDescending(h=>root.InverseTransformPoint(h.transform.position).y).ToArray();
        var rows=menu.roleChances.ToArray().Where(r=>r && r.role).ToArray();
        if(native.Length<2 || rows.Length==0)return;
        var neutral=rows.Where(r=>Alignment(r.role)==RoleAlignment.Neutral).ToArray();
        if(neutral.Length==0)return;
        var crew=native.FirstOrDefault(h=>h.Title.text.Contains("Crewmate",StringComparison.OrdinalIgnoreCase))
            ?? native.FirstOrDefault(h=>h.Background.color.b>h.Background.color.r);
        var impostor=native.FirstOrDefault(h=>h.Title.text.Contains("Impostor",StringComparison.OrdinalIgnoreCase))
            ?? native.FirstOrDefault(h=>h.Background.color.r>h.Background.color.b);
        if(!crew || !impostor)return;
        foreach(var duplicate in native.Where(h=>h.Pointer!=crew!.Pointer && h.Pointer!=impostor!.Pointer))
        {duplicate.gameObject.SetActive(false);UnityEngine.Object.Destroy(duplicate.gameObject);}
        var firstY=native.Max(h=>root.InverseTransformPoint(h.transform.position).y);
        var rowTop=rows.Max(r=>root.InverseTransformPoint(r.transform.position).y);
        var step=Math.Max(.35f,Math.Abs(RolesSettingsMenu.Y_OFFSET));
        var gap=Math.Max(step,firstY-rowTop);
        var extra=UnityEngine.Object.Instantiate(crew!,crew!.transform.parent);
        extra.name=HeaderName;extra.gameObject.SetActive(true);
        foreach(var translator in extra.Title.GetComponents<TextTranslatorTMP>())translator.enabled=false;
        extra.Title.text="Neutral Roles";
        foreach(var renderer in new[]{extra.Background,extra.blankLabel,extra.chanceLabel,extra.countLabel})
            if(renderer){var alpha=renderer.color.a;renderer.color=new Color(Cannibal.Color.r,Cannibal.Color.g,Cannibal.Color.b,alpha);}
        var y=firstY;
        PlaceGroup(crew,RoleAlignment.Crew);
        PlaceGroup(extra,RoleAlignment.Neutral);
        PlaceGroup(impostor!,RoleAlignment.Impostor);

        void PlaceGroup(CategoryHeaderEditRole header,RoleAlignment alignment)
        {
            var group=rows.Where(r=>Alignment(r.role)==alignment).OrderByDescending(r=>root.InverseTransformPoint(r.transform.position).y).ToArray();
            Move(header.transform,y);y-=gap;
            foreach(var row in group){Move(row.transform,y);y-=step;}
            y-=step*.5f;
        }
        void Move(Transform transform,float targetY)
        {
            var position=root.InverseTransformPoint(transform.position);position.y=targetY;transform.position=root.TransformPoint(position);
        }
    }
}

// Change only the native team reveal's presentation; ShowRole and its fades stay native.
internal static class NeutralIntro
{
    internal static bool ShowingTeam;
    internal static bool LocalIsNeutral=>Game.Local && Game.Local!.Data?.Role!=null && RegisteredRoles.Get(Game.Local.Data.Role.Role)?.Alignment==RoleAlignment.Neutral;
    internal static void Apply(IntroCutscene view)
    {
        if(!ShowingTeam || !LocalIsNeutral || !view)return;
        foreach(var translator in view.TeamTitle.GetComponents<TextTranslatorTMP>())translator.enabled=false;
        view.TeamTitle.text="Neutral";
        var color=view.TeamTitle.color;view.TeamTitle.color=new Color(Cannibal.Color.r,Cannibal.Color.g,Cannibal.Color.b,color.a);
        var background=view.BackgroundBar.material.color;view.BackgroundBar.material.color=new Color(Cannibal.Color.r,Cannibal.Color.g,Cannibal.Color.b,background.a);
        view.ImpostorText.gameObject.SetActive(false);
        view.CrewmateRules.SetActive(false);view.ImpostorRules.SetActive(false);
    }
}
[HarmonyPatch(typeof(IntroCutscene),nameof(IntroCutscene.CoBegin))]
internal static class NeutralIntroStart {static void Prefix()=>NeutralIntro.ShowingTeam=false;}
[HarmonyPatch(typeof(IntroCutscene),nameof(IntroCutscene.BeginCrewmate))]
internal static class NeutralTeamReveal
{
    static void Prefix(ref Il2CppSystem.Collections.Generic.List<PlayerControl> __0)
    {
        if(!NeutralIntro.LocalIsNeutral)return;
        NeutralIntro.ShowingTeam=true;
        __0=new Il2CppSystem.Collections.Generic.List<PlayerControl>();__0.Add(Game.Local!);
    }
    static void Postfix(IntroCutscene __instance)=>NeutralIntro.Apply(__instance);
}
[HarmonyPatch(typeof(IntroCutscene),nameof(IntroCutscene.ShowRole))]
internal static class NeutralSpecificReveal {static void Prefix()=>NeutralIntro.ShowingTeam=false;}
