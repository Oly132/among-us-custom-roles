using HarmonyLib;
using AmongUs.GameOptions;
using UnityEngine;
using System.Reflection;
namespace Forger;
internal static class RegisteredRoles
{
    internal const int First=200;
    internal static int Index(RoleTypes type){var i=(int)type-First;return i>=0&&i<CustomRoles.Roles.Length?i:-1;}
    internal static CustomRole? Get(RoleTypes type){var i=Index(type);return i<0||CustomRoles.Roles.Length==0?null:CustomRoles.Roles[i];}
    internal static void Ensure(RoleManager m)
    {
        if(!m || CustomRoles.Roles.Length==0 || m.AllRoles==null || m.AllRoles.Count==0)return;

        // AllRoles can retain the registered prefabs when the scene creates a
        // new manager. Registration belongs to the pool, not the manager pointer.
        var seen=new HashSet<RoleTypes>();
        for(int i=0;i<m.AllRoles.Count;)
        {
            var existing=m.AllRoles[i];
            if(!existing || (Index(existing.Role)>=0 && !seen.Add(existing.Role)))
            {m.AllRoles.RemoveAt(i);continue;}
            i++;
        }

        bool added=false;
        for(int i=0;i<CustomRoles.Roles.Length;i++)
        {
            if(seen.Contains((RoleTypes)(First+i)))continue;
            var descriptor=CustomRoles.Roles[i];
            var original=m.GetRole(descriptor.Alignment==RoleAlignment.Impostor?RoleTypes.Impostor:RoleTypes.Crewmate);if(!original)continue;
            var role=UnityEngine.Object.Instantiate(original,m.transform);role.name="RegisteredRole_"+CustomRoles.Roles[i].Name;
            role.Role=(RoleTypes)(First+i);role.MaxCount=1;role.NameColor=descriptor.Alignment==RoleAlignment.Impostor?Palette.ImpostorRed:descriptor.Alignment==RoleAlignment.Neutral?Cannibal.Color:Palette.CrewmateBlue;
            if(descriptor.Alignment==RoleAlignment.Neutral){role.TasksCountTowardProgress=false;role.CanUseKillButton=false;role.CanVent=false;}
            role.StringName=(StringNames)(30000+i*4);role.BlurbName=(StringNames)(30001+i*4);role.BlurbNameMed=(StringNames)(30002+i*4);role.BlurbNameLong=(StringNames)(30003+i*4);
            role.RoleIconSolid=role.RoleIconWhite=role.RoleIconColor=NativeRoles.Icon(CustomRoles.Roles[i]);role.RoleScreenshot=role.RoleIconColor;
            role.AllGameSettings=new Il2CppSystem.Collections.Generic.List<BaseGameSetting>();
            m.AllRoles.Add(role);
            seen.Add(role.Role);added=true;
        }
        if(added)Plugin.Instance.Log.LogInfo("Native custom role pool registered: "+string.Join(", ",CustomRoles.Roles.Select(r=>r.Name)));
    }
    internal static RoleTypes WireType(RoleTypes role)=>Get(role)?.Alignment==RoleAlignment.Impostor?RoleTypes.Impostor:RoleTypes.Crewmate;
    internal static string Blurb(int i)=>i switch {0=>"Forge evidence. Mislead the Detective.",1=>"Control crewmates. Kill through them.",2=>"Fake a visual task. Fool the crew.",3=>"Silence the crew and their emergency calls.",4=>"Eat everyone. Win alone.",5=>"Survive one stabbing. Help the crew.",6=>"Unleash your dog. Find this round's victims.",7=>"Explode. Take everyone with you.",8=>"Watch the map. Find the truth.",_=>"Connect the vents. Hunt across the map."};
    internal static void Apply(byte player,int index)
    {
        if(player==255 || !RoleManager.Instance)return;Ensure(RoleManager.Instance);
        var p=Puppeteer.Find(player);if(p && p!.Data?.Role!=null && !p.Data.IsDead && !p.Data.Disconnected && p.Data.Role.Role!=(RoleTypes)(First+index))RoleManager.Instance.SetRole(p,(RoleTypes)(First+index));
    }
}
[HarmonyPatch(typeof(RoleManager),nameof(RoleManager.Awake))]
internal static class RegisterNativeRoles {static void Postfix(RoleManager __instance)=>RegisteredRoles.Ensure(__instance);}
// The game's native pool selects the role. The server sees its normal impostor alignment;
// custom state RPCs restore the registered role on every modded client.
[HarmonyPatch(typeof(PlayerControl),nameof(PlayerControl.RpcSetRole))]
internal static class NativeRoleWire
{
    static void Prefix(ref RoleTypes roleType,ref int __state){__state=RegisteredRoles.Index(roleType);if(__state>=0)roleType=RegisteredRoles.WireType(roleType);}
    static void Postfix(PlayerControl __instance,int __state){if(__state>=0)RegisteredRoles.Apply(__instance.PlayerId,__state);}
}
[HarmonyPatch(typeof(RoleOptionsCollectionV12),nameof(RoleOptionsCollectionV12.GetNumPerGame))]
internal static class NativeCustomCount {static bool Prefix(RoleTypes role,ref int __result){var r=RegisteredRoles.Get(role);if(r==null)return true;__result=r.Count.Value;return false;}}
[HarmonyPatch(typeof(RoleOptionsCollectionV12),nameof(RoleOptionsCollectionV12.GetChancePerGame))]
internal static class NativeCustomChance {static bool Prefix(RoleTypes role,ref int __result){var r=RegisteredRoles.Get(role);if(r==null)return true;__result=r.Chance.Value;return false;}}
[HarmonyPatch(typeof(RoleOptionsCollectionV12),nameof(RoleOptionsCollectionV12.TryGetRoleRates))]
internal static class NativeCustomRates {static bool Prefix(RoleTypes type,ref RoleRate roleRates,ref bool __result){var r=RegisteredRoles.Get(type);if(r==null)return true;roleRates=new RoleRate{MaxCount=r.Count.Value,Chance=r.Chance.Value};__result=true;return false;}}
[HarmonyPatch(typeof(RoleOptionsCollectionV12),nameof(RoleOptionsCollectionV12.SetRoleRate))]
internal static class NativeCustomSetRate {static bool Prefix(RoleTypes role,int maxCount,int chance){var r=RegisteredRoles.Get(role);if(r==null)return true;if(Game.Host){r.Count.Value=Math.Clamp(maxCount,0,1);r.Chance.Value=Math.Clamp(chance,0,100);CustomRoles.Changed();}return false;}}
[HarmonyPatch]
internal static class NativeRoleTranslations
{
    static IEnumerable<MethodBase> TargetMethods()=>typeof(TranslationController).GetMethods().Where(m=>m.Name=="GetString" && m.GetParameters().Length>0 && m.GetParameters()[0].ParameterType==typeof(StringNames));
    static bool Prefix(object[] __args,ref string __result)
    {
        int id=(int)(StringNames)__args[0]-30000;if(id<0||id>=CustomRoles.Roles.Length*4||CustomRoles.Roles.Length==0)return true;
        int role=id/4,part=id%4;__result=part==0?CustomRoles.Roles[role].Name:part==1?RegisteredRoles.Blurb(role):CustomRoles.Roles[role].Description;return false;
    }
}

[HarmonyPatch(typeof(NetworkedPlayerInfo),nameof(NetworkedPlayerInfo.Serialize))]
internal static class NativeRoleDataWire
{
    internal sealed class Snapshot
    {
        internal RoleTypes Role,Type;

    }
    static void Prefix(NetworkedPlayerInfo __instance,ref Snapshot __state)
    {
        __state=new Snapshot{Role=__instance.Role?__instance.Role.Role:RoleTypes.Crewmate,Type=__instance.RoleType};
        if(RegisteredRoles.Index(__state.Role)>=0)__instance.Role.Role=RegisteredRoles.WireType(__state.Role);
        if(RegisteredRoles.Index(__state.Type)>=0)__instance.RoleType=RegisteredRoles.WireType(__state.Type);
    }
    static void Postfix(NetworkedPlayerInfo __instance,Snapshot __state)=>Restore(__instance,__state);
    static Exception? Finalizer(NetworkedPlayerInfo __instance,Snapshot __state,Exception? __exception){Restore(__instance,__state);return __exception;}
    static void Restore(NetworkedPlayerInfo p,Snapshot? state)
    {
        if(state==null)return;if(p.Role)p.Role.Role=state.Role;p.RoleType=state.Type;
    }
}
[HarmonyPatch(typeof(NetworkedPlayerInfo),nameof(NetworkedPlayerInfo.Deserialize))]
internal static class RestoreRegisteredRoleData
{
    static void Postfix(NetworkedPlayerInfo __instance)
    {
        if(__instance.IsDead || (!Game.Started && !Game.WaitingForRoundStart))return;
        for(int i=0;i<CustomRoles.AssignedIds.Length;i++)if(CustomRoles.AssignedIds[i]==__instance.PlayerId){RegisteredRoles.Apply(__instance.PlayerId,i);return;}
    }
}

[HarmonyPatch(typeof(RoleBehaviour),nameof(RoleBehaviour.TeamColor),MethodType.Getter)]
internal static class CannibalNativeColor {static bool Prefix(RoleBehaviour __instance,ref Color __result){if(RegisteredRoles.Index(__instance.Role)!=Cannibal.Index)return true;__result=Cannibal.Color;return false;}}



