using Innersloth.Assets;
using UnityEngine;
namespace Forger;
internal static class DogVisual
{
    static AddressableAsset<PetBehaviour>? petAsset;
    static GameObject? root;
    static PetBehaviour? pet;
    static Vector2[] route=Array.Empty<Vector2>();
    static float began,duration,distance;
    static bool empty,finishing,visible;
    static byte owner;
    static float finishAt;
    internal static bool Exists=>root;
    internal static bool NativePet=>pet;
    internal static int Poofs;
    internal static void Prepare()
    {
        if(petAsset!=null || !HatManager.Instance)return;
        var data=HatManager.Instance.allPets.FirstOrDefault(p=>p && ((p.ProductId??"").Contains("dog",StringComparison.OrdinalIgnoreCase) || p.name.Contains("dog",StringComparison.OrdinalIgnoreCase)));
        if(!data){Plugin.Instance.Log.LogWarning("Dog Owner: native dog pet not found.");return;}
        petAsset=data!.CreateAddressableAsset();petAsset.LoadAsync();Plugin.Instance.Log.LogInfo("Dog Owner pet asset: "+data.ProductId);
    }
    internal static void Launch(byte player,int sequence,Vector2[] path,float seconds,bool noBody,bool everyone)
    {
        Cancel();Prepare();owner=player;route=path;began=Time.time;duration=seconds;empty=noBody;finishing=false;
        visible=everyone || Game.Local?.PlayerId==player;
        distance=0;for(int i=1;i<route.Length;i++)distance+=Vector2.Distance(route[i-1],route[i]);
        if(!visible)return;
        root=new GameObject("DogOwnerSearch_"+sequence);root.layer=LayerMask.NameToLayer("Players");root.transform.position=new Vector3(path[0].x,path[0].y,path[0].y/1000f-.02f);
        TryPet();Poof(path[0],true);
    }
    static void TryPet()
    {
        if(!root || pet || petAsset==null || !petAsset.IsLoaded())return;
        var prefab=petAsset.GetAsset();if(!prefab)return;
        pet=UnityEngine.Object.Instantiate(prefab,root!.transform);pet.name="SearchDog";pet.transform.localPosition=Vector3.zero;pet.gameObject.SetActive(true);pet.enabled=false;pet.manualMoving=true;pet.viewOnly=true;
        foreach(var collider in pet.GetComponentsInChildren<Collider2D>())collider.enabled=false;
        foreach(var body in pet.GetComponentsInChildren<Rigidbody2D>())body.simulated=false;
        pet.SetDefaultMaterial();pet.Visible=true;
        if(pet.animator && (empty?pet.sadClip:pet.walkClip))pet.animator.Play(empty?pet.sadClip:pet.walkClip);
        Plugin.Instance.Log.LogInfo("Dog Owner native dog instantiated.");
    }
    internal static void Tick()
    {
        if(!root)return;
        if(!Game.Started || MeetingHud.Instance){Cancel();return;}
        TryPet();
        if(finishing){if(Time.time>=finishAt)Cancel();return;}
        var elapsed=Time.time-began;
        if(empty)
        {
            var sinking=Mathf.Clamp01((elapsed-.65f)/1.6f);
            root!.transform.position=new Vector3(route[0].x,route[0].y-sinking*.45f,route[0].y/1000f-.02f);
            root.transform.localScale=new Vector3(1,1-sinking*.95f,1);
            if(pet)pet!.SetAlpha(1-sinking);
            return;
        }
        var progress=Mathf.Clamp01((elapsed-.35f)/Math.Max(.1f,duration-.5f));var remaining=distance*progress;var position=route[0];var direction=Vector2.right;
        for(int i=1;i<route.Length;i++)
        {
            var length=Vector2.Distance(route[i-1],route[i]);
            if(remaining<=length){position=Vector2.Lerp(route[i-1],route[i],length>0?remaining/length:1);direction=route[i]-route[i-1];break;}
            remaining-=length;position=route[i];
        }
        root!.transform.position=new Vector3(position.x,position.y,position.y/1000f-.02f);if(pet && Math.Abs(direction.x)>.01f)pet!.FlipX=direction.x<0;
        if(progress>=1 && pet)pet!.SetIdle();
    }
    internal static void Finish()
    {
        if(!root)return;
        if(empty){Cancel();return;}
        Poof(route[^1],false);if(pet)pet!.Visible=false;finishing=true;finishAt=Time.time+.7f;
    }
    internal static void Cancel(){if(root)UnityEngine.Object.Destroy(root);root=null;pet=null;route=Array.Empty<Vector2>();}
    static void Poof(Vector2 position,bool appear)
    {
        if(!visible)return;
        var type=appear?RoleEffectAnimation.EffectType.Appear_Poof:RoleEffectAnimation.EffectType.Vanish_Poof;
        var template=Resources.FindObjectsOfTypeAll<RoleEffectAnimation>().FirstOrDefault(e=>e && e.effectType==type);
        if(!template){Plugin.Instance.Log.LogWarning("Dog Owner: native "+type+" effect unavailable.");return;}
        var effect=UnityEngine.Object.Instantiate(template!);effect.enabled=false;effect.gameObject.name="DogOwner_"+type;effect.gameObject.SetActive(true);
        effect.transform.position=new Vector3(position.x,position.y,position.y/1000f-.05f);effect.transform.localScale=Vector3.one*.7f;
        if(effect.Renderer){effect.Renderer.enabled=true;effect.Renderer.maskInteraction=SpriteMaskInteraction.None;effect.Renderer.sortingOrder=12;}
        var player=Puppeteer.Find(owner);if(player && player!.Data!=null)effect.SetMaterialColor(player.Data.DefaultOutfit.ColorId);
        if(effect.Animator && effect.Clip)effect.Animator.Play(effect.Clip);
        UnityEngine.Object.Destroy(effect.gameObject,(effect.Clip?effect.Clip!.length:.5f)+.15f);Poofs++;
    }
}



