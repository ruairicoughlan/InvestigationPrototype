using UnityEngine;
using UnityEngine.UI; // For potential UI elements like a pop-up
// Add any other necessary namespaces (e.g., for TextMeshPro if you use it for pop-ups)

public class ClueInteractable : MonoBehaviour
{
    public ClueData clueData; // Reference to the ScriptableObject asset
    private bool isInteractable = false;
    private bool hasBeenInteractedWith = false; // To prevent multiple interactions if needed or change behavior

    // References to be set by InvestigationManager or found if needed
    private InvestigationManager investigationManager;
    private GameStateManager gameStateManager;

    // Optional: Reference to a generic Clue UI Pop-up prefab/manager
    // public GameObject cluePopupPrefab;
    // public UIManager uiManager; // If you have a central UI manager

    public void Initialize(ClueData data, InvestigationManager invManager, GameStateManager stateManager)
    {
        clueData = data;
        investigationManager = invManager;
        gameStateManager = stateManager;

        // Set up the visual representation
        if (clueData.clueWorldSprite != null)
        {
            SpriteRenderer sr = GetComponent<SpriteRenderer>();
            if (sr == null) sr = gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = clueData.clueWorldSprite;
            // You might want to adjust sorting order, scale, etc.
        }
        else // If no world sprite, it might be a hotspot on a background. Ensure renderer is off if one exists.
        {
            SpriteRenderer sr = GetComponent<SpriteRenderer>();
            if (sr != null) sr.enabled = false;
        }


        // Set up the collider
        BoxCollider2D bc = GetComponent<BoxCollider2D>();
        if (bc == null) bc = gameObject.AddComponent<BoxCollider2D>();
        if (clueData.colliderSize != Vector2.zero)
        {
            bc.size = clueData.colliderSize;
        }
        else // Default collider size if not specified
        {
            bc.size = Vector2.one; // Or based on sprite bounds if sprite exists
        }
        bc.isTrigger = true; // Usually good for click interactions if using OnMouseDown or physics raycasts

        // Perform initial perception check
        if (gameStateManager != null && clueData.initialPerceptionSkill != InvestigationSkillType.None && clueData.initialPerceptionDC > 0)
        {
            int playerSkill = gameStateManager.GetSkillLevel(clueData.initialPerceptionSkill.ToString());
            isInteractable = playerSkill >= clueData.initialPerceptionDC;
            if (!isInteractable)
            {
                Debug.Log($"Clue '{clueData.clueName}' is NOT interactable due to failed perception check (Player {clueData.initialPerceptionSkill}: {playerSkill} vs DC: {clueData.initialPerceptionDC}). Hiding.");
                gameObject.SetActive(false);
            }
            else
            {
                 Debug.Log($"Clue '{clueData.clueName}' IS interactable due to passed perception check (Player {clueData.initialPerceptionSkill}: {playerSkill} vs DC: {clueData.initialPerceptionDC}).");
            }
        }
        else // No perception check needed or GameStateManager not available
        {
            isInteractable = true;
            if (clueData.initialPerceptionSkill != InvestigationSkillType.None && clueData.initialPerceptionDC > 0 && gameStateManager == null)
            {
                Debug.LogWarning($"GameStateManager not found for perception check on {clueData.clueName}. Clue will be interactable by default.");
            }
        }

        if (!gameObject.activeSelf && isInteractable) // Ensure it's active if it should be
        {
            gameObject.SetActive(true);
        }
    }

    // This method would be called if you implement a way for player skills to update and reveal clues
    public void UpdateInteractableState()
    {
        if (gameStateManager == null || clueData == null || gameObject.activeSelf) return; // Already active or cannot check

        if (clueData.initialPerceptionSkill != InvestigationSkillType.None && clueData.initialPerceptionDC > 0)
        {
            int playerSkill = gameStateManager.GetSkillLevel(clueData.initialPerceptionSkill.ToString());
            bool canNowSee = playerSkill >= clueData.initialPerceptionDC;

            if (canNowSee)
            {
                gameObject.SetActive(true);
                isInteractable = true;
                Debug.Log($"Clue '{clueData.clueName}' is NOW interactable after skill update.");
                // Optional: GetComponent<SpriteRenderer>().color = Color.white; // Restore full visibility if changed
            }
        }
    }

    private bool CheckPlayerSkill(InvestigationSkillType skill, int dc)
    {
        if (gameStateManager == null)
        {
            Debug.LogWarning($"Cannot perform skill check for {skill}; GameStateManager is missing.");
            return false; // Or true, depending on desired fallback behavior
        }
        int skillValue = gameStateManager.GetSkillLevel(skill.ToString());
        return skillValue >= dc;
    }

    // Using OnMouseDown requires a Collider on this GameObject and a Camera with a PhysicsRaycaster (or Physics2DRaycaster for 2D colliders)
    // If your setup uses Unity's EventSystem more directly (e.g. for UI elements or worldspace UI),
    // you might prefer IPointerClickHandler. For now, OnMouseDown is simpler for basic world objects.
    void OnMouseDown()
    {
        if (!isInteractable || hasBeenInteractedWith || investigationManager == null)
        {
            if(investigationManager == null) Debug.LogError("InvestigationManager not set on ClueInteractable!");
            return;
        }

        Debug.Log($"Clicked on clue: {clueData.clueName}");
        // hasBeenInteractedWith = true; // Uncomment if clues should only be interacted with once

        // 1. Prepare description based on skill check for more info (if required)
        string descriptionToShow = clueData.baseDescription;
        if (clueData.requiresSkillCheckForMoreInfo)
        {
            bool success = CheckPlayerSkill(clueData.skillCheckType, clueData.skillCheckDC);
            if (success)
            {
                descriptionToShow = !string.IsNullOrEmpty(clueData.successDescription) ? clueData.successDescription : clueData.baseDescription;
                Debug.Log($"Skill check SUCCESS for {clueData.clueName}: {descriptionToShow}");
            }
            else
            {
                descriptionToShow = !string.IsNullOrEmpty(clueData.failureDescription) ? clueData.failureDescription : clueData.baseDescription;
                Debug.Log($"Skill check FAILED for {clueData.clueName}: {descriptionToShow}");
            }
        }

        // 2. Tell InvestigationManager to show the pop-up
        investigationManager.ShowClueInfoPopup(clueData, descriptionToShow);


        // 3. Update case file/game state if it's key evidence
        if (clueData.isKeyEvidence && gameStateManager != null)
        {
            Debug.Log($"Clue '{clueData.clueName}' is key evidence.");
            if(!string.IsNullOrEmpty(clueData.relatedCaseID) && !string.IsNullOrEmpty(clueData.caseFlagToSet))
            {
                gameStateManager.SetCaseFlag(clueData.relatedCaseID, clueData.caseFlagToSet, true);
            }
            if(!string.IsNullOrEmpty(clueData.relatedCaseID) && !string.IsNullOrEmpty(clueData.relatedObjectiveID))
            {
                // Assuming a method like this exists or will be created in GameStateManager
                // gameStateManager.CompleteObjective(clueData.relatedCaseID, clueData.relatedObjectiveID);
                 Debug.Log($"Objective completion for {clueData.relatedObjectiveID} in case {clueData.relatedCaseID} needs to be handled by GameStateManager.");
            }
        }

        // Potentially make non-interactable after first interaction, or change sprite, etc.
        // isInteractable = false;
        // SpriteRenderer sr = GetComponent<SpriteRenderer>();
        // if (sr != null) sr.color = Color.gray; // Example: grey out after interaction
    }

    void OnMouseEnter()
    {
        if (isInteractable)
        {
            // Optional: Highlight or change cursor
            // transform.localScale *= 1.1f; // Example hover effect
            if (investigationManager != null)
            {
                // investigationManager.ShowMouseTooltip($"Inspect {clueData.clueName}");
            }
        }
    }

    void OnMouseExit()
    {
        // Optional: Remove highlight or reset cursor
        // transform.localScale /= 1.1f; // Reset hover effect
        if (investigationManager != null)
        {
           // investigationManager.HideMouseTooltip();
        }
    }
}
