using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;
namespace Forger;
internal static class Binocular
{
    internal const int Index=8;
    internal static byte RoleId=255;
    internal static ConfigEntry<int> Chance=null!;
    internal static ConfigEntry<float> Duration=null!,Cooldown=null!;
    internal static float ReadyAt,EndsAt;
    static AsyncOperationHandle<GameObject> asset=null!;
    static bool loading;
    static GameObject? prefabAsset;
    internal static FungleSurveillanceMinigame? View;
    static TMPro.TextMeshPro? timer;
    internal static bool Mine=>Game.Local&&Game.Local!.PlayerId==RoleId;
    internal static bool CanUse=>Mine&&Game.Started&&Game.Alive&&Game.Local!.CanMove&&!Game.Local.inVent&&!MeetingHud.Instance&&!Minigame.Instance&&!RoleIntro.Visible&&Time.time>=ReadyAt;
    internal static void Configure()
    {
        Chance=Plugin.Instance.Config.Bind("Binocular","Assignment chance",0,new ConfigDescription("Chance for a Binocular crewmate.",new AcceptableValueRange<int>(0,100)));
        Duration=Plugin.Instance.Config.Bind("Binocular","Viewing duration",10f,new ConfigDescription("Seconds per use.",new AcceptableValueRange<float>(3,60)));
        Cooldown=Plugin.Instance.Config.Bind("Binocular","Viewing cooldown",30f,new ConfigDescription("Seconds after closing the view.",new AcceptableValueRange<float>(0,120)));
    }
    internal static void Reset(byte id=255){Close();RoleId=id;ReadyAt=0;RegisteredRoles.Apply(id,Index);}
    internal static void Prepare()
    {
        if(prefabAsset||loading||!AmongUsClient.Instance||AmongUsClient.Instance.ShipPrefabs.Count<6)return;
        prefabAsset=AmongUsClient.Instance.ShipPrefabs[5].Asset?.TryCast<GameObject>();
        if(prefabAsset)return;
        asset=AmongUsClient.Instance.ShipPrefabs[5].LoadAssetAsync<GameObject>();loading=true;
    }
    internal static void Press()
    {
        if(View){Close();return;}if(!CanUse)return;Prepare();
        if(!prefabAsset){if(!loading||!asset.IsDone){Game.Notify("Loading binoculars… Press again in a moment.");return;}prefabAsset=asset.Result;}
        try
        {
            var prefab=prefabAsset!.GetComponentInChildren<FungleSurveillanceMinigame>(true);
            if(!prefab)prefab=prefabAsset.GetComponentsInChildren<SystemConsole>(true).Select(c=>c.MinigamePrefab?.TryCast<FungleSurveillanceMinigame>()).FirstOrDefault(p=>p);
            if(!prefab)throw new Exception("Fungle binocular minigame unavailable.");
            View=UnityEngine.Object.Instantiate(prefab!,HudManager.Instance.transform);View.transform.localPosition=new Vector3(0,0,-50);
            // Native Begin needs a Lookout room solely to place the initial camera.
            // Supply the prefab's room during initialization, then restore this map.
            var rooms=ShipStatus.Instance.AllRooms;
            var predicate=new FungleSurveillanceMinigame.__c();
            var lookout=prefabAsset.GetComponentsInChildren<PlainShipRoom>(true).FirstOrDefault(r=>predicate._Begin_b__17_0(r));
            if(!lookout)throw new Exception("Native binocular starting room missing in Fungle prefab.");
            try{if(!rooms.Any(r=>predicate._Begin_b__17_0(r)))ShipStatus.Instance._AllRooms_k__BackingField=new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<PlainShipRoom>(rooms.ToArray().Append(lookout!).ToArray());View.Begin(null!);}
            finally{ShipStatus.Instance._AllRooms_k__BackingField=rooms;}
            View.securityCamera.CollisionsEnabled=false;
            View.securityCamera.transform.SetParent(null,true);
            View.securityCamera.transform.position=new Vector3(Game.Local!.GetTruePosition().x,Game.Local.GetTruePosition().y,-50);
            View.securityCamera.cam.cullingMask &= ~(1<<View.gameObject.layer);
            Plugin.Instance.Log.LogInfo("BINOCULAR camera position="+View.securityCamera.cam.transform.position+", layer="+View.gameObject.layer+", mask="+View.securityCamera.cam.cullingMask);
            EndsAt=Time.time+Duration.Value;
            timer=UnityEngine.Object.Instantiate(HudManager.Instance.KillButton.cooldownTimerText,View.transform);timer.transform.localPosition=new Vector3(0,-2.5f,-3);timer.transform.localScale=Vector3.one;timer.gameObject.SetActive(true);timer.fontSize=2.4f;timer.alignment=TMPro.TextAlignmentOptions.Center;timer.text="10s";
            Plugin.Instance.Log.LogInfo("BINOCULAR opened native Fungle view on "+ShipStatus.Instance.name);
        }
        catch(Exception e){Close();Plugin.Instance.Log.LogError("Binocular view: "+e);Game.Notify("Binoculars could not open.");}
    }
    internal static void Close(){if(View){View!.Close();View=null;ReadyAt=Time.time+Cooldown.Value;}}
    internal static void Closed(FungleSurveillanceMinigame view){if(View&&View!.Pointer==view.Pointer){View=null;timer=null;ReadyAt=Time.time+Cooldown.Value;Plugin.Instance.Log.LogInfo("BINOCULAR closed; cooldown="+Cooldown.Value);}}
    internal static void Tick()
    {
        if(Mine&&Game.Started)Prepare();
        if(View){if(timer)timer!.text=Math.Ceiling(Math.Max(0,EndsAt-Time.time))+"s";if(!View!.gameObject.activeInHierarchy){View=null;ReadyAt=Time.time+Cooldown.Value;}else if(!Mine||!Game.Started||!Game.Alive||MeetingHud.Instance||Time.time>=EndsAt||Input.GetKeyDown(KeyCode.F))Close();}
    }
}
[HarmonyPatch(typeof(FungleSurveillanceMinigame),nameof(FungleSurveillanceMinigame.Close))]
internal static class BinocularClose {static void Postfix(FungleSurveillanceMinigame __instance)=>Binocular.Closed(__instance);}
