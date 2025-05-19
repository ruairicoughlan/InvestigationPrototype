using UnityEngine;
// using UnityEngine.UI; // Only if ClueInteractable itself directly manipulates UI Images.

public class ClueInteractable : MonoBehaviour
{
    public ClueData clueData;

    private bool isInteractable = false;
    private bool hasBeenInteractedWith = false; // For preventing re-interaction or altering hover states

    private InvestigationManager investigationManager;
    private GameStateManager gameStateManager;
    private SpriteRenderer spriteRenderer;
    private BoxCollider2D boxCollider;
    private PolygonCollider2D polygonCollider;
    private Collider2D activeCollider; // Tracks the currently used collider

    public void Initialize(ClueData data, InvestigationManager invManager, GameStateManager stateManager)
    {
        clueData = data;
        investigationManager = invManager;
        gameStateManager = stateManager;

        // --- Component Setup ---
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null && clueData.clueWorldSprite != null)
        {
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        }
        
        boxCollider = GetComponent<BoxCollider2D>();
        if (boxCollider == null) boxCollider = gameObject.AddComponent<BoxCollider2D>();
        
        polygonCollider = GetComponent<PolygonCollider2D>();
        if (polygonCollider == null) polygonCollider = gameObject.AddComponent<PolygonCollider2D>();

        // --- Visual Setup ---
        if (spriteRenderer != null)
        {
            if (clueData.clueWorldSprite != null)
            {
                spriteRenderer.sprite = clueData.clueWorldSprite;
                spriteRenderer.enabled = true;
            }
            else
            {
                spriteRenderer.enabled = false; // Invisible clue if no world sprite
            }
        }

        // --- Collider Setup based on ClueData ---
        if (clueData.colliderType == ClueColliderType.Box)
        {
            boxCollider.enabled = true;
            polygonCollider.enabled = false;
            activeCollider = boxCollider;

            boxCollider.size = clueData.boxColliderSize;
            boxCollider.offset = clueData.boxColliderOffset;
            boxCollider.isTrigger = true;
        }
        else // Polygon
        {
            boxCollider.enabled = false;
            polygonCollider.enabled = true;
            activeCollider = polygonCollider;

            if (clueData.polygonColliderPoints != null && clueData.polygonColliderPoints.Length >= 3)
            {
                polygonCollider.points = clueData.polygonColliderPoints;
            }
            else
            {
                polygonCollider.points = new Vector2[] { // Default square
                    new Vector2(-50f, -50f), new Vector2(50f, -50f), new Vector2(50f, 50f), new Vector2(-50f, 50f)
                };
                Debug.LogWarning($"Clue '{clueData.clueName}': PolygonCollider points invalid. Using default.");
            }
            polygonCollider.isTrigger = true;
        }

        // --- Perception Check & Interactable State ---
        // Note: InvestigationManager also does an initial SetActive() based on perception.
        // This 'isInteractable' flag is for this script's logic.
        isInteractable = false; 
        if (gameStateManager != null && clueData.initialPerceptionSkill != InvestigationSkillType.None && clueData.initialPerceptionDC > 0)
        {
            int playerSkill = gameStateManager.GetSkillLevel(clueData.initialPerceptionSkill.ToString());
            isInteractable = playerSkill >= clueData.initialPerceptionDC;
            // Debug.Log($"Clue '{clueData.clueName}' Perception: PlayerSkill={playerSkill}, DC={clueData.initialPerceptionDC}, Interactable={isInteractable}");
        }
        else 
        {
            isInteractable = true; 
            // Debug.Log($"Clue '{clueData.clueName}' Interactable (no perception check or GSM missing).");
        }
    }

    public void UpdateInteractableState() // Called if player skills change
    {
        if (gameObject.activeSelf || clueData == null || gameStateManager == null) return;
        
        if (clueData.initialPerceptionSkill != InvestigationSkillType.None && clueData.initialPerceptionDC > 0)
        {
            int playerSkill = gameStateManager.GetSkillLevel(clueData.initialPerceptionSkill.ToString());
            if (playerSkill >= clueData.initialPerceptionDC)
            {
                isInteractable = true;
                gameObject.SetActive(true);
                Debug.Log($"Clue '{clueData.clueName}' became interactable after skill update.");
            }
        }
    }

    private bool CheckPlayerSkill(InvestigationSkillType skill, int dc)
    {
        if (gameStateManager == null) return false;
        int skillValue = gameStateManager.GetSkillLevel(skill.ToString());
        bool result = skillValue >= dc;
        Debug.Log($"CheckPlayerSkill for '{clueData.clueName}': Skill={skill}, PlayerLevel={skillValue}, DC={dc}, Result={result}");
        return result;
    }

    void OnMouseDown()
    {
        if (!isInteractable || hasBeenInteractedWith || investigationManager == null || clueData == null) return;

        Debug.Log($"Clicked on clue: {clueData.clueName}");
        
        string descriptionToShow = clueData.baseDescription;
        if (clueData.requiresSkillCheckForMoreInfo)
        {
            bool success = CheckPlayerSkill(clueData.skillCheckType, clueData.skillCheckDC);
            if (success)
            {
                descriptionToShow = !string.IsNullOrEmpty(clueData.successDescription) ? clueData.successDescription : clueData.baseDescription;
            }
            else
            {
                descriptionToShow = !string.IsNullOrEmpty(clueData.failureDescription) ? clueData.failureDescription : clueData.baseDescription;
            }
        }

        investigationManager.ShowClueInfoPopup(clueData, descriptionToShow);
        if (clueData.requiresSkillCheckForMoreInfo) investigationManager.HideSkillCheckUI(); // Hide hover UI

        if (clueData.isKeyEvidence && gameStateManager != null)
        {
            // ... (key evidence logic as before) ...
            if (!string.IsNullOrEmpty(clueData.relatedCaseID) && !string.IsNullOrEmpty(clueData.caseFlagToSet))
            {
                gameStateManager.SetCaseFlag(clueData.relatedCaseID, clueData.caseFlagToSet, true);
            }
            if(!string.IsNullOrEmpty(clueData.relatedCaseID) && !string.IsNullOrEmpty(clueData.relatedObjectiveID))
            {
                gameStateManager.CompleteCaseObjective(clueData.relatedCaseID, clueData.relatedObjectiveID);
            }
        }
        // hasBeenInteractedWith = true; // If clues can only be fully processed once
    }

    void OnMouseEnter()
    {
        Debug.Log($"[[[ MOUSE ENTERED {gameObject.name} ]]] --- isInteractable: {isInteractable}, requiresSkillCheck: {clueData?.requiresSkillCheckForMoreInfo}, hasBeenInteracted: {hasBeenInteractedWith}");

        if (isInteractable && !hasBeenInteractedWith && CursorManager.Instance != null)
        {
            CursorManager.Instance.SetCursorToInteract();
        }

        if (isInteractable && !hasBeenInteractedWith && 
            clueData != null && clueData.requiresSkillCheckForMoreInfo && 
            investigationManager != null)
        {
            Debug.Log($"[[[ CONDITIONS MET in OnMouseEnter - Attempting to show Skill Check UI for {clueData.clueName} ]]]");
            bool success = CheckPlayerSkill(clueData.skillCheckType, clueData.skillCheckDC);
            investigationManager.ShowSkillCheckUI(clueData, success);
        }
    }

    void OnMouseExit()
    {
        Debug.Log($"[[[ MOUSE EXITED {gameObject.name} ]]]");
        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.SetCursorToNormal();
        }

        if (investigationManager != null && clueData != null && clueData.requiresSkillCheckForMoreInfo) 
        {
            Debug.Log($"[[[ Attempting to hide Skill Check UI for {clueData.clueName} from OnMouseExit ]]]");
            investigationManager.HideSkillCheckUI();
        }
    }
}