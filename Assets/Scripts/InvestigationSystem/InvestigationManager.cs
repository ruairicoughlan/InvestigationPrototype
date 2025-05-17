using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections; // Ensure this is here for IEnumerator
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

    [Header("Police Timer UI")]
    [Tooltip("Drag a TextMeshProUGUI component here to display the police timer value (e.g., 02:00).")]
    public TextMeshProUGUI policeTimerText;
    [Tooltip("Drag a UI Slider component here to visually represent the police timer bar.")]
    public Slider policeTimerBar;
    [Tooltip("Default color for the timer text.")]
    public Color policeTimerNormalColor = Color.white;
    [Tooltip("Color for the timer text when time is getting low.")]
    public Color policeTimerWarningColor = Color.yellow;
    [Tooltip("Color for the timer text when time is critical.")]
    public Color policeTimerCriticalColor = Color.red;
    [Tooltip("At what percentage remaining (0.0 to 1.0) does warning color apply?")]
    public float warningThreshold = 0.5f; // 50%
    [Tooltip("At what percentage remaining (0.0 to 1.0) does critical color apply?")]
    public float criticalThreshold = 0.25f; // 25%

    [Header("Clue Info Popup UI")]
    [Tooltip("Drag the GameObject that is the root of your Clue Info Pop-up UI here.")]
    public GameObject clueInfoPopupPanel;
    [Tooltip("Drag the TextMeshProUGUI component for the clue's name/title in the pop-up.")]
    public TextMeshProUGUI cluePopupNameText;
    [Tooltip("Drag the TextMeshProUGUI component for the clue's description in the pop-up.")]
    public TextMeshProUGUI cluePopupDescriptionText;
    [Tooltip("Drag the Image component for the clue's image in the pop-up (optional).")]
    public Image cluePopupImageUI;
    [Tooltip("Drag the Button component for closing the clue pop-up.")]
    public Button cluePopupContinueButton;

    [Header("Skill Check Display UI")]
    [Tooltip("Drag the GameObject that is the root of your Skill Check Display UI (the pill shape).")]
    public GameObject skillCheckDisplayPanel;
    [Tooltip("Drag the UI Image for the colored circle (red/green) here.")]
    public Image skillCheckCircleImage;
    [Tooltip("Drag the TextMeshProUGUI for the static text 'Skill Required'.")]
    public TextMeshProUGUI skillCheckStaticText;
    [Tooltip("Drag the TextMeshProUGUI for displaying the skill check DC (e.g., '30').")]
    public TextMeshProUGUI skillCheckValueText;
    [Tooltip("Drag the TextMeshProUGUI for displaying the skill name (e.g., 'Lockpicking').")]
    public TextMeshProUGUI skillCheckSkillNameText;
    [Tooltip("Color for the skill check circle when the check is successful.")]
    public Color skillCheckSuccessColor = Color.green;
    [Tooltip("Color for the skill check circle when the check fails.")]
    public Color skillCheckFailureColor = Color.red;

    [Header("Character Thought UI (Optional)")]
    [Tooltip("Drag the GameObject for the character thought bubble UI here (parent panel).")]
    public GameObject characterThoughtBubblePanel;
    [Tooltip("Drag the TextMeshProUGUI for the thought bubble text here.")]
    public TextMeshProUGUI characterThoughtText;

    // Internal State
    private float currentPoliceTimerValue;
    private float totalPoliceTimerDuration; // Store the initial calculated total duration
    private bool isTimerPaused = false;
    private List<GameObject> spawnedClueObjects = new List<GameObject>();
    private List<GameObject> spawnedCharacterObjects = new List<GameObject>();
    private List<GameObject> spawnedWitnessPopups = new List<GameObject>();
    private Coroutine characterThoughtCoroutine;

    void Start()
    {
        if (GameStateManager.Instance == null)
        {
            Debug.LogError("InvestigationManager: GameStateManager not found! Ensure it's in your initial scene and persists, or add a temporary one to this scene for testing.");
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
            Debug.LogError("InvestigationManager: No InvestigationSceneData assigned or passed! Please assign one in the Inspector if testing this scene directly.");
            enabled = false;
            return;
        }

        InitializeScene();
        InitializeUI();
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
            // Consider adding AspectRatioFitter to the backgroundImageUI GameObject for better results
            // For now, Preserve Aspect is handled by the Image component's own setting
            // backgroundImageUI.preserveAspect = true; // Ensure this is ticked on your Image component or set here
        }
        else
        {
            if(backgroundImageUI == null) Debug.LogWarning("InvestigationManager: Background Image UI reference is missing.");
            if(currentSceneData == null) Debug.LogWarning("InvestigationManager: CurrentSceneData is null during InitializeScene.");
            else if(currentSceneData.backgroundImage == null) Debug.LogWarning("InvestigationManager: Background Sprite in SceneData is missing.");
        }

        ClearSpawnedObjects();
        SpawnClues();
        SpawnCharacters();
        SpawnWitnessPopups();
        SetupPoliceTimer(); // Call this after everything else in InitializeScene is ready

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

        if (skillCheckStaticText != null)
        {
            skillCheckStaticText.text = "Skill Required"; // Set default static text
        }
        
        // Set initial timer text color
        if (policeTimerText != null) policeTimerText.color = policeTimerNormalColor;
    }

    void SpawnClues()
    {
        if (currentSceneData == null || currentSceneData.cluesInScene == null || cluesParent == null) return;
        foreach (ClueData clueData in currentSceneData.cluesInScene)
        {
            if (clueData == null) continue;
            Debug.Log("Spawning Clue (Placeholder): " + clueData.clueName);
            // TODO: Actual clue spawning logic
        }
    }

    void SpawnCharacters()
    {
        if (currentSceneData == null || currentSceneData.charactersInScene == null || charactersParent == null) return;
        foreach (CharacterPlacementData charPlacement in currentSceneData.charactersInScene)
        {
            if (charPlacement == null || charPlacement.characterProfile == null) continue;
            Debug.Log("Spawning Character (Placeholder): " + charPlacement.characterProfile.displayName);
            // TODO: Actual character spawning logic
        }
    }

    void SpawnWitnessPopups()
    {
        if (currentSceneData == null || currentSceneData.availableWitnesses == null) return;
        foreach (WitnessData witnessData in currentSceneData.availableWitnesses)
        {
            if (witnessData == null || witnessData.characterProfile == null) continue;
            Debug.Log("Creating Witness Pop-up (Placeholder): " + witnessData.characterProfile.displayName);
            // TODO: Actual witness UI spawning logic
        }
    }

    void SetupPoliceTimer()
    {
        if (currentSceneData == null || GameStateManager.Instance == null) {
            Debug.LogError("Cannot setup police timer - currentSceneData or GameStateManager.Instance is missing.");
            enabled = false; 
            return;
        }

        float baseTime = currentSceneData.basePoliceTimerSeconds;
        float reputationModifier = GameStateManager.Instance.PoliceReputationModifier;
        
        totalPoliceTimerDuration = baseTime * (1 + reputationModifier);
        totalPoliceTimerDuration = Mathf.Max(0.1f, totalPoliceTimerDuration); // Ensure total duration is not effectively zero
        
        currentPoliceTimerValue = totalPoliceTimerDuration; // Start timer full
        
        UpdatePoliceTimerUI(); // Initial UI update based on full time
        Debug.Log($"Police timer initiated: CurrentValue={currentPoliceTimerValue}, TotalDuration={totalPoliceTimerDuration} seconds (Base: {baseTime}, RepMod: {reputationModifier})");
    }

    void HandlePoliceTimer()
    {
        if (isTimerPaused) return;

        if (currentPoliceTimerValue > 0)
        {
            currentPoliceTimerValue -= Time.deltaTime;
            if (currentPoliceTimerValue <= 0)
            {
                currentPoliceTimerValue = 0; // Clamp to exactly zero
                UpdatePoliceTimerUI();       // Update UI to show "00:00"
                OnPoliceTimerEnd();          // Trigger end-of-timer logic
                return;                      // Stop further processing for this frame
            }
        }
        // If timer is already at or below zero (should be exactly zero due to above),
        // ensure UI reflects this, especially if paused/resumed at zero.
        UpdatePoliceTimerUI();
    }

    void UpdatePoliceTimerUI()
    {
        float displayValue = Mathf.Max(0f, currentPoliceTimerValue);

        if (policeTimerText != null)
        {
            int minutes = Mathf.FloorToInt(displayValue / 60F);
            int seconds = Mathf.FloorToInt(displayValue % 60F);
            policeTimerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);

            if (totalPoliceTimerDuration > 0.0001f)
            {
                float percentageRemaining = displayValue / totalPoliceTimerDuration;
                if (percentageRemaining <= criticalThreshold)       { policeTimerText.color = policeTimerCriticalColor; }
                else if (percentageRemaining <= warningThreshold)  { policeTimerText.color = policeTimerWarningColor; }
                else                                               { policeTimerText.color = policeTimerNormalColor; }
            } else {
                policeTimerText.color = policeTimerNormalColor;
            }
        }

        if (policeTimerBar != null)
        {
            if (totalPoliceTimerDuration > 0.0001f)
            {
                policeTimerBar.value = displayValue / totalPoliceTimerDuration;
            }
            else
            {
                policeTimerBar.value = (displayValue > 0f) ? 1f : 0f;
            }
        }
    }

    void OnPoliceTimerEnd()
    {
        if (isTimerPaused && currentPoliceTimerValue <= 0) return; // Already handled

        Debug.Log("Police Timer Ended! Player must leave.");
        currentPoliceTimerValue = 0; 
        PauseTimer(true);      // Sets isTimerPaused = true
        UpdatePoliceTimerUI(); // Final UI update after pausing and setting time to 0

        DisplayCharacterThought("The cops are here! I gotta bail!");
        // Example for future: StartCoroutine(DelayedSceneSwitch(GameStateManager.GameState.Map, 2.0f));
    }

    // IEnumerator DelayedSceneSwitch(GameStateManager.GameState state, float delay)
    // {
    //    yield return new WaitForSeconds(delay);
    //    GameStateManager.Instance.SwitchState(state, null);
    // }

    public void PauseTimer(bool pause)
    {
        isTimerPaused = pause;
        // Debug.Log("Police Timer " + (pause ? "Paused" : "Resumed")); // Optional: for debugging pause state
    }

    public void DisplayCharacterThought(string thoughtText, float duration = 3f)
    {
        if (string.IsNullOrEmpty(thoughtText)) return;
        if (characterThoughtBubblePanel == null || characterThoughtText == null) {
            Debug.LogWarning("Character thought UI not assigned. Logging thought: " + thoughtText);
            return;
        }
        if (characterThoughtCoroutine != null) StopCoroutine(characterThoughtCoroutine);
        characterThoughtCoroutine = StartCoroutine(ShowThoughtCoroutine(thoughtText, duration));
    }

    private System.Collections.IEnumerator ShowThoughtCoroutine(string thoughtText, float duration)
    {
        if (characterThoughtText == null || characterThoughtBubblePanel == null)
        {
            Debug.LogError("Character thought UI elements not assigned within coroutine start!");
            PauseTimer(false); 
            yield break; 
        }
        characterThoughtText.text = thoughtText;
        characterThoughtBubblePanel.SetActive(true);
        PauseTimer(true);
        Debug.Log("Character Thought Displayed: " + thoughtText);
        float elapsedTime = 0f;
        bool dismissedByClick = false;
        while(elapsedTime < duration)
        {
            if (Input.GetMouseButtonDown(0)) { dismissedByClick = true; break; }
            elapsedTime += Time.deltaTime;
            yield return null; 
        }
        if (dismissedByClick) Debug.Log("Character thought dismissed by click.");
        else Debug.Log("Character thought timed out.");
        characterThoughtBubblePanel.SetActive(false);
        PauseTimer(false);
        characterThoughtCoroutine = null;
    }

    public void ShowClueInfoPopup(ClueData clueData, string descriptionToShow)
    {
        if (clueInfoPopupPanel == null || clueData == null) return;
        PauseTimer(true);
        clueInfoPopupPanel.SetActive(true);
        if (cluePopupNameText != null) cluePopupNameText.text = clueData.clueName;
        if (cluePopupDescriptionText != null) cluePopupDescriptionText.text = descriptionToShow;
        if (cluePopupImageUI != null)
        {
            cluePopupImageUI.sprite = clueData.cluePopupImage;
            cluePopupImageUI.gameObject.SetActive(clueData.cluePopupImage != null);
        }
    }

    void CloseClueInfoPopup()
    {
        if (clueInfoPopupPanel != null) clueInfoPopupPanel.SetActive(false);
        PauseTimer(false);
    }

    public void ShowSkillCheckUI(ClueData forClue, bool checkSucceeded)
    {
        if (skillCheckDisplayPanel == null || forClue == null || !forClue.requiresSkillCheckForMoreInfo)
        {
            if(skillCheckDisplayPanel != null) skillCheckDisplayPanel.SetActive(false);
            return;
        }
        skillCheckDisplayPanel.SetActive(true);
        if (skillCheckValueText != null) skillCheckValueText.text = forClue.skillCheckDC.ToString();
        if (skillCheckSkillNameText != null) skillCheckSkillNameText.text = forClue.skillCheckType.ToString();
        if (skillCheckCircleImage != null) skillCheckCircleImage.color = checkSucceeded ? skillCheckSuccessColor : skillCheckFailureColor;
        Debug.Log($"Displaying Skill Check UI for {forClue.clueName}: {forClue.skillCheckDC} {forClue.skillCheckType} - Success: {checkSucceeded}");
    }

    public void HideSkillCheckUI()
    {
        if (skillCheckDisplayPanel != null) skillCheckDisplayPanel.SetActive(false);
    }

    void ClearSpawnedObjects()
    {
        foreach (GameObject obj in spawnedClueObjects) if (obj != null) Destroy(obj);
        spawnedClueObjects.Clear();
        foreach (GameObject obj in spawnedCharacterObjects) if (obj != null) Destroy(obj);
        spawnedCharacterObjects.Clear();
        foreach (GameObject obj in spawnedWitnessPopups) if (obj != null) Destroy(obj);
        spawnedWitnessPopups.Clear();
    }

    public void OnReturnedFromDialogue(object dialogueOutcomeData)
    {
        Debug.Log("InvestigationManager: Returned from Dialogue.");
        PauseTimer(false);
        // TODO: Process dialogueOutcomeData
    }

    void OnEnable()
    {
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnGameStateChanged += HandleGameStateChanged;
        }
    }

    void OnDisable()
    {
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
        }
        ClearSpawnedObjects();
    }

    private void HandleGameStateChanged(GameStateManager.GameState newState, object data)
    {
        if (this == null || !gameObject.activeInHierarchy) return;
        if (newState == GameStateManager.GameState.Investigation)
        {
            Debug.Log("InvestigationManager: HandleGameStateChanged - Investigation state is active.");
            InvestigationSceneData dataFromState = data as InvestigationSceneData;
            if (dataFromState != null && currentSceneData != dataFromState)
            {
                currentSceneData = dataFromState;
                InitializeScene();
            }
            else if (data != null && !(data is InvestigationSceneData))
            {
                OnReturnedFromDialogue(data);
            }
            else if (data == null && currentSceneData == null) { // Scene loaded directly, no data from GSM, nothing in Inspector
                Debug.LogError("Investigation scene loaded but no InvestigationSceneData available! Please assign one in Inspector or ensure GameStateManager passes it.");
                enabled = false; 
            }
             else if (data == null && currentSceneData != null) { // Scene loaded directly (currentSceneData from Inspector) or returning to it
                 PauseTimer(false); // Ensure timer is running if we are in the scene
            }
        }
        else
        {
            // We are leaving the investigation state
            PauseTimer(true);
        }
    }
}