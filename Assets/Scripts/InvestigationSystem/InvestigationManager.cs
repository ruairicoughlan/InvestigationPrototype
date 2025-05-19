using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class InvestigationManager : MonoBehaviour
{
    [Header("Scene Configuration")]
    [Tooltip("Assign the InvestigationSceneData ScriptableObject for the current scene here in the Inspector.")]
    public InvestigationSceneData currentSceneData;

    [Header("Core UI References")]
    [Tooltip("Drag the UI Image component for the panoramic background here.")]
    public Image backgroundImageUI;

    [Tooltip("Drag an empty GameObject here to act as a parent for instantiated clue visuals/hotspots.")]
    public Transform cluesParent;

    [Tooltip("Drag an empty GameObject here to act as a parent for instantiated character visuals.")]
    public Transform charactersParent;

    [Header("Prefabs")]
    [Tooltip("Prefab that has the ClueInteractable script attached.")]
    public GameObject cluePrefab; 

    [Tooltip("Prefab that has the InteractiveObject script attached, used for characters (or a generic interactable).")]
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

    // Internal State
    private float currentPoliceTimerValue;
    private float totalPoliceTimerDuration;
    private bool isTimerPaused = false;
    private List<ClueInteractable> activeCluesInScene = new List<ClueInteractable>();
    private List<GameObject> spawnedCharacterObjects = new List<GameObject>();
    private List<GameObject> spawnedWitnessPopups = new List<GameObject>();
    private Coroutine characterThoughtCoroutine;

    void Start()
    {
        if (GameStateManager.Instance == null)
        {
            Debug.LogError("InvestigationManager: GameStateManager not found!");
            enabled = false;
            return;
        }

        InvestigationSceneData dataFromState = GameStateManager.Instance.GetCurrentStateData<InvestigationSceneData>();
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

        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnGameStateChanged.RemoveListener(HandleGameStateChanged);
            GameStateManager.Instance.OnGameStateChanged.AddListener(HandleGameStateChanged);
        }
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
        // ... (other background UI null checks)

        ClearSpawnedObjects();
        SpawnClues(); 
        SpawnCharacters(); 
        SpawnWitnessPopups(); 
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
        if (currentSceneData == null || currentSceneData.cluesInScene == null) return;
        if (cluePrefab == null) { Debug.LogError("Clue Prefab not assigned!"); return; }
        
        Transform parentToUse = cluesParent != null ? cluesParent : transform;

        Debug.Log($"Spawning {currentSceneData.cluesInScene.Count} clues for scene '{currentSceneData.name}'.");
        foreach (ClueData clueData in currentSceneData.cluesInScene)
        {
            if (clueData == null) { Debug.LogWarning("Null ClueData in list."); continue; }

            GameObject clueObjectInstance = Instantiate(cluePrefab, parentToUse);
            clueObjectInstance.name = $"Clue_{(!string.IsNullOrEmpty(clueData.clueID) ? clueData.clueID : clueData.clueName)}";
            
            clueObjectInstance.transform.localPosition = clueData.worldPosition;
            clueObjectInstance.transform.localScale = new Vector3(clueData.visualScale.x, clueData.visualScale.y, 1f); // Apply visual scale

            ClueInteractable clueScript = clueObjectInstance.GetComponent<ClueInteractable>();
            if (clueScript != null)
            {
                clueScript.Initialize(clueData, this, GameStateManager.Instance);
                
                bool passesInitialPerception = true; 
                if (clueData.initialPerceptionSkill != InvestigationSkillType.None && clueData.initialPerceptionDC > 0 && GameStateManager.Instance != null)
                {
                    passesInitialPerception = GameStateManager.Instance.GetSkillLevel(clueData.initialPerceptionSkill.ToString()) >= clueData.initialPerceptionDC;
                }
                clueObjectInstance.SetActive(passesInitialPerception);

                if(passesInitialPerception) activeCluesInScene.Add(clueScript);
                else Debug.Log($"Clue '{clueData.clueName}' initially inactive (Perception).");
            }
            else
            {
                Debug.LogError($"Clue Prefab '{cluePrefab.name}' missing ClueInteractable script. Destroying instance for clue: {clueData.clueName}.");
                Destroy(clueObjectInstance);
            }
        }
        Debug.Log("Clue spawning complete. Active clues in list: " + activeCluesInScene.Count);
    }

    void SpawnCharacters() {/* ... (placeholder) ... */}
    void SpawnWitnessPopups() {/* ... (placeholder) ... */}
    void SetupPoliceTimer() {/* ... (full logic as before) ... */
        if (currentSceneData == null || GameStateManager.Instance == null) {
            Debug.LogError("Cannot setup police timer - currentSceneData or GameStateManager.Instance is missing.");
            if (policeTimerText != null) policeTimerText.text = "ERR";
            if (policeTimerBar != null) policeTimerBar.gameObject.SetActive(false);
            enabled = false; 
            return;
        }
        float baseTime = currentSceneData.basePoliceTimerSeconds; 
        float reputationModifier = GameStateManager.Instance.PoliceReputationModifier;
        totalPoliceTimerDuration = baseTime * (1f + reputationModifier);
        totalPoliceTimerDuration = Mathf.Max(0.1f, totalPoliceTimerDuration); 
        currentPoliceTimerValue = totalPoliceTimerDuration;
        UpdatePoliceTimerUI();
        PauseTimer(false); 
    }
    void HandlePoliceTimer() {/* ... (full logic as before) ... */
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
    void UpdatePoliceTimerUI() {/* ... (full logic as before) ... */
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
    void OnPoliceTimerEnd() {/* ... (full logic as before) ... */
        Debug.Log("Police Timer Ended! Player must leave.");
        PauseTimer(true); 
        DisplayCharacterThought("The cops are here! I gotta bail!", 5f);
    }
    public void PauseTimer(bool pause) {/* ... (full logic as before) ... */
        isTimerPaused = pause;
        Debug.Log($"Timer Paused: {isTimerPaused}");
    }
    public void DisplayCharacterThought(string thoughtText, float duration = 3f) {/* ... (full logic as before, ensure null checks) ... */
        if (string.IsNullOrEmpty(thoughtText) || characterThoughtBubblePanel == null || characterThoughtText == null) return;
        if (characterThoughtCoroutine != null) StopCoroutine(characterThoughtCoroutine);
        characterThoughtCoroutine = StartCoroutine(ShowThoughtCoroutine(thoughtText, duration));
    }
    private IEnumerator ShowThoughtCoroutine(string thoughtText, float duration) {/* ... (full logic as before, ensure null checks & timer handling) ... */
        if (characterThoughtText == null || characterThoughtBubblePanel == null) yield break; 
        characterThoughtText.text = thoughtText;
        characterThoughtBubblePanel.SetActive(true);
        bool wasTimerPausedBeforeThought = isTimerPaused;
        PauseTimer(true);
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

    public void ShowClueInfoPopup(ClueData clueData, string descriptionToShow) {/* ... (full logic as before, ensure null checks) ... */
        if (clueInfoPopupPanel == null || clueData == null) return;
        PauseTimer(true);
        clueInfoPopupPanel.SetActive(true);
        if (cluePopupNameText != null) cluePopupNameText.text = clueData.clueName;
        if (cluePopupDescriptionText != null) cluePopupDescriptionText.text = descriptionToShow;
        if (cluePopupImageUI != null) {
            cluePopupImageUI.sprite = clueData.cluePopupImage;
            cluePopupImageUI.gameObject.SetActive(clueData.cluePopupImage != null);
        }
    }
    void CloseClueInfoPopup()  {/* ... (full logic as before) ... */
        if (clueInfoPopupPanel != null) clueInfoPopupPanel.SetActive(false);
        if (currentPoliceTimerValue > 0) PauseTimer(false);
    }

    public void ShowSkillCheckUI(ClueData forClue, bool checkSucceeded) {/* ... (full logic as before, with detailed logs) ... */
        Debug.Log($"ShowSkillCheckUI called for: {(forClue != null ? forClue.clueName : "NULL ClueData")}, Player Succeeded Pre-Check: {checkSucceeded}");
        if (skillCheckDisplayPanel == null) { Debug.LogError("SkillCheckDisplayPanel NOT ASSIGNED!"); return; }
        if (forClue == null || !forClue.requiresSkillCheckForMoreInfo) {
            skillCheckDisplayPanel.SetActive(false); return;
        }
        Debug.Log($"SkillCheckDisplayPanel: Before SetActive(true) - local: {skillCheckDisplayPanel.activeSelf}, world: {skillCheckDisplayPanel.activeInHierarchy}");
        skillCheckDisplayPanel.SetActive(true);
        Debug.Log($"SkillCheckDisplayPanel: After SetActive(true) - local: {skillCheckDisplayPanel.activeSelf}, world: {skillCheckDisplayPanel.activeInHierarchy}");
        if (skillCheckDisplayPanel.activeInHierarchy) {
            if (skillCheckValueText != null) skillCheckValueText.text = forClue.skillCheckDC.ToString();
            if (skillCheckSkillNameText != null) skillCheckSkillNameText.text = forClue.skillCheckType.ToString();
            if (skillCheckCircleImage != null) skillCheckCircleImage.color = checkSucceeded ? skillCheckSuccessColor : skillCheckFailureColor;
            Debug.Log($"Successfully displayed Skill Check UI for '{forClue.clueName}'");
        } else {
            Debug.LogError($"SkillCheckDisplayPanel FAILED to activate. Check parent hierarchy!");
        }
    }
    public void HideSkillCheckUI() {/* ... (full logic as before) ... */
        if (skillCheckDisplayPanel != null) skillCheckDisplayPanel.SetActive(false);
    }
    public void UpdateCluePerceptionChecks() {/* ... (full logic as before) ... */
        if (GameStateManager.Instance == null) return;
        foreach (ClueInteractable clueScript in activeCluesInScene) {
            if (clueScript != null && !clueScript.gameObject.activeSelf) clueScript.UpdateInteractableState(); 
        }
    }
    void ClearSpawnedObjects() {/* ... (full logic as before, ensuring correct list types) ... */
        foreach (ClueInteractable clueScript in activeCluesInScene) if (clueScript != null) Destroy(clueScript.gameObject);
        activeCluesInScene.Clear();
        foreach (GameObject obj in spawnedCharacterObjects) if (obj != null) Destroy(obj);
        spawnedCharacterObjects.Clear();
        foreach (GameObject obj in spawnedWitnessPopups) if (obj != null) Destroy(obj);
        spawnedWitnessPopups.Clear();
        Debug.Log("All spawned objects cleared.");
    }
    public void OnReturnedFromDialogue(object dialogueOutcomeData) {/* ... (full logic as before) ... */
        if (currentPoliceTimerValue > 0) PauseTimer(false);
    }
    void OnEnable()  {/* ... (full logic as before with Add/RemoveListener) ... */
        if (GameStateManager.Instance != null) {
            GameStateManager.Instance.OnGameStateChanged.RemoveListener(HandleGameStateChanged);
            GameStateManager.Instance.OnGameStateChanged.AddListener(HandleGameStateChanged);
        }
    }
    void OnDisable() {/* ... (full logic as before with RemoveListener) ... */
         if (GameStateManager.Instance != null) GameStateManager.Instance.OnGameStateChanged.RemoveListener(HandleGameStateChanged);
        if (characterThoughtCoroutine != null) StopCoroutine(characterThoughtCoroutine);
    }
    void OnDestroy()  {/* ... (full logic as before with RemoveListener) ... */
        if (GameStateManager.Instance != null) GameStateManager.Instance.OnGameStateChanged.RemoveListener(HandleGameStateChanged);
        ClearSpawnedObjects();
    }
    private void HandleGameStateChanged(GameStateManager.GameState newState, object data) {/* ... (full logic as before, with careful checks for data and currentSceneData) ... */
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
                OnReturnedFromDialogue(data);
            } else if (data == null && currentSceneData == null) {
                Debug.LogError("Investigation scene loaded but no InvestigationSceneData available!"); enabled = false; 
            } else if (data == null && currentSceneData != null) {
                 if (currentPoliceTimerValue > 0) PauseTimer(false);
            }
        } else {
            PauseTimer(true);
            if (clueInfoPopupPanel != null && clueInfoPopupPanel.activeSelf) CloseClueInfoPopup(); 
            if (skillCheckDisplayPanel != null && skillCheckDisplayPanel.activeSelf) HideSkillCheckUI();
            if (characterThoughtBubblePanel != null && characterThoughtCoroutine != null) {
                StopCoroutine(characterThoughtCoroutine); characterThoughtCoroutine = null; characterThoughtBubblePanel.SetActive(false);
            }
        }
    }
}