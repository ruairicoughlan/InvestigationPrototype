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
    public GameObject cluePrefab; // For spawning clues with ClueInteractable.cs

    [Tooltip("Prefab that has the InteractiveObject script attached, used for characters (or a generic interactable).")]
    public GameObject characterInteractiveObjectPrefab; // Kept for character spawning logic


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
            Debug.LogError("InvestigationManager: No InvestigationSceneData assigned or passed via GameStateManager! Please assign one in the Inspector if testing this scene directly.");
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

        if (skillCheckStaticText != null)
        {
            skillCheckStaticText.text = "Skill Required";
        }
        
        if (policeTimerText != null) policeTimerText.color = policeTimerNormalColor;
    }

    void SpawnClues()
    {
        if (currentSceneData == null || currentSceneData.cluesInScene == null)
        {
            Debug.LogWarning("InvestigationManager: No scene data or cluesInScene to spawn.");
            return;
        }

        if (cluePrefab == null)
        {
            Debug.LogError("InvestigationManager: Clue Prefab not assigned in the Inspector!");
            return;
        }
        
        Transform parentToUse = cluesParent != null ? cluesParent : transform;

        Debug.Log($"Spawning {currentSceneData.cluesInScene.Count} clues for scene '{currentSceneData.name}'.");
        foreach (ClueData clueData in currentSceneData.cluesInScene)
        {
            if (clueData == null)
            {
                Debug.LogWarning("Encountered a null ClueData in cluesInScene list.");
                continue;
            }

            GameObject clueObjectInstance = Instantiate(cluePrefab, parentToUse);
            clueObjectInstance.name = $"Clue_{(!string.IsNullOrEmpty(clueData.clueID) ? clueData.clueID : clueData.clueName)}";
            
            clueObjectInstance.transform.localPosition = clueData.worldPosition;
            // Apply individual visual scale from ClueData
            clueObjectInstance.transform.localScale = new Vector3(clueData.visualScale.x, clueData.visualScale.y, 1f);

            ClueInteractable clueScript = clueObjectInstance.GetComponent<ClueInteractable>();
            if (clueScript != null)
            {
                clueScript.Initialize(clueData, this, GameStateManager.Instance);
                activeCluesInScene.Add(clueScript);
            }
            else
            {
                Debug.LogError($"Clue Prefab '{cluePrefab.name}' is missing the ClueInteractable script for clue: {clueData.clueName}. Destroying instance.");
                Destroy(clueObjectInstance);
            }
        }
        Debug.Log("Clue spawning complete. Spawned " + activeCluesInScene.Count + " clues.");
    }

    void SpawnCharacters()
    {
        if (currentSceneData == null || currentSceneData.charactersInScene == null) return;
        Transform parentToUse = charactersParent != null ? charactersParent : transform;

        if (characterInteractiveObjectPrefab == null) {
            // Debug.LogWarning("Character Interactive Object Prefab not assigned. Cannot spawn characters.");
            return;
        }

        foreach (CharacterPlacementData charPlacement in currentSceneData.charactersInScene)
        {
            if (charPlacement == null || charPlacement.characterProfile == null) continue;
            Debug.Log("Spawning Character (Placeholder): " + charPlacement.characterProfile.displayName);
            // Example if using InteractiveObject.cs for characters:
            // GameObject charInstance = Instantiate(characterInteractiveObjectPrefab, parentToUse);
            // charInstance.name = "Character_" + charPlacement.characterProfile.characterID;
            // charInstance.transform.localPosition = charPlacement.scenePosition; 
            // // If characters also need individual scaling, add a scale field to CharacterPlacementData
            // // charInstance.transform.localScale = new Vector3(charPlacement.visualScale.x, charPlacement.visualScale.y, 1f);
            // InteractiveObject charScript = charInstance.GetComponent<InteractiveObject>();
            // if (charScript != null)
            // {
            //    charScript.InitializeAsCharacter(charPlacement, this);
            //    spawnedCharacterObjects.Add(charInstance);
            // } else {
            //    Debug.LogError($"Character prefab {characterInteractiveObjectPrefab.name} missing InteractiveObject script for {charPlacement.characterProfile.displayName}");
            //    Destroy(charInstance);
            // }
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
        Debug.Log($"Police timer initiated: CurrentValue={currentPoliceTimerValue}, TotalDuration={totalPoliceTimerDuration} seconds (Base: {baseTime}, RepMod: {reputationModifier})");
    }

    void HandlePoliceTimer()
    {
        if (isTimerPaused || currentPoliceTimerValue <= 0) return;

        currentPoliceTimerValue -= Time.deltaTime;
        if (currentPoliceTimerValue <= 0)
        {
            currentPoliceTimerValue = 0;
            UpdatePoliceTimerUI();
            OnPoliceTimerEnd();
            return; 
        }
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
                policeTimerText.color = (displayValue <=0) ? policeTimerCriticalColor : policeTimerNormalColor;
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
                policeTimerBar.value = (displayValue > 0f && totalPoliceTimerDuration > 0.0001f) ? 1f : 0f;
            }
        }
    }

    void OnPoliceTimerEnd()
    {
        Debug.Log("Police Timer Ended! Player must leave.");
        PauseTimer(true); 

        DisplayCharacterThought("The cops are here! I gotta bail!", 5f);
    }

    public void PauseTimer(bool pause)
    {
        isTimerPaused = pause;
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

    private IEnumerator ShowThoughtCoroutine(string thoughtText, float duration)
    {
        if (characterThoughtText == null || characterThoughtBubblePanel == null)
        {
            Debug.LogError("Character thought UI elements not assigned within coroutine start!");
            if (currentPoliceTimerValue > 0) PauseTimer(false);  
            yield break; 
        }
        characterThoughtText.text = thoughtText;
        characterThoughtBubblePanel.SetActive(true);
        bool originalTimerState = isTimerPaused;
        PauseTimer(true);

        Debug.Log("Character Thought Displayed: " + thoughtText);
        float elapsedTime = 0f;
        bool dismissedByClick = false;
        
        while(elapsedTime < duration)
        {
            if (Input.GetMouseButtonDown(0) || Input.anyKeyDown) 
            { 
                dismissedByClick = true; 
                break; 
            }
            elapsedTime += Time.unscaledDeltaTime; 
            yield return null; 
        }

        if (dismissedByClick) Debug.Log("Character thought dismissed by input.");
        else Debug.Log("Character thought timed out.");
        
        characterThoughtBubblePanel.SetActive(false);
        if (!originalTimerState && currentPoliceTimerValue > 0) 
        {
            PauseTimer(false);
        }
        characterThoughtCoroutine = null;
    }

    public void ShowClueInfoPopup(ClueData clueData, string descriptionToShow)
    {
        if (clueInfoPopupPanel == null || clueData == null) 
        {
            Debug.LogError("Cannot show clue info: Panel or ClueData is null.");
            return;
        }
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
        if (currentPoliceTimerValue > 0) 
        {
            PauseTimer(false);
        }
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
    
    public void UpdateCluePerceptionChecks()
    {
        if (GameStateManager.Instance == null)
        {
            Debug.LogWarning("Cannot update clue perception checks; GameStateManager is missing.");
            return;
        }

        foreach (ClueInteractable clueScript in activeCluesInScene)
        {
            if (clueScript != null && !clueScript.gameObject.activeSelf) 
            {
                clueScript.UpdateInteractableState(); 
            }
        }
    }

    void ClearSpawnedObjects()
    {
        foreach (ClueInteractable clueScript in activeCluesInScene) 
        {
            if (clueScript != null) Destroy(clueScript.gameObject);
        }
        activeCluesInScene.Clear();

        foreach (GameObject obj in spawnedCharacterObjects) if (obj != null) Destroy(obj);
        spawnedCharacterObjects.Clear();
        
        foreach (GameObject obj in spawnedWitnessPopups) if (obj != null) Destroy(obj);
        spawnedWitnessPopups.Clear();
        Debug.Log("All spawned objects cleared.");
    }

    public void OnReturnedFromDialogue(object dialogueOutcomeData)
    {
        Debug.Log("InvestigationManager: Returned from Dialogue.");
        if (currentPoliceTimerValue > 0) 
        {
             PauseTimer(false);
        }
    }

    void OnEnable() 
    {
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnGameStateChanged.RemoveListener(HandleGameStateChanged);
            GameStateManager.Instance.OnGameStateChanged.AddListener(HandleGameStateChanged);
        }
    }

    void OnDisable()
    {
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnGameStateChanged.RemoveListener(HandleGameStateChanged);
        }
        if (characterThoughtCoroutine != null)
        {
            StopCoroutine(characterThoughtCoroutine);
            characterThoughtCoroutine = null;
        }
    }

    void OnDestroy() 
    {
         if (GameStateManager.Instance != null)
        {
            // Corrected the typo here
            GameStateManager.Instance.OnGameStateChanged.RemoveListener(HandleGameStateChanged); 
        }
        ClearSpawnedObjects();
    }

    private void HandleGameStateChanged(GameStateManager.GameState newState, object data)
    {
        if (this == null || !gameObject.activeInHierarchy || !enabled) return;

        if (newState == GameStateManager.GameState.Investigation)
        {
            Debug.Log("InvestigationManager: HandleGameStateChanged - Investigation state is active.");
            InvestigationSceneData dataFromState = data as InvestigationSceneData;

            if (dataFromState != null && (currentSceneData == null || currentSceneData.name != dataFromState.name)) 
            {
                currentSceneData = dataFromState;
                InitializeScene(); 
                InitializeUI();    
            }
            else if (dataFromState != null && currentSceneData == dataFromState) 
            {
                 Debug.Log("InvestigationManager: Returning to current investigation scene: " + currentSceneData.name);
                 if (clueInfoPopupPanel != null && clueInfoPopupPanel.activeSelf) CloseClueInfoPopup();
                 if (characterThoughtBubblePanel != null && characterThoughtBubblePanel.activeSelf) characterThoughtBubblePanel.SetActive(false);
                 if (currentPoliceTimerValue > 0) PauseTimer(false);
            }
            else if (data != null && !(data is InvestigationSceneData)) 
            {
                OnReturnedFromDialogue(data);
            }
            else if (data == null && currentSceneData == null) 
            {
                Debug.LogError("Investigation scene loaded but no InvestigationSceneData available! Please assign one in Inspector or ensure GameStateManager passes it.");
                enabled = false; 
            }
            else if (data == null && currentSceneData != null) 
            {
                 if (currentPoliceTimerValue > 0) PauseTimer(false);
            }
        }
        else 
        {
            PauseTimer(true);
            if (clueInfoPopupPanel != null && clueInfoPopupPanel.activeSelf) CloseClueInfoPopup(); 
            if (skillCheckDisplayPanel != null && skillCheckDisplayPanel.activeSelf) HideSkillCheckUI();
            if (characterThoughtBubblePanel != null && characterThoughtCoroutine != null) {
                StopCoroutine(characterThoughtCoroutine);
                characterThoughtCoroutine = null;
                characterThoughtBubblePanel.SetActive(false);
            }
        }
    }
}