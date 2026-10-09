using UnityEngine;
using UnityEngine.Events;
namespace Forger;
public sealed class ExtraRoleUI:MonoBehaviour
{
    AbilityButton? button;
    int shown=-1;
    Sprite? abilityIcon;
    public ExtraRoleUI(IntPtr p):base(p){}
    
    void Update(){Binocular.Tick();}
    void LateUpdate()
    {
        var index=Terrorist.Mine?7:Binocular.Mine?8:-1;var hud=HudManager.Instance;
        if(index<0||!hud||!Game.Started||!Game.Alive||RoleIntro.Visible||MeetingHud.Instance||Minigame.Instance||ExileController.Instance){if(button)button!.gameObject.SetActive(false);return;}
        if(!button)
        {
            shown=-1;abilityIcon=null;
            button=UnityEngine.Object.Instantiate(hud.AbilityButton,hud.AbilityButton.transform.parent);button.enabled=false;button.name="ExtraRoleAbility";
            var aspect=button.GetComponent<AspectPosition>();if(aspect)UnityEngine.Object.Destroy(aspect);
            var passive=button.GetComponent<PassiveButton>();passive.OnClick=new UnityEngine.UI.Button.ButtonClickedEvent();passive.OnClick.AddListener((UnityAction)(()=>{if(Terrorist.Mine)Terrorist.Press();else Binocular.Press();}));
            if(button.glyph)button.glyph.gameObject.SetActive(false);if(button.commsDown)button.commsDown.SetActive(false);
        }
        if(shown!=index){shown=index;var original=NativeRoles.Icon(CustomRoles.Roles[index]);abilityIcon=Sprite.Create(original.texture,original.rect,new Vector2(.5f,.5f),original.rect.width/(hud.KillButton.graphic.sprite.bounds.size.x*1.35f));}
        button!.graphic.sprite=abilityIcon;button.graphic.transform.localScale=hud.KillButton.graphic.transform.localScale;button.SetInfiniteUses();
        button!.gameObject.SetActive(true);button.transform.position=index==7?hud.AbilityButton.transform.position:hud.KillButton.transform.position;
        if(index==7)button.transform.position=new Vector3(hud.SabotageButton.transform.position.x,hud.KillButton.transform.position.y,hud.KillButton.transform.position.z);
        button.graphic.enabled=true;button.graphic.gameObject.SetActive(true);hud.AbilityButton.gameObject.SetActive(false);
        button.OverrideText(index==7?"EXPLODE":"LOOK");var enabled=index==7?Terrorist.CanUse:Binocular.CanUse;
        if(enabled)button.SetEnabled();else button.SetDisabled();
        var left=index==8?Math.Max(0,Binocular.ReadyAt-Time.time):0;button.SetCoolDown(left,index==8?Binocular.Cooldown.Value:1);
        if(Input.GetKeyDown(KeyCode.F)){if(index==7)Terrorist.Press();else Binocular.Press();}
    }
    
}
