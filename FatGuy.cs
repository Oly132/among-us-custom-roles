using AmongUs.GameOptions;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
namespace Forger;
internal static class FatGuy
{
    internal const int Index=5;
    internal static byte RoleId=255;
    internal static bool StabbingSpent;
    internal static ConfigEntry<int> Chance=null!;
    internal static void Configure()=>Chance=Plugin.Instance.Config.Bind("The Fat Guy","Assignment chance",0,new ConfigDescription("Chance for The Fat Guy, a crewmate who silently survives one non-Viper impostor attack per game.",new AcceptableValueRange<int>(0,100)));
    internal static void Reset(byte id=255){RoleId=id;StabbingSpent=false;RegisteredRoles.Apply(id,Index);}
    internal static bool Is(PlayerControl? p)=>p && p!.PlayerId==RoleId;
    internal static bool EligibleStab(PlayerControl attacker,PlayerControl target)=>!Terrorist.Exploding && Is(target) && !StabbingSpent && Puppeteer.Living(target) && attacker && attacker.Data?.Role!=null && attacker.Data.Role.IsImpostor && attacker.Data.Role.Role!=RoleTypes.Viper;
    internal static bool TryAbsorb(PlayerControl attacker,PlayerControl target)
    {
        if(!Game.Host || !Game.Started || !EligibleStab(attacker,target))return false;
        ApplyAbsorb(attacker.PlayerId);
        Cannibal.Broadcast(2,w=>{w.Write(attacker.PlayerId);w.Write(target.PlayerId);});
        Plugin.Instance.Log.LogInfo($"FAT GUY first stabbing absorbed: attacker={attacker.PlayerId}, target={target.PlayerId}");return true;
    }
    internal static void ApplyAbsorb(byte attacker)
    {
        StabbingSpent=true;
        var p=Puppeteer.Find(attacker);var cd=GameOptionsManager.Instance.CurrentGameOptions.GetFloat(FloatOptionNames.KillCooldown);
        if(p && p!.AmOwner)p.SetKillTimer(cd);
        if(Puppeteer.Active && attacker==Puppeteer.RoleId)Puppeteer.KillReadyAt=Time.time+cd;
    }
    internal static void OnDeath(PlayerControl killer,PlayerControl victim)
    {
        if(Is(victim) && killer.Data.Role.Role==RoleTypes.Viper)HungerEvidence.Add(victim.PlayerId,255,victim.GetTruePosition(),true,false);
    }
}
[HarmonyPatch(typeof(PlayerControl),nameof(PlayerControl.RpcMurderPlayer))]
internal static class FatNativeMurderRpc
{
    static bool Prefix(PlayerControl __instance,PlayerControl target,bool didSucceed)=>!didSucceed || !FatGuy.TryAbsorb(__instance,target);
}
[HarmonyPatch(typeof(PlayerControl),nameof(PlayerControl.MurderPlayer))]
internal static class FatNativeMurder
{
    static bool Prefix(PlayerControl __instance,PlayerControl target,MurderResultFlags resultFlags)=>((resultFlags&MurderResultFlags.Succeeded)==0) || !FatGuy.TryAbsorb(__instance,target);
    static void Postfix(PlayerControl __instance,PlayerControl target){if(target && target.Data.IsDead)FatGuy.OnDeath(__instance,target);}
}
[HarmonyPatch(typeof(ViperDeadBody),nameof(ViperDeadBody.FixedUpdate))]
internal static class FatViperRemains
{
    static bool Prefix(ViperDeadBody __instance)
    {
        var body=__instance.GetComponent<DeadBody>();
        if(!body || !HungerEvidence.Traces.TryGetValue(body.ParentId,out var trace) || !trace.HalfBody || trace.FromCannibal)return true;
        __instance.victimDissolving=false;
        // Retain the native Viper saliva puddle on the surviving half body.
        if(__instance.acidRenderer){__instance.acidRenderer.gameObject.SetActive(true);__instance.acidRenderer.enabled=true;}
        if(__instance.splashRenderer)__instance.splashRenderer.enabled=false;
        if(__instance.spriteAnim)__instance.spriteAnim.enabled=false;
        if(!body.name.EndsWith("_FatHalf"))
        {
            var classic=GameManager.Instance.GetDeadBody(RoleManager.Instance.GetRole(RoleTypes.Impostor));
            if(classic && classic.bodyRenderers!=null && body.bodyRenderers!=null)
                for(int i=0;i<Math.Min(classic.bodyRenderers.Length,body.bodyRenderers.Length);i++)
                {
                    var renderer=body.bodyRenderers[i];var original=classic.bodyRenderers[i];
                    renderer.sprite=original.sprite;renderer.sharedMaterial=original.sharedMaterial;
                    renderer.SetPropertyBlock(null);renderer.color=Color.white;
                    renderer.gameObject.SetActive(true);renderer.enabled=true;
                    renderer.transform.localScale=original.transform.localScale;
                    var victim=Puppeteer.Find(body.ParentId);
                    if(victim && victim!.Data!=null)PlayerMaterial.SetColors(victim.Data.DefaultOutfit.ColorId,renderer);
                }
            if(body.myCollider)body.myCollider.enabled=true;
            body.name+="_FatHalf";
        }
        if(body.bodyRenderers!=null)foreach(var renderer in body.bodyRenderers)if(renderer){renderer.enabled=true;renderer.color=Color.white;}
        // Keep the native reportable lower-half corpse rather than dissolving it.
        return false;
    }
}

