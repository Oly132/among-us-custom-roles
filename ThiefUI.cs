using UnityEngine;
using UnityEngine.Events;
namespace Forger;
public sealed class ThiefUI:MonoBehaviour
{
    AbilityButton? button;
    Sprite? icon;
    static PlayerControl? outlined;
    public ThiefUI(IntPtr p):base(p){}
    internal static void ClearTarget()
    {
        if(outlined)outlined!.ToggleHighlight(false,RoleTeamTypes.Crewmate);
        outlined=null;
    }
    void OnDestroy(){ClearTarget();if(button)UnityEngine.Object.Destroy(button!.gameObject);}
    void LateUpdate()
    {
        var hud=HudManager.Instance;if(hud)NativeHudPlacement.Prepare(hud);
        if(!hud || !Thief.Mine || !Game.Started || !Game.Alive || RoleIntro.Visible || MeetingHud.Instance || Minigame.Instance || ExileController.Instance || (MapBehaviour.Instance && MapBehaviour.Instance.IsOpen))
        {ClearTarget();if(button)button!.gameObject.SetActive(false);return;}
        if(!button)
        {
            button=UnityEngine.Object.Instantiate(hud.AbilityButton,hud.transform);button.name="ThiefSteal";button.enabled=false;
            var aspect=button.GetComponent<AspectPosition>();if(aspect)UnityEngine.Object.Destroy(aspect);
            var click=button.GetComponent<PassiveButton>();click.OnClick=new UnityEngine.UI.Button.ButtonClickedEvent();click.OnClick.AddListener((UnityAction)Thief.Press);
            if(button.glyph)button.glyph.gameObject.SetActive(false);if(button.commsDown)button.commsDown.SetActive(false);
            // Keep the native sprite itself; rebuilding packed atlas sprites can
            // pick the wrong texture rectangle when maps/menus change.
            icon=NativeRoles.Icon(CustomRoles.Roles[Thief.Index]);
            button.buttonLabelText.fontSize=hud.KillButton.buttonLabelText.fontSize;
            button.buttonLabelText.enableAutoSizing=false;
            button.buttonLabelText.transform.localPosition=hud.KillButton.buttonLabelText.transform.localPosition;
            button.buttonLabelText.transform.localScale=hud.KillButton.buttonLabelText.transform.localScale;
            button.graphic.transform.localPosition=hud.KillButton.graphic.transform.localPosition;
        }
        button!.gameObject.SetActive(true);NativeHudPlacement.ShowGraphic(button);button.transform.position=hud.KillButton.transform.position;button.transform.localScale=hud.KillButton.transform.localScale;
        button.graphic.sprite=icon;button.graphic.enabled=true;button.graphic.gameObject.SetActive(true);
        var graphicScale=hud.KillButton.graphic.transform.lossyScale*(hud.KillButton.graphic.sprite.bounds.size.x/Math.Max(.01f,icon!.bounds.size.x));
        var graphicParent=button.graphic.transform.parent.lossyScale;
        button.graphic.transform.localScale=new Vector3(graphicScale.x/graphicParent.x,graphicScale.y/graphicParent.y,graphicScale.z/graphicParent.z);
        button.graphic.transform.position=hud.KillButton.graphic.transform.position;
        button.buttonLabelText.transform.position=hud.KillButton.buttonLabelText.transform.position;
        var labelScale=hud.KillButton.buttonLabelText.transform.lossyScale;var labelParent=button.buttonLabelText.transform.parent.lossyScale;
        button.buttonLabelText.transform.localScale=new Vector3(labelScale.x/labelParent.x,labelScale.y/labelParent.y,labelScale.z/labelParent.z);
        button.OverrideText("STEAL");button.SetUsesRemaining(1);button.SetCooldownFill(0);button.cooldownTimerText.gameObject.SetActive(false);
        hud.AbilityButton.gameObject.SetActive(false);
        var ready=Thief.CanSteal;
        if(ready)button.SetEnabled();else button.SetDisabled();
        button.graphic.color=ready?Color.white:new Color(.5f,.5f,.5f,.65f);
        var target=ready?Thief.Target():null;
        if(outlined!=target){ClearTarget();outlined=target;}
        if(outlined)outlined!.ToggleHighlight(true,RoleTeamTypes.Crewmate);
        if(Input.GetKeyDown(KeyCode.F))Thief.Press();
    }
}
