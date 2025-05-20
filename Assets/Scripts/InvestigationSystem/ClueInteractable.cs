using UnityEngine;
// using UnityEngine.UI; // Only if ClueInteractable itself directly manipulates UI Images.

public class ClueInteractable : MonoBehaviour
{
    public ClueData clueData;

    private bool isInteractable = false;
    // private bool hasBeenInteractedWith = false; // Can be re-added if you need to prevent re-interaction with certain non-key clues

    private InvestigationManager investigationManager;
    private GameStateManager gameStateManager;
    private SpriteRenderer spriteRenderer;
    private BoxCollider2D boxCollider;
    private PolygonCollider2D polygonCollider;
    private Collider2D activeCollider;

    public void Initialize(ClueData data, InvestigationManager invManager, GameStateManager stateManager)
    {
        clueData = data;
        investigationManager = invManager;
        gameStateManager = stateManager;

        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null && clueData != null && clueData.clueWorldSprite != null)
        {
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        }

        boxCollider = GetComponent<BoxCollider2D>();
        if (boxCollider == null) boxCollider = gameObject.AddComponent<BoxCollider2D>();

        polygonCollider = GetComponent<PolygonCollider2D>();
        if (polygonCollider == null) polygonCollider = gameObject.AddComponent<PolygonCollider2D>();

        if (clueData == null)
        {
            Debug.LogError($"ClueInteractable on {gameObject.name} initialized with null ClueData. Disabling.");
            isInteractable = false;
            gameObject.SetActive(false);
            if(spriteRenderer != null) spriteRenderer.enabled = false;
            if(boxCollider != null) boxCollider.enabled = false;
            if(polygonCollider != null) polygonCollider.enabled = false;
            return;
        }

        if (spriteRenderer != null)
        {
            if (clueData.clueWorldSprite != null)
            {
                spriteRenderer.sprite = clueData.clueWorldSprite;
                spriteRenderer.enabled = true;
            }
            else
            {
                spriteRenderer.enabled = false;
            }
        }

        if (clueData.colliderType == ClueColliderType.Box)
        {
            boxCollider.enabled = true;
            polygonCollider.enabled = false;
            activeCollider = boxCollider;
            boxCollider.size = clueData.boxColliderSize == Vector2.zero ? new Vector2(100,100) : clueData.boxColliderSize;
            boxCollider.offset = clueData.boxColliderOffset;
            boxCollider.isTrigger = true;
        }
        else
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
                polygonCollider.points = new Vector2[] {
                    new Vector2(-50f, -50f), new Vector2(50f, -50f), new Vector2(50f, 50f), new Vector2(-50f, 50f)
                };
                Debug.LogWarning($"Clue '{clueData.clueName}': PolygonCollider points invalid or not set. Using default square.");
            }
            polygonCollider.isTrigger = true;
        }
        UpdateInteractableState();
    }

    public void UpdateInteractableState()
    {
        if (clueData == null || gameStateManager == null) {
            isInteractable = false;
            if (gameObject.activeSelf) gameObject.SetActive(false);
            if (activeCollider != null) activeCollider.enabled = false;
            return;
        }

        bool perceptionPassed = true;
        if (clueData.initialPerceptionSkill != InvestigationSkillType.None && clueData.initialPerceptionDC > 0)
        {
            int playerSkill = gameStateManager.GetSkillLevel(clueData.initialPerceptionSkill.ToString());
            perceptionPassed = playerSkill >= clueData.initialPerceptionDC;
        }

        isInteractable = perceptionPassed;
        gameObject.SetActive(isInteractable);
        if (activeCollider != null)
        {
            activeCollider.enabled = isInteractable;
        }
    }

    public void SetColliderEnabled(bool enabledStatus)
    {
        if (activeCollider != null)
        {
            activeCollider.enabled = enabledStatus;
        }
    }

    private bool CheckPlayerSkill(InvestigationSkillType skill, int dc)
    {
        if (gameStateManager == null) return false;
        int skillValue = gameStateManager.GetSkillLevel(skill.ToString());
        bool result = skillValue >= dc;
        return result;
    }

    void OnMouseDown()
    {
        if (activeCollider == null || !activeCollider.enabled) return;
        if (!isInteractable || investigationManager == null || clueData == null || gameStateManager == null)
        {
            return;
        }

        Debug.Log($"Clicked on clue: {clueData.clueName}");

        string descriptionToShow = clueData.baseDescription;
        if (clueData.requiresSkillCheckForMoreInfo)
        {
            bool success = CheckPlayerSkill(clueData.skillCheckType, clueData.skillCheckDC);
            descriptionToShow = success ?
                                (!string.IsNullOrEmpty(clueData.successDescription) ? clueData.successDescription : clueData.baseDescription) :
                                (!string.IsNullOrEmpty(clueData.failureDescription) ? clueData.failureDescription : clueData.baseDescription);
        }

        // Pass 'this' ClueInteractable (which contains ClueData) to the InvestigationManager
        investigationManager.ShowClueInfoPopup(clueData, descriptionToShow, this); // MODIFIED: Pass 'this' or just 'clueData'
                                                                                 // We'll use clueData directly in InvManager
        if (clueData.requiresSkillCheckForMoreInfo) investigationManager.HideSkillCheckUI();

        // Case update logic is now handled in InvestigationManager.CloseClueInfoPopup()
    }

    void OnMouseEnter()
    {
        if (activeCollider == null || !activeCollider.enabled) return;
        if (!isInteractable) return; // Removed hasBeenInteractedWith here for consistency, re-add if needed

        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.SetCursorToInteract();
        }

        if (clueData != null && clueData.requiresSkillCheckForMoreInfo &&
            investigationManager != null && gameStateManager != null)
        {
            bool success = CheckPlayerSkill(clueData.skillCheckType, clueData.skillCheckDC);
            investigationManager.ShowSkillCheckUI(clueData, success);
        }
    }

    void OnMouseExit()
    {
        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.SetCursorToNormal();
        }

        if (investigationManager != null && clueData != null && clueData.requiresSkillCheckForMoreInfo)
        {
            investigationManager.HideSkillCheckUI();
        }
    }
}