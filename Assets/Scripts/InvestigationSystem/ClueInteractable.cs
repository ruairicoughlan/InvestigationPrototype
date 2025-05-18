using UnityEngine;
using UnityEngine.UI; // Keep if your prefab might use UI elements, though current logic uses SpriteRenderer

public class ClueInteractable : MonoBehaviour
{
    public ClueData clueData; // Reference to the ScriptableObject asset
    
    // Internal state
    private bool isInteractable = false;
    private bool hasBeenInteractedWith = false; // To prevent re-interaction or change behavior after first interaction

    // Cached references
    private InvestigationManager investigationManager;
    private GameStateManager gameStateManager;
    private SpriteRenderer spriteRenderer; // Optional: if you directly manipulate it often
    private BoxCollider2D boxCollider; // Optional: if you directly manipulate it often

    public void Initialize(ClueData data, InvestigationManager invManager, GameStateManager stateManager)
    {
        clueData = data;
        investigationManager = invManager;
        gameStateManager = stateManager;

        // Cache components
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null && clueData.clueWorldSprite != null) // Only add if a world sprite is expected
        {
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            Debug.LogWarning($"Clue '{clueData.clueName}' was missing a SpriteRenderer. One was added. Ensure prefab is set up correctly if this is unexpected.");
        }
        
        boxCollider = GetComponent<BoxCollider2D>();
        if (boxCollider == null)
        {
            boxCollider = gameObject.AddComponent<BoxCollider2D>();
            Debug.LogWarning($"Clue '{clueData.clueName}' was missing a BoxCollider2D. One was added.");
        }

        // Set up the visual representation
        if (spriteRenderer != null) // Check if SpriteRenderer exists
        {
            if (clueData.clueWorldSprite != null)
            {
                spriteRenderer.sprite = clueData.clueWorldSprite;
                spriteRenderer.enabled = true;
            }
            else
            {
                // If no world sprite, it might be an invisible hotspot or part of background.
                // If SpriteRenderer exists but no sprite, disable it.
                spriteRenderer.enabled = false;
            }
        }
        
        // Set up the collider size (must happen after BoxCollider2D is ensured)
        if (clueData.colliderSize != Vector2.zero)
        {
            boxCollider.size = clueData.colliderSize;
        }
        else if (spriteRenderer != null && spriteRenderer.sprite != null && spriteRenderer.enabled)
        {
            // Optional: Default collider size to sprite bounds if not specified and sprite exists
            // This requires the sprite to be set first.
            // For more accurate sizing, it's better to set colliderSize explicitly in ClueData.
            boxCollider.size = spriteRenderer.bounds.size / transform.lossyScale.x; // Adjust for world scale if necessary
            if(boxCollider.size == Vector2.zero) boxCollider.size = new Vector2(1,1); // Fallback if bounds are zero
        }
        else
        {
            boxCollider.size = new Vector2(1,1); // Default if no sprite and no size given, ensure it's clickable
        }
        boxCollider.isTrigger = true; // Usually good for OnMouseDown without physical collision effects

        // Perform initial perception check to determine if interactable
        isInteractable = false; // Default to not interactable
        if (gameStateManager != null && clueData.initialPerceptionSkill != InvestigationSkillType.None && clueData.initialPerceptionDC > 0)
        {
            int playerSkill = gameStateManager.GetSkillLevel(clueData.initialPerceptionSkill.ToString());
            if (playerSkill >= clueData.initialPerceptionDC)
            {
                isInteractable = true;
                Debug.Log($"Clue '{clueData.clueName}' IS interactable (Perception Check: Player {clueData.initialPerceptionSkill} {playerSkill} vs DC {clueData.initialPerceptionDC}).");
            }
            else
            {
                Debug.Log($"Clue '{clueData.clueName}' is NOT interactable (Perception Check: Player {clueData.initialPerceptionSkill} {playerSkill} vs DC {clueData.initialPerceptionDC}). Hiding GameObject.");
            }
        }
        else // No perception check needed (DC 0 or Skill None) or GameStateManager not available for check
        {
            isInteractable = true; // Default to interactable if no check is defined
            if (clueData.initialPerceptionSkill != InvestigationSkillType.None && clueData.initialPerceptionDC > 0 && gameStateManager == null)
            {
                Debug.LogWarning($"GameStateManager not found for perception check on '{clueData.clueName}'. Clue defaults to interactable.");
            }
        }

        // Set GameObject active state based on interactability (from perception)
        // Note: InvestigationManager already sets the initial active state in SpawnClues based on perception.
        // This internal `isInteractable` flag is primarily for governing mouse events and OnMouseDown.
        // If the GameObject was set inactive by InvestigationManager, OnMouseEnter/Exit/Down won't fire anyway.
        // So, the SetActive(false) here is somewhat redundant if InvestigationManager already handles it,
        // but it ensures consistency if this Initialize method were called in a different context.
        if (!isInteractable)
        {
            // If not interactable due to perception, InvestigationManager.SpawnClues should have already handled SetActive(false).
            // If somehow it's still active, this ensures the internal flag matches.
            // We don't want to call SetActive(false) here again if InvestigationManager already did,
            // as this script won't receive OnEnable/Start if it starts inactive.
        }
    }

    // Called by InvestigationManager if skills update, potentially revealing this clue
    public void UpdateInteractableState()
    {
        if (gameObject.activeSelf || clueData == null || gameStateManager == null) // Already active or cannot re-check
        {
            return;
        }

        // Re-check perception
        if (clueData.initialPerceptionSkill != InvestigationSkillType.None && clueData.initialPerceptionDC > 0)
        {
            int playerSkill = gameStateManager.GetSkillLevel(clueData.initialPerceptionSkill.ToString());
            if (playerSkill >= clueData.initialPerceptionDC)
            {
                isInteractable = true;
                gameObject.SetActive(true); // Activate the clue if it passes now
                Debug.Log($"Clue '{clueData.clueName}' became interactable after skill update.");
            }
            // else, it remains inactive and isInteractable remains false
        }
        // If no perception check was needed, it should have been active from the start.
    }

    private bool CheckPlayerSkill(InvestigationSkillType skill, int dc)
    {
        if (gameStateManager == null)
        {
            Debug.LogWarning($"Cannot perform skill check for {skill}; GameStateManager is missing for clue '{clueData.clueName}'. Assuming failure.");
            return false;
        }
        int skillValue = gameStateManager.GetSkillLevel(skill.ToString());
        return skillValue >= dc;
    }

    void OnMouseDown()
    {
        // GameObject must be active and have a collider for OnMouseDown to work.
        // The `isInteractable` flag here is an additional internal check.
        if (!isInteractable || hasBeenInteractedWith || investigationManager == null || clueData == null)
        {
            if (investigationManager == null) Debug.LogError($"InvestigationManager not set on ClueInteractable: {name}");
            return;
        }

        Debug.Log($"Clicked on clue: {clueData.clueName}");
        // hasBeenInteractedWith = true; // Uncomment if clues should only be fully interacted with once

        string descriptionToShow = clueData.baseDescription;
        if (clueData.requiresSkillCheckForMoreInfo)
        {
            bool success = CheckPlayerSkill(clueData.skillCheckType, clueData.skillCheckDC);
            if (success)
            {
                descriptionToShow = !string.IsNullOrEmpty(clueData.successDescription) ? clueData.successDescription : clueData.baseDescription;
                Debug.Log($"Skill check SUCCESS for {clueData.clueName}.");
            }
            else
            {
                descriptionToShow = !string.IsNullOrEmpty(clueData.failureDescription) ? clueData.failureDescription : clueData.baseDescription;
                Debug.Log($"Skill check FAILED for {clueData.clueName}.");
            }
        }

        investigationManager.ShowClueInfoPopup(clueData, descriptionToShow);

        if (clueData.isKeyEvidence && gameStateManager != null)
        {
            Debug.Log($"Clue '{clueData.clueName}' is key evidence.");
            if (!string.IsNullOrEmpty(clueData.relatedCaseID) && !string.IsNullOrEmpty(clueData.caseFlagToSet))
            {
                gameStateManager.SetCaseFlag(clueData.relatedCaseID, clueData.caseFlagToSet, true);
            }
            if (!string.IsNullOrEmpty(clueData.relatedCaseID) && !string.IsNullOrEmpty(clueData.relatedObjectiveID))
            {
                gameStateManager.CompleteCaseObjective(clueData.relatedCaseID, clueData.relatedObjectiveID);
                Debug.Log($"Objective '{clueData.relatedObjectiveID}' in case '{clueData.relatedCaseID}' marked for completion.");
            }
        }
        
        // Optional: if a clue can only be "processed" once for its main effect
        // if (!clueData.allowMultipleInteractions) // Assuming ClueData has such a field
        // {
        //    hasBeenInteractedWith = true;
        //    // Potentially change sprite or disable further detailed interaction
        // }
    }

    void OnMouseEnter()
    {
        // Only change cursor if the clue is active, interactable, and not already "used up" (if that's a mechanic)
        // Note: If the GameObject is inactive (due to failing perception check), OnMouseEnter won't fire.
        // The `isInteractable` flag confirms it passed its perception check *and* is generally interactable.
        if (isInteractable && !hasBeenInteractedWith && CursorManager.Instance != null)
        {
            CursorManager.Instance.SetCursorToInteract();
        }
        // Optional: Add visual highlighting for the clue itself
        // if (isInteractable && !hasBeenInteractedWith && spriteRenderer != null && spriteRenderer.enabled)
        // {
        //     spriteRenderer.color = Color.yellow; // Example highlight
        // }
    }

    void OnMouseExit()
    {
        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.SetCursorToNormal();
        }
        // Optional: Remove visual highlighting
        // if (spriteRenderer != null && spriteRenderer.enabled)
        // {
        //     spriteRenderer.color = Color.white; // Reset to default color
        // }
    }
}