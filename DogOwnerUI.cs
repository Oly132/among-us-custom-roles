using UnityEngine;
using UnityEngine.Events;
namespace Forger;
public sealed class DogOwnerUI:MonoBehaviour
{
    static AbilityButton? button;
    static Sprite? icon;
    static float nextError;
    public DogOwnerUI(IntPtr pointer):base(pointer){}
    void Update(){DogVisual.Tick();}
    void LateUpdate()
    {
        try{Refresh();}
        catch(Exception e){if(Time.time>=nextError){Plugin.Instance.Log.LogWarning("Dog Owner UI: "+e);nextError=Time.time+5;}if(button)button!.gameObject.SetActive(false);}
    }
    void Refresh()
    {
        var hud=HudManager.Instance;
        if(!hud || !DogOwner.Mine || !Game.Started || !Game.Alive || RoleIntro.Visible || MeetingHud.Instance || Minigame.Instance || ExileController.Instance || (MapBehaviour.Instance && MapBehaviour.Instance.IsOpen)) {if(button)button!.gameObject.SetActive(false);return;}
        if(!button)
        {
            button=UnityEngine.Object.Instantiate(hud.AbilityButton,hud.AbilityButton.transform.parent);button.name="DogOwnerUnleash";button.enabled=false;
            var passive=button.GetComponent<PassiveButton>();passive.OnClick=new UnityEngine.UI.Button.ButtonClickedEvent();passive.OnClick.AddListener((UnityAction)(()=>DogOwner.Unleash()));
            var aspect=button.GetComponent<AspectPosition>();if(aspect)UnityEngine.Object.Destroy(aspect);
            if(button.glyph)button.glyph.gameObject.SetActive(false);if(button.commsDown)button.commsDown.SetActive(false);
            using var stream=typeof(Plugin).Assembly.GetManifestResourceStream("Forger.Assets.unleash.png")!;using var buffer=new MemoryStream();stream.CopyTo(buffer);
            var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);ImageConversion.LoadImage(texture,buffer.ToArray(),false);texture.filterMode=FilterMode.Point;
            icon=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,.5f),texture.width/hud.KillButton.graphic.sprite.bounds.size.x);
            button.graphic.sprite=icon;button.buttonLabelText.fontSize=hud.AbilityButton.buttonLabelText.fontSize;
        }
        button!.gameObject.SetActive(true);button.transform.position=hud.KillButton.transform.position;button.transform.localScale=hud.KillButton.transform.localScale;button.graphic.gameObject.SetActive(true);button.graphic.enabled=true;
        hud.AbilityButton.gameObject.SetActive(false);button.OverrideText("UNLEASH");button.SetUsesRemaining(DogOwner.MaxUses-DogOwner.Used);button.SetCooldownFill(0);button.cooldownTimerText.gameObject.SetActive(false);
        if(DogOwner.CanUnleash)button.SetEnabled();else button.SetDisabled();button.graphic.color=DogOwner.CanUnleash?Color.white:new Color(.5f,.5f,.5f,.65f);
        if(Input.GetKeyDown(KeyCode.F))DogOwner.Unleash();
    }
    
    
}
