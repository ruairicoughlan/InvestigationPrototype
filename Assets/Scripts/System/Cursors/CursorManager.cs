using UnityEngine;

public class CursorManager : MonoBehaviour
{
    public static CursorManager Instance { get; private set; }

    [Header("Cursor State Assets")]
    [Tooltip("Cursor for normal interaction (default arrow).")]
    public CursorStateData normalCursorState;
    [Tooltip("Cursor for hovering over general interactables (clues, buttons).")]
    public CursorStateData interactCursorState;
    [Tooltip("Cursor for hovering over characters (dialogue).")]
    public CursorStateData dialogueCursorState;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Make it persist if needed across scenes
        }
        else
        {
            Destroy(gameObject); // Destroy duplicate instances
            return;
        }
    }

    private void Start()
    {
        // Set the initial cursor to the normal state
        SetCursorToNormal();
    }

    /// <summary>
    /// Sets the system cursor based on the provided CursorStateData.
    /// </summary>
    /// <param name="stateData">The ScriptableObject defining the cursor texture and hotspot.</param>
    public void ApplyCursorState(CursorStateData stateData)
    {
        if (stateData != null && stateData.cursorTexture != null)
        {
            Cursor.SetCursor(stateData.cursorTexture, stateData.hotspot, CursorMode.Auto);
        }
        else
        {
            // Revert to OS hardware cursor if no valid state data is provided
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            if (stateData == null)
            {
                Debug.LogWarning("CursorManager: Attempted to apply a null CursorStateData.");
            }
            else
            {
                Debug.LogWarning($"CursorManager: CursorStateData '{stateData.name}' is missing its cursorTexture.");
            }
        }
    }

    /// <summary>
    /// Sets the cursor to the 'Normal' state.
    /// </summary>
    public void SetCursorToNormal()
    {
        if (normalCursorState != null)
        {
            ApplyCursorState(normalCursorState);
        }
        else
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto); // Fallback to hardware default
            Debug.LogWarning("CursorManager: NormalCursorState is not assigned. Reverting to hardware cursor.");
        }
    }

    /// <summary>
    /// Sets the cursor to the 'Interact' state (for clues, buttons, general interactables).
    /// </summary>
    public void SetCursorToInteract()
    {
        if (interactCursorState != null)
        {
            ApplyCursorState(interactCursorState);
        }
        else
        {
            Debug.LogWarning("CursorManager: InteractCursorState is not assigned. Reverting to normal cursor.");
            SetCursorToNormal(); // Fallback
        }
    }

    /// <summary>
    /// Sets the cursor to the 'Dialogue' state (for characters).
    /// </summary>
    public void SetCursorToDialogue()
    {
        if (dialogueCursorState != null)
        {
            ApplyCursorState(dialogueCursorState);
        }
        else
        {
            Debug.LogWarning("CursorManager: DialogueCursorState is not assigned. Reverting to normal cursor.");
            SetCursorToNormal(); // Fallback
        }
    }
}