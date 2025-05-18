using UnityEngine;

[CreateAssetMenu(fileName = "NewCursorState", menuName = "Project Dublin/Cursors/Cursor State Data")]
public class CursorStateData : ScriptableObject
{
    [Tooltip("The texture to use for this cursor state.")]
    public Texture2D cursorTexture;

    [Tooltip("The offset from the top-left of the texture to use as the cursor's active click point (hotspot). Default is (0,0).")]
    public Vector2 hotspot = Vector2.zero;

    [Tooltip("A descriptive name for this cursor state (for editor organization).")]
    public string stateName = "DefaultCursor";
}