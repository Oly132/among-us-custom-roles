using HarmonyLib;
using UnityEngine;
using BepInEx.Unity.IL2CPP.Utils.Collections;
namespace Forger;
[HarmonyPatch(typeof(IntroCutscene),nameof(IntroCutscene.CoBegin))]
internal static class AssignmentIntroBarrier
{
    static int previousIntroEpoch;
    static void Postfix(ref Il2CppSystem.Collections.IEnumerator __result)
    {
        if(TutorialManager.InstanceExists)return;
        __result=WaitForAssignment(__result).WrapToIl2Cpp();
    }
    static System.Collections.IEnumerator WaitForAssignment(Il2CppSystem.Collections.IEnumerator original)
    {
        float until=Time.realtimeSinceStartup+10;
        while((!CustomRoles.CompleteAssignment || Game.Epoch==previousIntroEpoch) && Time.realtimeSinceStartup<until)
            yield return null;
        if(CustomRoles.CompleteAssignment && Game.Epoch!=previousIntroEpoch)
        {
            previousIntroEpoch=Game.Epoch;
            for(int i=0;i<CustomRoles.AssignedIds.Length;i++)RegisteredRoles.Apply(CustomRoles.AssignedIds[i],i);
            Plugin.Instance.Log.LogInfo("Role intro ready: epoch="+Game.Epoch+", local="+Game.Local?.Data?.Role?.Role);
        }
        else Plugin.Instance.Log.LogWarning("Role assignment timed out before native intro. Check host/client versions and connection.");
        while(original.MoveNext())yield return original.Current;
    }
}
