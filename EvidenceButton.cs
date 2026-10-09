using UnityEngine;
using UnityEngine.Events;

namespace Forger;

// Clone the game's own role button so its font, outline, counter and click feedback match the HUD.
internal static class EvidenceButton
{
    static AbilityButton? button;
    static Sprite? icon;

    static bool available;
    internal static void Hide() { if (button is not null && button) button.gameObject.SetActive(false); }
    internal static void Update()
    {
        if (!Game.Local || !HudManager.InstanceExists) { Hide(); return; }
        var hud = HudManager.Instance;
        if (RoleIntro.Visible || !hud || !Game.Started || !Game.IsForger || !Game.Alive || MeetingHud.Instance || ExileController.Instance ||
            Game.PickerOpen || (MapBehaviour.Instance && MapBehaviour.Instance.IsOpen) || !hud.KillButton.gameObject.activeInHierarchy)
        { Hide(); return; }
        if (!button)
        {
            button = UnityEngine.Object.Instantiate(hud.AbilityButton, hud.AbilityButton.transform.parent);
            button.gameObject.name = "ForgerChangeEvidence";
            button.enabled = false; // The clone must not invoke the base impostor ability.
            var passive = button.GetComponent<PassiveButton>();
            passive.OnClick = new UnityEngine.UI.Button.ButtonClickedEvent();
            passive.OnClick.AddListener((UnityAction)Click);
            var aspect = button.GetComponent<AspectPosition>();
            if (aspect) UnityEngine.Object.Destroy(aspect);
            if (button.glyph) button.glyph.gameObject.SetActive(false);
            if (button.commsDown) button.commsDown.SetActive(false);
            if (!icon)
            {
                using var stream = typeof(Plugin).Assembly.GetManifestResourceStream("Forger.Assets.forge-stamp.png")!;
                using var buffer = new MemoryStream(); stream.CopyTo(buffer);
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                ImageConversion.LoadImage(texture, buffer.ToArray(), false);
                texture.filterMode = FilterMode.Point;
                var width = hud.KillButton.graphic.sprite.bounds.size.x;
                icon = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), texture.width / width);
            }
            button.graphic.sprite = icon;
            button.OverrideText("FORGE");
            button.buttonLabelText.fontSize = hud.KillButton.buttonLabelText.fontSize;
            Plugin.Instance.Log.LogInfo("Forger native evidence button created");
        }
        if (button is null) return;
        button.gameObject.SetActive(true);
        // Spare top-row action slot, directly above Sabotage; tracks the game's safe-area layout.
        button.transform.position = hud.KillButton.transform.position + hud.SabotageButton.transform.position - hud.UseButton.transform.position;
        button.transform.localScale = hud.KillButton.transform.localScale;
        var state = Game.Evidence;
        available = state.PendingVictim is byte victim && state.CanChange(Game.Local!.PlayerId, victim, Time.time) && Game.Local.CanMove;
        if (available) button.SetEnabled(); else button.SetDisabled();
        button.SetUsesRemaining(Math.Max(0, state.MaxUses - state.Used));
        button.usesRemainingSprite.color = new Color(1f, .4f, .4f);
        var opportunity = state.PendingVictim != null && Time.time < state.Deadline && state.Used < state.MaxUses;
        var cooling = Time.time < state.ReadyAt && state.Used < state.MaxUses;
        var remaining = opportunity && !cooling ? state.Deadline - Time.time : cooling ? state.ReadyAt - Time.time : 0;
        button.SetCooldownFill(cooling ? Mathf.Clamp01((float)(remaining / Math.Max(1, state.Cooldown))) : 0);
        button.cooldownTimerText.gameObject.SetActive(remaining > 0);
        button.cooldownTimerText.text = remaining > 0 ? Math.Ceiling(remaining).ToString() : "";
        button.cooldownTimerText.color = opportunity && !cooling ? new Color(1f, .9f, .65f) : Color.white;
        button.graphic.color = available ? Color.white : new Color(.5f, .5f, .5f, .65f);
        button.buttonLabelText.color = available ? Color.white : new Color(.6f, .6f, .6f, .75f);
        if (Input.GetKeyDown(KeyCode.F)) Click();
    }
    static void Click()
    {
        if (!available || Game.PickerOpen || !Game.IsForger || !Game.Alive || MeetingHud.Instance) return;
        var state = Game.Evidence;
        if (state.PendingVictim is not byte victim || !state.CanChange(Game.Local!.PlayerId, victim, Time.time)) return;
        HudManager.Instance.InitMap();
        HudManager.Instance.ToggleMapVisible(new MapOptions { Mode = MapOptions.Modes.Normal, AllowMovementWhileMapOpen = false, ShowLivePlayerPosition = false });
        Game.PickerOpen = true;
    }
}



