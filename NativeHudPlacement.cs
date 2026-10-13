using UnityEngine;
namespace Forger;
internal static class NativeHudPlacement
{
    internal static void Prepare(HudManager hud)
    {
        // Hidden native actions can retain an off-screen position. Anchor our
        // slots to the currently visible native Use/Pet and Report controls.
        if(!(Cannibal.Mine || Binocular.Mine || DogOwner.Mine || Thief.Mine))return;
        if(!hud || !hud.UseButton || !hud.ReportButton || !hud.KillButton || !hud.AbilityButton || !hud.SabotageButton)return;
        var anchor=hud.PetButton && hud.PetButton.gameObject.activeInHierarchy?hud.PetButton.transform:hud.UseButton.transform;
        var lower=anchor.position;
        var spacing=Mathf.Abs(lower.x-hud.ReportButton.transform.position.x);
        if(spacing<.2f || spacing>2f)spacing=1.2f;
        hud.KillButton.transform.position=lower+new Vector3(0,spacing,0);
        hud.AbilityButton.transform.position=lower+new Vector3(-2*spacing,spacing,0);
        hud.SabotageButton.transform.position=lower+new Vector3(-2*spacing,0,0);
    }
    internal static void ShowGraphic(AbilityButton button)
    {
        if(button.graphic){button.graphic.gameObject.SetActive(true);button.graphic.enabled=true;}
        if(button.buttonLabelText){button.buttonLabelText.gameObject.SetActive(true);button.buttonLabelText.enabled=true;}
    }
}
