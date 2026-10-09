using UnityEngine;
using UnityEngine.Events;
using AmongUs.GameOptions;

namespace Forger;

internal static class PuppeteerVisual
{
    static PlayerControl? owner;
    static readonly List<(Transform transform,Vector3 position,Quaternion rotation)> parts=new();
    internal static void Restore()
    {
        foreach(var part in parts)if(part.transform){part.transform.localPosition=part.position;part.transform.localRotation=part.rotation;}
        parts.Clear();owner=null;
    }
    internal static void Update()
    {
        if (!Puppeteer.Active) { Restore(); return; }
        var player=Puppeteer.Find(Puppeteer.RoleId);
        if (!player || !player!.cosmetics) return;
        if(owner!=player)
        {
            Restore();owner=player;
            // Shake visual roots only: the collider and network position stay stationary.
            var roots=new List<Transform>();
            foreach(var renderer in player.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if(renderer.GetComponentInParent<PetBehaviour>())continue;
                var visual=renderer.transform;
                while(visual.parent && visual.parent!=player.transform)visual=visual.parent;
                if(visual==player.transform || visual.name.Contains("Shadow",StringComparison.OrdinalIgnoreCase) || visual.name.Contains("Light",StringComparison.OrdinalIgnoreCase))continue;
                if(!roots.Any(t=>t.Pointer==visual.Pointer))roots.Add(visual);
            }
            foreach(var visual in roots)parts.Add((visual,visual.localPosition,visual.localRotation));
        }
        var offset=new Vector3(Mathf.Sin(Time.time*48)*.045f,Mathf.Sin(Time.time*33)*.015f,0);
        var turn=Quaternion.Euler(0,0,Mathf.Sin(Time.time*39)*2);
        foreach(var part in parts)if(part.transform){part.transform.localPosition=part.position+offset;part.transform.localRotation=part.rotation*turn;}
    }
}

internal static class PuppeteerButton
{
    static AbilityButton? button;
    static Sprite? icon;
    internal static void Update()
    {
        if (!Game.Local || !HudManager.InstanceExists) return;
        var hud=HudManager.Instance;
        if (RoleIntro.Visible || !Game.Started || !Puppeteer.Mine || !Game.Alive || MeetingHud.Instance || ExileController.Instance || Minigame.Instance ||
            PossessionTablet.IsOpen || (MapBehaviour.Instance && MapBehaviour.Instance.IsOpen))
        { if (button) button!.gameObject.SetActive(false); return; }
        if (!button)
        {
            button=UnityEngine.Object.Instantiate(hud.AbilityButton,hud.AbilityButton.transform.parent);
            button.name="PuppeteerPossessReturn"; button.enabled=false;
            var click=button.GetComponent<PassiveButton>();
            click.OnClick=new UnityEngine.UI.Button.ButtonClickedEvent();
            click.OnClick.AddListener((UnityAction)Click);
            var aspect=button.GetComponent<AspectPosition>(); if (aspect) UnityEngine.Object.Destroy(aspect);
            if (button.glyph) button.glyph.gameObject.SetActive(false);
            if (button.commsDown) button.commsDown.SetActive(false);
            if (!icon)
            {
                using var stream=typeof(Plugin).Assembly.GetManifestResourceStream("Forger.Assets.possess.png")!;
                using var buffer=new MemoryStream();stream.CopyTo(buffer);
                var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
                ImageConversion.LoadImage(texture,buffer.ToArray(),false);texture.filterMode=FilterMode.Point;
                icon=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,.5f),texture.width/hud.KillButton.graphic.sprite.bounds.size.x);
            }
            button.graphic.sprite=icon;
            button.buttonLabelText.fontSize=hud.KillButton.buttonLabelText.fontSize;
        }
        button!.gameObject.SetActive(true);
        button.transform.position=hud.KillButton.transform.position+hud.SabotageButton.transform.position-hud.UseButton.transform.position;
        button.transform.localScale=hud.KillButton.transform.localScale;
        button.OverrideText(Puppeteer.Active ? "RETURN" : "POSSESS");
        var ready=Puppeteer.Active || Puppeteer.CanPossess;
        if (ready) button.SetEnabled(); else button.SetDisabled();
        button.SetUsesRemaining(Math.Max(0,Puppeteer.Uses.Value-Puppeteer.Used));
        var timer=Puppeteer.Active ? Puppeteer.EndsAt-Time.time : Math.Max(0,Puppeteer.ReadyAt-Time.time);
        button.SetCooldownFill(!Puppeteer.Active ? Mathf.Clamp01(timer/Math.Max(1,Puppeteer.Cooldown.Value)):0);
        button.cooldownTimerText.gameObject.SetActive(timer>0);
        button.cooldownTimerText.text=timer>0 ? Math.Ceiling(timer).ToString():"";
        button.graphic.color=ready ? Color.white:new Color(.5f,.5f,.5f,.65f);
        if (Puppeteer.Active) hud.SabotageButton.gameObject.SetActive(false);
        else if (Game.Local!.Data.Role.IsImpostor) hud.SabotageButton.gameObject.SetActive(true);
        if (Input.GetKeyDown(KeyCode.F)) Click();
    }
    static void Click()
    {
        if (Puppeteer.Active) Puppeteer.Return();
        else if (Puppeteer.CanPossess) PossessionTablet.Open();
    }
}




