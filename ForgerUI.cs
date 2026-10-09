using UnityEngine;

namespace Forger;

public sealed class ForgerUI : MonoBehaviour
{
    public ForgerUI(IntPtr pointer) : base(pointer) { }
    void LateUpdate()
    {
        try { EvidenceButton.Update(); RoomPicker.Update(); }
        catch (Exception e) { EvidenceButton.Hide(); Plugin.Instance.Log.LogError("Forger button: " + e); }
    }
    void OnGUI()
    {
        if(RoleIntro.Visible) return;
        if (!Game.Local || !AmongUsClient.Instance) return;
        var scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
        var oldMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1));
        GUI.skin.button.fontSize = 20;
        GUI.skin.label.fontSize = 20;
        GUI.skin.box.fontSize = 20;
        try
        {
            if (!Game.Started) return;
            Game.SettingsOpen = false;
            if (!Game.IsForger || !Game.Alive || MeetingHud.Instance) return;
            if (Time.time < Game.StatusUntil) GUI.Box(new Rect(400, 66, 480, 34), Game.Status);
            if (Game.PickerOpen) { DrawMapPicker(scale); return; }
            if (MapBehaviour.Instance && MapBehaviour.Instance.IsOpen) return;
        }
        catch (Exception e) { Plugin.Instance.Log.LogError(e); Game.PickerOpen = false; }
        finally { GUI.matrix = oldMatrix; }
    }
    static void DrawMapPicker(float scale)
    {
        var state = Game.Evidence;
        if (state.PendingVictim == null || Time.time >= state.Deadline || !MapBehaviour.Instance || !MapBehaviour.Instance.IsOpen)
        {
            Game.PickerOpen = false;
            if (MapBehaviour.Instance && MapBehaviour.Instance.IsOpen) MapBehaviour.Instance.Close();
            Game.Notify("Evidence window closed. No use consumed."); return;
        }
        GUI.Box(new Rect(380, 12, 520, 55), $"CHANGE EVIDENCE · {Math.Ceiling(state.Deadline - Time.time)}s left\nSelect the room your Detective should hear about.");
        if (GUI.Button(new Rect(550, 657, 180, 40), "Cancel")) { Game.PickerOpen = false; MapBehaviour.Instance.Close(); return; }
    }
    static void DrawSettings()
    {
        Panels.Draw(new Rect(350, 75, 580, 370), "FORGER · Four host settings");
        Plugin.Chance.Value = (int)Setting(115, "Assignment chance", Plugin.Chance.Value, 0, 100, 10, "%");
        Plugin.Cooldown.Value = Setting(185, "Change evidence cooldown", Plugin.Cooldown.Value, 0, 120, 5, "s");
        Plugin.Uses.Value = (int)Setting(255, "Uses per game", Plugin.Uses.Value, 1, 15, 1, "");
        Plugin.Window.Value = Setting(325, "Change evidence window", Plugin.Window.Value, 1, 60, 1, "s");
        if (GUI.Button(new Rect(550, 390, 180, 36), "Done")) Game.SettingsOpen = false;
    }
    static float Setting(float y, string label, float value, float min, float max, float step, string unit)
    {
        GUI.Label(new Rect(385, y, 360, 30), label);
        GUI.Label(new Rect(690, y, 95, 30), $"{value:0}{unit}");
        if (GUI.Button(new Rect(785, y, 42, 30), "−")) value = Mathf.Max(min, value - step);
        if (GUI.Button(new Rect(838, y, 42, 30), "+")) value = Mathf.Min(max, value + step);
        return value;
    }
}



