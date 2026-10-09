using UnityEngine;
namespace Forger;
internal static class Panels
{
    static GUIStyle? style;
    internal static void Draw(Rect rectangle, string title)
    {
        style ??= new GUIStyle(GUI.skin.box);
        style.normal.background = Texture2D.whiteTexture;
        style.normal.textColor = Color.white;
        style.fontSize = 18;
        var previous = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.04f, 0.05f, 0.06f, 0.96f);
        GUI.Box(rectangle, title, style);
        GUI.backgroundColor = previous;
    }
}
