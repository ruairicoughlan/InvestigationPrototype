using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic; // For Dictionary

// Make sure ClueColliderType is accessible (e.g., defined in ClueData.cs or its own file)
// public enum ClueColliderType { Box, Polygon } // If not defined elsewhere

public class InteractiveObject : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public enum ObjectType { Clue, Character }

    [Header("Object Configuration")]
    public ObjectType objectType = ObjectType.Clue;

    public ClueData ClueSpecificData { get; private set; }
    public CharacterPlacementData CharacterSpecificData { get; private set; }

    private InvestigationManager investigationManager;
    private BoxCollider2D interactionCollider; // This script primarily manages a BoxCollider2D

    public void InitializeAsClue(ClueData data, InvestigationManager manager)
    {
        objectType = ObjectType.Clue;
        ClueSpecificData = data;
        investigationManager = manager;
        gameObject.name = "InteractiveClue_" + (ClueSpecificData != null ? ClueSpecificData.clueID : "UnnamedClue");

        interactionCollider = GetComponent<BoxCollider2D>();
        if (interactionCollider == null)
        {
            interactionCollider = gameObject.AddComponent<BoxCollider2D>();
        }

        if (ClueSpecificData != null)
        {
            // This script uses its own BoxCollider2D. It will use the ClueData's box settings.
            // If ClueData specifies Polygon, this BoxCollider might not be relevant if another system (like ClueInteractable)
            // is truly handling the PolygonCollider on the same GameObject, or if this prefab is simpler.
            if (ClueSpecificData.colliderType == ClueColliderType.Box)
            {
                interactionCollider.size = ClueSpecificData.boxColliderSize; // Use new field
                interactionCollider.offset = ClueSpecificData.boxColliderOffset; // Use new field

                if (ClueSpecificData.boxColliderSize == Vector2.zero)
                {
                    interactionCollider.size = Vector2.one; // Default if zero
                }
            }
            else // ClueData specifies Polygon
            {
                Debug.LogWarning($"InteractiveObject '{gameObject.name}' initialized as Clue, but ClueData '{ClueSpecificData.clueName}' specifies PolygonCollider. This InteractiveObject will still use its BoxCollider with default settings. Ensure prefab setup is as intended.");
                interactionCollider.size = Vector2.one; 
                interactionCollider.offset = Vector2.zero;
            }
            // Visual scale from ClueData could also be applied here to transform.localScale
            // if InteractiveObject is responsible for the visual representation directly.
            transform.localScale = new Vector3(ClueSpecificData.visualScale.x, ClueSpecificData.visualScale.y, 1f);
        }
        else
        {
            interactionCollider.size = Vector2.one;
            interactionCollider.offset = Vector2.zero;
        }
        interactionCollider.isTrigger = true;
    }

    public void InitializeAsCharacter(CharacterPlacementData data, InvestigationManager manager)
    {
        objectType = ObjectType.Character;
        CharacterSpecificData = data;
        investigationManager = manager;
        gameObject.name = "InteractiveChar_" + (CharacterSpecificData != null && CharacterSpecificData.characterProfile != null ? CharacterSpecificData.characterProfile.characterID : "UnnamedChar");

        interactionCollider = GetComponent<BoxCollider2D>();
        if (interactionCollider == null) interactionCollider = gameObject.AddComponent<BoxCollider2D>();
        
        // TODO: Define how character colliders are sized/offset, possibly from CharacterPlacementData
        interactionCollider.size = new Vector2(100, 200); // Placeholder
        interactionCollider.offset = Vector2.zero;
        interactionCollider.isTrigger = true;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (investigationManager == null || GameStateManager.Instance == null) return;

        if (CursorManager.Instance != null)
        {
            if (objectType == ObjectType.Clue && ClueSpecificData != null)
            {
                // Simplified: Assume if it's a Clue object, it's interactable for cursor purposes.
                // More complex logic would check an 'isInteractable' flag on this InteractiveObject.
                CursorManager.Instance.SetCursorToInteract();
                
                if (ClueSpecificData.requiresSkillCheckForMoreInfo)
                {
                    bool checkSucceeded = GameStateManager.Instance.GetSkillLevel(ClueSpecificData.skillCheckType.ToString()) >= ClueSpecificData.skillCheckDC;
                    investigationManager.ShowSkillCheckUI(ClueSpecificData, checkSucceeded);
                }
            }
            else if (objectType == ObjectType.Character && CharacterSpecificData != null)
            {
                CursorManager.Instance.SetCursorToDialogue();
            }
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (investigationManager == null) return;

        if (CursorManager.Instance != null) CursorManager.Instance.SetCursorToNormal();
        
        if (objectType == ObjectType.Clue && ClueSpecificData != null && ClueSpecificData.requiresSkillCheckForMoreInfo)
        {
            investigationManager.HideSkillCheckUI();
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (investigationManager == null || GameStateManager.Instance == null) return;

        if (objectType == ObjectType.Clue && ClueSpecificData != null)
        {
            if (ClueSpecificData.requiresSkillCheckForMoreInfo) investigationManager.HideSkillCheckUI();

            string descriptionToShow = ClueSpecificData.baseDescription;
            if (ClueSpecificData.requiresSkillCheckForMoreInfo)
            {
                // ... (skill check logic as before) ...
                int playerSkillLevel = GameStateManager.Instance.GetSkillLevel(ClueSpecificData.skillCheckType.ToString());
                if (playerSkillLevel >= ClueSpecificData.skillCheckDC) {
                    descriptionToShow = !string.IsNullOrEmpty(ClueSpecificData.successDescription) ? ClueSpecificData.successDescription : ClueSpecificData.baseDescription;
                } else {
                    descriptionToShow = !string.IsNullOrEmpty(ClueSpecificData.failureDescription) ? ClueSpecificData.failureDescription : ClueSpecificData.baseDescription;
                }
            }
            investigationManager.ShowClueInfoPopup(ClueSpecificData, descriptionToShow);

            if (ClueSpecificData.isKeyEvidence) { /* ... (key evidence logic as before) ... */ }
        }
        else if (objectType == ObjectType.Character && CharacterSpecificData != null && CharacterSpecificData.characterProfile != null)
        {
            // ... (dialogue switching logic as before) ...
             if (!string.IsNullOrEmpty(CharacterSpecificData.dialogueGraphID) && !string.IsNullOrEmpty(CharacterSpecificData.startingNodeID)) {
                Dictionary<string, object> dialogueStateData = new Dictionary<string, object> {
                    { "DialogueGraphID", CharacterSpecificData.dialogueGraphID },
                    { "StartingNodeID", CharacterSpecificData.startingNodeID },
                    { "ReturnState", GameStateManager.GameState.Investigation },
                    { "ReturnStateData", investigationManager.currentSceneData } 
                };
                investigationManager.PauseTimer(true);
                GameStateManager.Instance.SwitchState(GameStateManager.GameState.Dialogue, dialogueStateData);
            }
        }
    }
}