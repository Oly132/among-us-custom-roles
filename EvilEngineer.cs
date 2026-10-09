using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using System.Reflection;
namespace Forger;

internal static class EvilEngineer
{
    internal const int Index=9;
    internal static byte RoleId=255;
    internal static ConfigEntry<int> Chance=null!;
    static IntPtr ship;
    internal static Vent[] Vents=Array.Empty<Vent>();
    internal static Dictionary<int,Vent[]> Routes=new();
    static bool scoped;
    internal static bool Mine=>Game.Local&&Game.Local!.PlayerId==RoleId&&Game.Local.Data?.Role!=null&&RegisteredRoles.Index(Game.Local.Data.Role.Role)==Index;
    internal static void Configure()=>Chance=Plugin.Instance.Config.Bind("Evil Engineer","Assignment chance",0,new ConfigDescription("Chance for an impostor with a connected, step-by-step vent network.",new AcceptableValueRange<int>(0,100)));
    internal static void Reset(byte id=255){RoleId=id;ship=IntPtr.Zero;Vents=Array.Empty<Vent>();Routes.Clear();RegisteredRoles.Apply(id,Index);}
    internal static void Prepare()
    {
        if(!ShipStatus.Instance||scoped)return;
        if(ship==ShipStatus.Instance.Pointer&&Vents.Length>0&&Vents.All(v=>v))return;
        Vents=UnityEngine.Object.FindObjectsOfType<Vent>().Where(v=>v&&v.gameObject.activeInHierarchy).OrderBy(v=>v.Id).ToArray();
        if(Vents.Length==0)return;
        var native=Vents.Select(v=>new[]{v.Left,v.Right,v.Center}.Where(n=>n).Select(n=>n.Id).ToArray()).ToArray();
        var positions=Vents.Select(v=>(Vector2)v.transform.position).ToArray();
        var graph=VentRoutes.Build(positions,native,Vents.Select(v=>v.Id).ToArray());
        Routes=Enumerable.Range(0,Vents.Length).ToDictionary(i=>Vents[i].Id,i=>graph[i].Select(j=>Vents[j]).OrderBy(v=>Mathf.Atan2(v.transform.position.y-Vents[i].transform.position.y,v.transform.position.x-Vents[i].transform.position.x)).ToArray());
        ship=ShipStatus.Instance.Pointer;
        Plugin.Instance.Log.LogInfo("EVIL ENGINEER network: "+string.Join("; ",Vents.Select(v=>v.name+" -> "+string.Join(",",Routes[v.Id].Select(n=>n.name)))));
    }
    internal sealed class Scope
    {
        readonly (Vent vent,Vent left,Vent right,Vent center)[] saved;
        bool restored;
        internal Scope()
        {
            saved=Vents.Select(v=>(v,v.Left,v.Right,v.Center)).ToArray();scoped=true;
            foreach(var v in Vents){var links=Routes[v.Id];v.Left=links.Length>0?links[0]:null!;v.Right=links.Length>1?links[1]:null!;v.Center=links.Length>2?links[2]:null!;}
        }
        internal void Restore(){if(restored)return;restored=true;foreach(var s in saved)if(s.vent){s.vent.Left=s.left;s.vent.Right=s.right;s.vent.Center=s.center;}scoped=false;}
    }
    internal static Scope? OpenScope(){if(!Mine||scoped||!Game.Started)return null;Prepare();return Vents.Length>0?new Scope():null;}
    
}

// A sparse geographic spanning tree, with original links kept when ports permit.
// Every component always has a free port, so the sorted edge pass connects all
// vents. Three is the native UI's maximum number of arrow slots.
internal static class VentRoutes
{
    internal static List<int>[] Build(Vector2[] positions,int[][] native,int[] ids)
    {
        int count=positions.Length;var links=Enumerable.Range(0,count).Select(_=>new List<int>()).ToArray();var parent=Enumerable.Range(0,count).ToArray();
        int Root(int a){while(parent[a]!=a){parent[a]=parent[parent[a]];a=parent[a];}return a;}
        void Add(int a,int b){links[a].Add(b);links[b].Add(a);}
        var edges=(from a in Enumerable.Range(0,count) from b in Enumerable.Range(a+1,count-a-1) select(a,b,distance:(positions[a]-positions[b]).sqrMagnitude)).OrderBy(e=>e.distance).ThenBy(e=>ids[e.a]).ThenBy(e=>ids[e.b]).ToArray();
        foreach(var e in edges)if(Root(e.a)!=Root(e.b)&&links[e.a].Count<3&&links[e.b].Count<3){Add(e.a,e.b);parent[Root(e.a)]=Root(e.b);}
        var lookup=Enumerable.Range(0,count).ToDictionary(i=>ids[i]);
        for(int a=0;a<count;a++)foreach(var id in native[a])if(lookup.TryGetValue(id,out int b)&&a!=b&&!links[a].Contains(b)&&links[a].Count<3&&links[b].Count<3)Add(a,b);
        return links;
    }
}

[HarmonyPatch]
internal static class EvilEngineerNativeVents
{
    static IEnumerable<MethodBase> TargetMethods()=>typeof(Vent).GetMethods().Where(m=>m.Name is "Use" or "SetButtons" or "ClickLeft" or "ClickRight" or "ClickCenter" or "TryMoveToVent" or "UpdateArrows");
    static void Prefix(ref EvilEngineer.Scope? __state)=>__state=EvilEngineer.OpenScope();
    static void Postfix(EvilEngineer.Scope? __state)=>__state?.Restore();
    static Exception? Finalizer(EvilEngineer.Scope? __state,Exception? __exception){__state?.Restore();return __exception;}
}
