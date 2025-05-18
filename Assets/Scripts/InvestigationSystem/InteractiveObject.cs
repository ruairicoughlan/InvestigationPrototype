using UnityEngine;
using UnityEngine.EventSystems; // Required for mouse interaction interfaces (IPointerEnterHandler, etc.)
using System.Collections.Generic; // <-- THIS LINE IS ADDED TO FIX THE COMPILE ERROR

public class InteractiveObject : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public enum ObjectType
    {
        Clue,
        Character
        // Future ideas: WitnessHotspot (if witnesses become clickable directly in scene)
    }

    [Header("Object Configuration")]
    public ObjectType objectType = ObjectType.Clue; // Default, will be set during initialization

    // Data references - these will be set by InvestigationManager when this object is spawned
    // We use properties with private setters to ensure they are only set during initialization.
    public ClueData ClueSpecificData { get; private set; }
    public CharacterPlacementData CharacterSpecificData { get; private set; }

    private InvestigationManager investigationManager; // Reference to the main scene manager
    private BoxCollider2D interactionCollider; // We'll add and configure this collider

    // --- INITIALIZATION METHODS ---
    // These are called by InvestigationManager when it creates this interactive object

    /// <summary>
    /// Sets up this interactive object as a Clue.
    /// </summary>
    public void InitializeAsClue(ClueData data, InvestigationManager manager)
    {
        objectType = ObjectType.Clue;
        ClueSpecificData = data;
        investigationManager = manager;
        gameObject.name = "ClueHotspot_" + (ClueSpecificData != null ? ClueSpecificData.clueID : "UnnamedClue");

        // Ensure there's a BoxCollider2D for interaction
        interactionCollider = GetComponent<BoxCollider2D>();
        if (interactionCollider == null)
        {
            interactionCollider = gameObject.AddComponent<BoxCollider2D>();
        }
        // Configure the collider size based on ClueData (if provided, otherwise default)
        if (ClueSpecificData != null)
        {
            interactionCollider.size = ClueSpecificData.colliderSize;
            // Note: If ClueData.colliderSize is (0,0), you might want a default, e.g., (1,1)
            if (ClueSpecificData.colliderSize == Vector2.zero) interactionCollider.size = Vector2.one;
        }
        interactionCollider.isTrigger = true; // Usually best for UI-like raycast interactions
    }

    /// <summary>
    /// Sets up this interactive object as a Character.
    /// </summary>
    public void InitializeAsCharacter(CharacterPlacementData data, InvestigationManager manager)
    {
        objectType = ObjectType.Character;
        CharacterSpecificData = data;
        investigationManager = manager;
        gameObject.name = "CharacterInteract_" + (CharacterSpecificData != null && CharacterSpecificData.characterProfile != null ? CharacterSpecificData.characterProfile.characterID : "UnnamedCharacter");

        // Ensure there's a BoxCollider2D for interaction
        // You might want a CircleCollider2D or CapsuleCollider2D for characters depending on their shape
        interactionCollider = GetComponent<BoxCollider2D>();
        if (interactionCollider == null)
        {
            interactionCollider = gameObject.AddComponent<BoxCollider2D>();
        }
        // TODO: Set collider size appropriately for the character sprite if needed.
        // This might come from CharacterPlacementData or be based on the sprite bounds.
        // For now, a default or manual setup will work.
        interactionCollider.isTrigger = true;
    }


    // --- UNITY UI EVENT SYSTEM INTERFACES ---
    // These methods are automatically called by Unity's EventSystem
    // when the mouse interacts with the GameObject this script is on (if it has a Collider).

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (investigationManager == null || GameStateManager.Instance == null) return;

        // Debug.Log($"Mouse Enter: {gameObject.name}");

        if (objectType == ObjectType.Clue && ClueSpecificData != null)
        {
            // Show Skill Check UI if this clue has one for more info, and player has the skill
            if (ClueSpecificData.requiresSkillCheckForMoreInfo)
            {
                int playerSkillLevel = GameStateManager.Instance.GetSkillLevel(ClueSpecificData.skillCheckType.ToString());
                bool checkSucceeded = playerSkillLevel >= ClueSpecificData.skillCheckDC;
                investigationManager.ShowSkillCheckUI(ClueSpecificData, checkSucceeded);
            }
            // TODO: Change mouse cursor visual (e.g., to a hand or magnifying glass)
        }
        else if (objectType == ObjectType.Character && CharacterSpecificData != null)
        {
            // TODO: Change mouse cursor visual (e.g., to a speech bubble)
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (investigationManager == null) return;

        // Debug.Log($"Mouse Exit: {gameObject.name}");

        // Always hide skill check UI when mouse exits, regardless of type
        investigationManager.HideSkillCheckUI();

        // TODO: Change mouse cursor back to default
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (investigationManager == null || GameStateManager.Instance == null) return;

        Debug.Log($"Clicked on: {gameObject.name} (Type: {objectType})");
        investigationManager.HideSkillCheckUI(); // Hide skill check on click before showing popup

        if (objectType == ObjectType.Clue && ClueSpecificData != null)
        {
            string descriptionToShow = ClueSpecificData.baseDescription;
            if (ClueSpecificData.requiresSkillCheckForMoreInfo)
            {
                int playerSkillLevel = GameStateManager.Instance.GetSkillLevel(ClueSpecificData.skillCheckType.ToString());
                if (playerSkillLevel >= ClueSpecificData.skillCheckDC)
                {
                    descriptionToShow = !string.IsNullOrEmpty(ClueSpecificData.successDescription) ? ClueSpecificData.successDescription : ClueSpecificData.baseDescription;
                    Debug.Log($"Skill check for '{ClueSpecificData.clueName}' PASSED!");
                }
                else
                {
                    descriptionToShow = !string.IsNullOrEmpty(ClueSpecificData.failureDescription) ? ClueSpecificData.failureDescription : ClueSpecificData.baseDescription;
                    Debug.Log($"Skill check for '{ClueSpecificData.clueName}' FAILED (Player Skill: {playerSkillLevel}, DC: {ClueSpecificData.skillCheckDC}).");
                }
            }

            investigationManager.ShowClueInfoPopup(ClueSpecificData, descriptionToShow);

            // Handle Key Evidence logic
            if (ClueSpecificData.isKeyEvidence)
            {
                Debug.Log($"'{ClueSpecificData.clueName}' is Key Evidence. Updating case info.");
                if (!string.IsNullOrEmpty(ClueSpecificData.relatedCaseID) && !string.IsNullOrEmpty(ClueSpecificData.caseFlagToSet))
                {
                    GameStateManager.Instance.SetCaseFlag(ClueSpecificData.relatedCaseID, ClueSpecificData.caseFlagToSet, true);
                }
                if(!string.IsNullOrEmpty(ClueSpecificData.relatedCaseID) && !string.IsNullOrEmpty(ClueSpecificData.relatedObjectiveID))
                {
                    GameStateManager.Instance.CompleteCaseObjective(ClueSpecificData.relatedCaseID, ClueSpecificData.relatedObjectiveID);
                }
                // Optional: Trigger a character thought when key evidence is found
                // investigationManager.DisplayCharacterThought("This seems important...");
            }
        }
        else if (objectType == ObjectType.Character && CharacterSpecificData != null && CharacterSpecificData.characterProfile != null)
        {
            Debug.Log($"Interacting with character: {CharacterSpecificData.characterProfile.displayName}");
            if (!string.IsNullOrEmpty(CharacterSpecificData.dialogueGraphID) && !string.IsNullOrEmpty(CharacterSpecificData.startingNodeID))
            {
                // Prepare data to pass to the Dialogue State
                // This structure matches what DialogueManager might expect, or can be adapted
                Dictionary<string, object> dialogueStateData = new Dictionary<string, object>
                {
                    { "DialogueGraphID", CharacterSpecificData.dialogueGraphID },
                    { "StartingNodeID", CharacterSpecificData.startingNodeID },
                    // Let DialogueManager know where to return and what data to bring back if needed
                    { "ReturnState", GameStateManager.GameState.Investigation },
                    { "ReturnStateData", investigationManager.currentSceneData } // Pass current scene data so Investigation can resume correctly
                };

                investigationManager.PauseTimer(true); // Pause investigation timer while in dialogue
                GameStateManager.Instance.SwitchState(GameStateManager.GameState.Dialogue, dialogueStateData);
            }
            else
            {
                Debug.LogWarning($"Character '{CharacterSpecificData.characterProfile.displayName}' is missing DialogueGraphID or StartingNodeID in their CharacterPlacementData.");
            }
        }
    }
}