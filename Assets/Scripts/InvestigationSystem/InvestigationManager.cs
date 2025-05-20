using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class InvestigationManager : MonoBehaviour
{
    [Header("Scene Configuration")]
    public InvestigationSceneData currentSceneData;

    [Header("Core UI References")]
    public Image backgroundImageUI;
    public Transform cluesParent;
    public Transform charactersParent;

    [Header("Prefabs")]
    public GameObject cluePrefab;
    public GameObject characterInteractiveObjectPrefab;

    [Header("Police Timer UI")]
    public TextMeshProUGUI policeTimerText;
    public Slider policeTimerBar;
    public Color policeTimerNormalColor = Color.white;
    public Color policeTimerWarningColor = Color.yellow;
    public Color policeTimerCriticalColor = Color.red;
    public float warningThreshold = 0.5f;
    public float criticalThreshold = 0.25f;

    [Header("Clue Info Popup UI")]
    public GameObject clueInfoPopupPanel;
    public TextMeshProUGUI cluePopupNameText;
    public TextMeshProUGUI cluePopupDescriptionText;
    public Image cluePopupImageUI;
    public Button cluePopupContinueButton;

    [Header("Skill Check Display UI")]
    public GameObject skillCheckDisplayPanel;
    public Image skillCheckCircleImage;
    public TextMeshProUGUI skillCheckStaticText;
    public TextMeshProUGUI skillCheckValueText;
    public TextMeshProUGUI skillCheckSkillNameText;
    public Color skillCheckSuccessColor = Color.green;
    public Color skillCheckFailureColor = Color.red;

    [Header("Character Thought UI (Optional)")]
    public GameObject characterThoughtBubblePanel;
    public TextMeshProUGUI characterThoughtText;

    [Header("Case Update Notification UI")]
    public GameObject caseUpdateNotificationPanel;
    public TextMeshProUGUI caseUpdateNotificationText;
    public float caseUpdateDisplayDuration = 2.5f;
    public float caseUpdateAnimationDuration = 0.3f;
    public Vector2 caseUpdateHiddenAnchoredPos = new Vector2(300, 0);
    public Vector2 caseUpdateVisibleAnchoredPos = new Vector2(-10, 0);


    private float currentPoliceTimerValue;
    private float totalPoliceTimerDuration;
    private bool isTimerPaused = false;
    private List<ClueInteractable> activeCluesInScene = new List<ClueInteractable>();
    private List<GameObject> spawnedCharacterObjects = new List<GameObject>();
    private Coroutine characterThoughtCoroutine;
    private Coroutine caseUpdateNotificationCoroutine;
    private RectTransform caseUpdatePanelRectTransform;

    private ClueData currentClueForPopup = null;
    private GameStateManager gameStateManagerInstance;
    private bool m_subscribedToEvents = false; // Flag to manage subscription state


    void Start()
    {
        gameStateManagerInstance = GameStateManager.Instance;
        if (gameStateManagerInstance == null)
        {
            Debug.LogError("InvestigationManager Start: GameStateManager.Instance is null! Cannot subscribe to events or function correctly.");
            enabled = false; // Disable this script if GSM is critical and missing
            return;
        }

        // Subscribe to events here, now that gameStateManagerInstance is confirmed valid
        SubscribeToGameStateEvents();


        InvestigationSceneData dataFromState = gameStateManagerInstance.GetCurrentStateData<InvestigationSceneData>();
        if (dataFromState != null)
        {
            currentSceneData = dataFromState;
        }

        if (currentSceneData == null)
        {
            Debug.LogError("InvestigationManager: No InvestigationSceneData assigned or passed via GameStateManager!");
            enabled = false;
            return;
        }

        InitializeScene();
        InitializeUI();
    }

    void OnEnable()
    {
        // If the object was disabled and re-enabled, and we subscribed in Start and unsubscribed in OnDestroy,
        // we might need to re-subscribe if GameStateManager is still valid.
        // However, for a scene manager that's typically active once per scene load,
        // Start/OnDestroy for subscriptions is often sufficient.
        // If you have scenarios where InvestigationManager is frequently disabled/enabled
        // while GameStateManager persists, you'd manage subscriptions more carefully here and in OnDisable.

        // For now, let's assume Start/OnDestroy handles the primary lifecycle.
        // If needed, re-subscription logic can be added here:
        if (gameStateManagerInstance != null && !m_subscribedToEvents)
        {
             // This implies Start might not have run (e.g. script was disabled initially) or OnDestroy cleared it.
             // Or, more simply, if we choose OnEnable/OnDisable pattern for sub/unsub
            Debug.LogWarning("InvestigationManager OnEnable: Attempting to subscribe to events as it seems Start might not have or was reset.");
            SubscribeToGameStateEvents();
        }
    }

    void SubscribeToGameStateEvents()
    {
        if (m_subscribedToEvents || gameStateManagerInstance == null) return;

        gameStateManagerInstance.OnGameStateChanged.RemoveListener(HandleGameStateChanged); // Always remove first
        gameStateManagerInstance.OnGameStateChanged.AddListener(HandleGameStateChanged);

        gameStateManagerInstance.OnCaseOverallStatusChanged.RemoveListener(HandleCaseStatusUpdate);
        gameStateManagerInstance.OnCaseOverallStatusChanged.AddListener(HandleCaseStatusUpdate);

        gameStateManagerInstance.OnObjectiveStatusChanged.RemoveListener(HandleObjectiveStatusUpdate);
        gameStateManagerInstance.OnObjectiveStatusChanged.AddListener(HandleObjectiveStatusUpdate);

        m_subscribedToEvents = true;
        Debug.Log("InvestigationManager: Successfully subscribed to GameStateManager events.");
    }

    void UnsubscribeFromGameStateEvents()
    {
        if (!m_subscribedToEvents || gameStateManagerInstance == null) return;

        gameStateManagerInstance.OnGameStateChanged.RemoveListener(HandleGameStateChanged);
        gameStateManagerInstance.OnCaseOverallStatusChanged.RemoveListener(HandleCaseStatusUpdate);
        gameStateManagerInstance.OnObjectiveStatusChanged.RemoveListener(HandleObjectiveStatusUpdate);

        m_subscribedToEvents = false;
        Debug.Log("InvestigationManager: Unsubscribed from GameStateManager events.");
    }


    void OnDisable()
    {
        // If you adopt a strict OnEnable/OnDisable pattern for subscriptions for objects
        // that are frequently enabled/disabled, you would unsubscribe here.
        // UnsubscribeFromGameStateEvents(); // Example if using OnEnable/OnDisable for subscriptions

        // Stop coroutines if the object is disabled, regardless of subscription pattern
        if (characterThoughtCoroutine != null) StopCoroutine(characterThoughtCoroutine);
        if (caseUpdateNotificationCoroutine != null) StopCoroutine(caseUpdateNotificationCoroutine);
    }

    void OnDestroy()
    {
        // Unsubscribe from events when this object is destroyed
        UnsubscribeFromGameStateEvents();
        ClearSpawnedObjects();
    }


    void Update()
    {
        HandlePoliceTimer();
    }

    void InitializeScene()
    {
        Debug.Log("InvestigationManager: Initializing scene with data: " + (currentSceneData != null ? currentSceneData.name : "NULL"));
        if (backgroundImageUI != null && currentSceneData != null && currentSceneData.backgroundImage != null)
        {
            backgroundImageUI.sprite = currentSceneData.backgroundImage;
        }
        ClearSpawnedObjects();
        SpawnClues();
        SpawnCharacters();
        SetupPoliceTimer();
        if (currentSceneData != null && !string.IsNullOrEmpty(currentSceneData.entryCharacterThought))
        {
            DisplayCharacterThought(currentSceneData.entryCharacterThought);
        }
        Debug.Log("InvestigationManager: Scene Initialized.");
    }

    void InitializeUI()
    {
        if (clueInfoPopupPanel != null) clueInfoPopupPanel.SetActive(false);
        if (skillCheckDisplayPanel != null) skillCheckDisplayPanel.SetActive(false);
        if (characterThoughtBubblePanel != null) characterThoughtBubblePanel.SetActive(false);

        if (caseUpdateNotificationPanel != null) {
            caseUpdatePanelRectTransform = caseUpdateNotificationPanel.GetComponent<RectTransform>();
            if (caseUpdatePanelRectTransform != null) {
                caseUpdatePanelRectTransform.anchoredPosition = caseUpdateHiddenAnchoredPos;
            } else {
                Debug.LogError("CaseUpdateNotificationPanel is missing a RectTransform component!");
            }
            caseUpdateNotificationPanel.SetActive(false);
        } else {
            Debug.LogWarning("CaseUpdateNotificationPanel is not assigned in the Inspector.");
        }


        if (cluePopupContinueButton != null)
        {
            cluePopupContinueButton.onClick.RemoveAllListeners();
            cluePopupContinueButton.onClick.AddListener(CloseClueInfoPopup);
        }
        if (skillCheckStaticText != null) skillCheckStaticText.text = "Skill Required";
        if (policeTimerText != null) policeTimerText.color = policeTimerNormalColor;
    }

    void SpawnClues()
    {
        if (currentSceneData == null || currentSceneData.cluesInScene == null) { Debug.LogWarning("SpawnClues: No currentSceneData or cluesInScene list."); return; }
        if (cluePrefab == null) { Debug.LogError("SpawnClues: Clue Prefab not assigned!"); return; }
        Transform parentToUse = cluesParent != null ? cluesParent : transform;
        foreach (ClueData clueData in currentSceneData.cluesInScene)
        {
            if (clueData == null) { Debug.LogWarning("SpawnClues: Null ClueData in list."); continue; }
            GameObject clueObjectInstance = Instantiate(cluePrefab, parentToUse);
            clueObjectInstance.name = $"Clue_{(!string.IsNullOrEmpty(clueData.clueID) ? clueData.clueID : clueData.clueName)}";
            clueObjectInstance.transform.localPosition = clueData.worldPosition;
            clueObjectInstance.transform.localScale = new Vector3(clueData.visualScale.x, clueData.visualScale.y, 1f);
            ClueInteractable clueScript = clueObjectInstance.GetComponent<ClueInteractable>();
            if (clueScript != null)
            {
                clueScript.Initialize(clueData, this, gameStateManagerInstance);
                activeCluesInScene.Add(clueScript);
            }
            else
            {
                Debug.LogError($"Clue Prefab '{cluePrefab.name}' missing ClueInteractable script. Destroying instance for clue: {clueData.clueName}.");
                Destroy(clueObjectInstance);
            }
        }
    }

    void SpawnCharacters() { /* Placeholder */ }

    void SetupPoliceTimer() {
        if (currentSceneData == null || gameStateManagerInstance == null) {
            Debug.LogError("Cannot setup police timer - currentSceneData or GameStateManager.Instance is missing.");
            if (policeTimerText != null) policeTimerText.text = "ERR";
            if (policeTimerBar != null) policeTimerBar.gameObject.SetActive(false);
            enabled = false;
            return;
        }
        float baseTime = currentSceneData.basePoliceTimerSeconds;
        float reputationModifier = gameStateManagerInstance.PoliceReputationModifier;
        totalPoliceTimerDuration = baseTime * (1f + reputationModifier);
        totalPoliceTimerDuration = Mathf.Max(0.1f, totalPoliceTimerDuration);
        currentPoliceTimerValue = totalPoliceTimerDuration;
        UpdatePoliceTimerUI();
        PauseTimer(false);
    }

    void HandlePoliceTimer() {
        if (isTimerPaused || currentPoliceTimerValue <= 0) return;
        currentPoliceTimerValue -= Time.deltaTime;
        if (currentPoliceTimerValue <= 0) {
            currentPoliceTimerValue = 0;
            UpdatePoliceTimerUI();
            OnPoliceTimerEnd();
            return;
        }
        UpdatePoliceTimerUI();
    }

    void UpdatePoliceTimerUI() {
        float displayValue = Mathf.Max(0f, currentPoliceTimerValue);
        if (policeTimerText != null) {
            int minutes = Mathf.FloorToInt(displayValue / 60F);
            int seconds = Mathf.FloorToInt(displayValue % 60F);
            policeTimerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
            if (totalPoliceTimerDuration > 0.0001f) {
                float percentageRemaining = displayValue / totalPoliceTimerDuration;
                if (percentageRemaining <= criticalThreshold) policeTimerText.color = policeTimerCriticalColor;
                else if (percentageRemaining <= warningThreshold) policeTimerText.color = policeTimerWarningColor;
                else policeTimerText.color = policeTimerNormalColor;
            } else {
                policeTimerText.color = (displayValue <=0) ? policeTimerCriticalColor : policeTimerNormalColor;
            }
        }
        if (policeTimerBar != null) {
            if (totalPoliceTimerDuration > 0.0001f) policeTimerBar.value = displayValue / totalPoliceTimerDuration;
            else policeTimerBar.value = (displayValue > 0f && totalPoliceTimerDuration > 0.0001f) ? 1f : 0f;
        }
    }

    void OnPoliceTimerEnd() {
        Debug.Log("Police Timer Ended! Player must leave.");
        PauseTimer(true);
        DisplayCharacterThought("The cops are here! I gotta bail!", 5f);
    }

    public void PauseTimer(bool pause) {
        isTimerPaused = pause;
    }

    public void DisplayCharacterThought(string thoughtText, float duration = 3f) {
        if (string.IsNullOrEmpty(thoughtText) || characterThoughtBubblePanel == null || characterThoughtText == null) return;
        if (characterThoughtCoroutine != null) StopCoroutine(characterThoughtCoroutine);
        characterThoughtCoroutine = StartCoroutine(ShowThoughtCoroutine(thoughtText, duration));
    }

    private IEnumerator ShowThoughtCoroutine(string thoughtText, float duration) {
        if (characterThoughtText == null || characterThoughtBubblePanel == null) yield break;
        characterThoughtText.text = thoughtText;
        characterThoughtBubblePanel.SetActive(true);
        bool wasTimerPausedBeforeThought = isTimerPaused;
        if (!wasTimerPausedBeforeThought) PauseTimer(true);

        float elapsedTime = 0f;
        while(elapsedTime < duration) {
            if (Input.GetMouseButtonDown(0) || Input.anyKeyDown) break;
            elapsedTime += Time.unscaledDeltaTime;
            yield return null;
        }
        characterThoughtBubblePanel.SetActive(false);
        if (!wasTimerPausedBeforeThought && currentPoliceTimerValue > 0) PauseTimer(false);
        characterThoughtCoroutine = null;
    }

    public void ShowClueInfoPopup(ClueData clueData, string descriptionToShow, ClueInteractable interactedClueScript = null)
    {
        if (clueInfoPopupPanel == null || clueData == null) return;

        currentClueForPopup = clueData;

        PauseTimer(true);
        SetAllClueCollidersEnabled(false);
        clueInfoPopupPanel.SetActive(true);
        if (cluePopupNameText != null) cluePopupNameText.text = clueData.clueName;
        if (cluePopupDescriptionText != null) cluePopupDescriptionText.text = descriptionToShow;
        if (cluePopupImageUI != null) {
            cluePopupImageUI.sprite = clueData.cluePopupImage;
            cluePopupImageUI.gameObject.SetActive(clueData.cluePopupImage != null);
        }
    }

    void CloseClueInfoPopup()  {
        if (clueInfoPopupPanel != null) clueInfoPopupPanel.SetActive(false);
        SetAllClueCollidersEnabled(true);

        if (currentClueForPopup != null && currentClueForPopup.isKeyEvidence)
        {
            Debug.Log($"Clue '{currentClueForPopup.clueName}' is key evidence. Processing update after popup close.");

            string relatedCaseID = currentClueForPopup.relatedCaseID?.Trim();
            string relatedObjectiveID = currentClueForPopup.relatedObjectiveID?.Trim();
            string caseFlagToSet = currentClueForPopup.caseFlagToSet?.Trim();

            if (!string.IsNullOrEmpty(relatedCaseID) && gameStateManagerInstance != null)
            {
                gameStateManagerInstance.UpdateObjectiveFromClue(relatedCaseID, relatedObjectiveID, caseFlagToSet);
            }
            else if (gameStateManagerInstance == null)
            {
                 Debug.LogError("GameStateManager instance is null in CloseClueInfoPopup. Cannot update objective.");
            }
            else
            {
                Debug.LogWarning($"Clue '{currentClueForPopup.clueName}' is key evidence but has no relatedCaseID set (or it's whitespace).");
            }
        }
        currentClueForPopup = null;

        if (currentPoliceTimerValue > 0 && gameStateManagerInstance != null && gameStateManagerInstance.CurrentGameState == GameStateManager.GameState.Investigation)
        {
            PauseTimer(false);
        }
    }

    private void SetAllClueCollidersEnabled(bool enabledStatus) {
        foreach (ClueInteractable clueScript in activeCluesInScene)
        {
            if (clueScript != null && clueScript.gameObject.activeSelf)
            {
                clueScript.SetColliderEnabled(enabledStatus);
            }
        }
    }

    public void ShowSkillCheckUI(ClueData forClue, bool checkSucceeded) {
        if (skillCheckDisplayPanel == null) { Debug.LogError("SkillCheckDisplayPanel NOT ASSIGNED!"); return; }
        if (forClue == null || !forClue.requiresSkillCheckForMoreInfo) {
            skillCheckDisplayPanel.SetActive(false); return;
        }
        skillCheckDisplayPanel.SetActive(true);
        if (skillCheckDisplayPanel.activeInHierarchy) {
            if (skillCheckValueText != null) skillCheckValueText.text = forClue.skillCheckDC.ToString();
            if (skillCheckSkillNameText != null) skillCheckSkillNameText.text = forClue.skillCheckType.ToString();
            if (skillCheckCircleImage != null) skillCheckCircleImage.color = checkSucceeded ? skillCheckSuccessColor : skillCheckFailureColor;
        } else {
            Debug.LogError($"SkillCheckDisplayPanel FAILED to activate. Check parent hierarchy!");
        }
    }

    public void HideSkillCheckUI() {
        if (skillCheckDisplayPanel != null) skillCheckDisplayPanel.SetActive(false);
    }

    public void UpdateCluePerceptionChecks() {
         if (gameStateManagerInstance == null) return;
        foreach (ClueInteractable clueScript in activeCluesInScene) {
            if (clueScript != null) clueScript.UpdateInteractableState();
        }
    }

    void ClearSpawnedObjects() {
        foreach (ClueInteractable clueScript in activeCluesInScene) if (clueScript != null) Destroy(clueScript.gameObject);
        activeCluesInScene.Clear();
        foreach (GameObject obj in spawnedCharacterObjects) if (obj != null) Destroy(obj);
        spawnedCharacterObjects.Clear();
    }

    private void HandleCaseStatusUpdate(CaseData caseData, CaseOverallStatus oldStatus, CaseOverallStatus newStatus)
    {
        if (caseData == null) return;
        Debug.Log($"HandleCaseStatusUpdate: Case '{caseData.CaseName}' changed from {oldStatus} to {newStatus}");

        if ( (oldStatus == CaseOverallStatus.Inactive && newStatus == CaseOverallStatus.InProgress) ||
             (oldStatus == CaseOverallStatus.InProgress && (newStatus == CaseOverallStatus.Successful || newStatus == CaseOverallStatus.Failed)) )
        {
            ShowCaseUpdateNotification("Case Updated");
        }
    }

    private void HandleObjectiveStatusUpdate(CaseData caseData, CaseObjectiveDefinition objectiveDef, ObjectiveStatus oldStatus, ObjectiveStatus newStatus)
    {
        if (caseData == null || objectiveDef == null) return;
        Debug.Log($"HandleObjectiveStatusUpdate: Objective '{objectiveDef.ObjectiveID}' in Case '{caseData.CaseName}' changed from {oldStatus} to {newStatus}");

        if ( (oldStatus == ObjectiveStatus.Inactive && newStatus == ObjectiveStatus.Active) ||
             (oldStatus == ObjectiveStatus.Active && newStatus == ObjectiveStatus.Completed) )
        {
            ShowCaseUpdateNotification("Case Updated");
        }
    }

    public void ShowCaseUpdateNotification(string message)
    {
        if (caseUpdateNotificationPanel == null || caseUpdatePanelRectTransform == null || caseUpdateNotificationText == null)
        {
            Debug.LogWarning("Case Update Notification UI not fully assigned. Cannot show notification. Message: " + message);
            return;
        }
        Debug.Log("ShowCaseUpdateNotification: " + message);

        if (caseUpdateNotificationCoroutine != null)
        {
            StopCoroutine(caseUpdateNotificationCoroutine);
            caseUpdatePanelRectTransform.anchoredPosition = caseUpdateHiddenAnchoredPos;
            caseUpdateNotificationPanel.SetActive(false);
        }
        caseUpdateNotificationCoroutine = StartCoroutine(AnimateCaseUpdateNotification(message));
    }

    private IEnumerator AnimateCaseUpdateNotification(string message)
    {
        if (caseUpdateNotificationText == null || caseUpdateNotificationPanel == null || caseUpdatePanelRectTransform == null) yield break;

        caseUpdateNotificationText.text = message;
        caseUpdateNotificationPanel.SetActive(true);
        caseUpdatePanelRectTransform.anchoredPosition = caseUpdateHiddenAnchoredPos;

        float elapsedTime = 0f;
        while (elapsedTime < caseUpdateAnimationDuration)
        {
            caseUpdatePanelRectTransform.anchoredPosition = Vector2.Lerp(caseUpdateHiddenAnchoredPos, caseUpdateVisibleAnchoredPos, elapsedTime / caseUpdateAnimationDuration);
            elapsedTime += Time.unscaledDeltaTime;
            yield return null;
        }
        caseUpdatePanelRectTransform.anchoredPosition = caseUpdateVisibleAnchoredPos;

        yield return new WaitForSecondsRealtime(caseUpdateDisplayDuration);

        elapsedTime = 0f;
        while (elapsedTime < caseUpdateAnimationDuration)
        {
            caseUpdatePanelRectTransform.anchoredPosition = Vector2.Lerp(caseUpdateVisibleAnchoredPos, caseUpdateHiddenAnchoredPos, elapsedTime / caseUpdateAnimationDuration);
            elapsedTime += Time.unscaledDeltaTime;
            yield return null;
        }
        caseUpdatePanelRectTransform.anchoredPosition = caseUpdateHiddenAnchoredPos;

        caseUpdateNotificationPanel.SetActive(false);
        caseUpdateNotificationCoroutine = null;
    }


    private void HandleGameStateChanged(GameStateManager.GameState newState, object data) {
        if (this == null || !gameObject.activeInHierarchy || !enabled ) return;
        if (newState == GameStateManager.GameState.Investigation) {
            InvestigationSceneData dataFromState = data as InvestigationSceneData;
            if (dataFromState != null && (currentSceneData == null || currentSceneData.name != dataFromState.name)) {
                currentSceneData = dataFromState; InitializeScene(); InitializeUI();
            } else if (dataFromState != null && currentSceneData == dataFromState) {
                 if (clueInfoPopupPanel != null && clueInfoPopupPanel.activeSelf) CloseClueInfoPopup();
                 if (characterThoughtBubblePanel != null && characterThoughtBubblePanel.activeSelf) characterThoughtBubblePanel.SetActive(false);
                 if (skillCheckDisplayPanel != null && skillCheckDisplayPanel.activeSelf) HideSkillCheckUI();
                 if (currentPoliceTimerValue > 0) PauseTimer(false);
            } else if (data != null && !(data is InvestigationSceneData)) {
                if (currentPoliceTimerValue > 0) PauseTimer(false);
            } else if (data == null && currentSceneData == null) {
                Debug.LogError("Investigation scene loaded but no InvestigationSceneData available!"); enabled = false;
            } else if (data == null && currentSceneData != null) {
                 if (currentPoliceTimerValue > 0) PauseTimer(false);
            }
        } else {
            PauseTimer(true);
            if (skillCheckDisplayPanel != null && skillCheckDisplayPanel.activeSelf) HideSkillCheckUI();
            if (characterThoughtBubblePanel != null && characterThoughtCoroutine != null) {
                StopCoroutine(characterThoughtCoroutine); characterThoughtCoroutine = null; characterThoughtBubblePanel.SetActive(false);
            }
        }
    }
}