using UnityEngine;
using UnityEngine.Events;
using TMPro;

namespace Forger;

internal static class RoomPicker
{
    static MapBehaviour? owner;
    static readonly List<GameObject> buttons = new();
    internal static void Clear()
    {
        foreach (var b in buttons) if (b) UnityEngine.Object.Destroy(b);
        buttons.Clear(); owner = null;
    }
    internal static void Update()
    {
        var map = MapBehaviour.Instance;
        if (!Game.PickerOpen || !map || !map.IsOpen) { if (buttons.Count > 0) Clear(); return; }
        if (owner == map && buttons.Count > 0) return;
        Clear(); owner = map;
        map.taskOverlay.gameObject.SetActive(false);
        map.fadedBackground.SetActive(true);
        foreach (var room in Game.Rooms())
        {
            var roomId = (byte)room.RoomId;
            var name = Game.RoomName(roomId);
            TextMeshPro? label = null;
            foreach (var text in map.GetComponentsInChildren<TextMeshPro>(true))
                if (text.text == name) { label = text; break; }
            var obj = UnityEngine.Object.Instantiate(map.detectiveMapButtonPrefab, map.transform);
            obj.name = "ForgerRoom_" + name;
            if (label != null) obj.transform.position = label.transform.position + new Vector3(0, -.32f, -.1f);
            else
            {
                var center = room.roomArea.bounds.center;
                var here = Game.Local!.GetTruePosition();
                var offset = map.HerePoint.transform.position - map.transform.TransformPoint(new Vector3(here.x / ShipStatus.Instance.MapScale, here.y / ShipStatus.Instance.MapScale, -1));
                obj.transform.position = map.transform.TransformPoint(center / ShipStatus.Instance.MapScale) + offset;
            }
            var click = obj.GetComponent<PassiveButton>() ?? obj.GetComponentInChildren<PassiveButton>();
            click.OnClick = new UnityEngine.UI.Button.ButtonClickedEvent();
            click.OnClick.AddListener((UnityAction)(() => { Game.RequestRoom(roomId); Clear(); }));
            obj.SetActive(true); buttons.Add(obj);
        }
        Plugin.Instance.Log.LogInfo($"Forger native pencil room picker: {buttons.Count} rooms");
    }
}
