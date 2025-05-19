using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using UnityEngine.Events; // Required for UnityEvent

// Make sure these enums are accessible, e.g., defined in CaseData.cs or a shared Enums.cs
// public enum CaseOverallStatus { Unavailable, Inactive, InProgress, Successful, Failed }
// public enum ObjectiveStatus { Inactive, Active, Completed, Failed }
// public enum TriggerConditionType { FlagIsSet, ObjectiveCompleted, PlayerLevel, CaseStatusIs, PlayerAcceptsQuestDialogue }

public class GameStateManager : MonoBehaviour
{
    // --- Singleton Instance ---
    private static GameStateManager _instance;
    public static GameStateManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<GameStateManager>();
                if (_instance == null)
                {
                    GameObject singletonObject = new GameObject("GameStateManager_Singleton");
                    _instance = singletonObject.AddComponent<GameStateManager>();
                }
            }
            return _instance;
        }
    }

    // --- Game State Enum & Event ---
    public enum GameState { MainMenu, Map, Investigation, Dialogue, Casing }
    [Header("Current Game State")]
    [SerializeField] private GameState currentGameState = GameState.MainMenu;
    public GameState CurrentGameState => currentGameState;
    [System.Serializable] public class GameStateChangedEvent : UnityEvent<GameState, object> { }
    public GameStateChangedEvent OnGameStateChanged = new GameStateChangedEvent();

    [Header("Scene Configuration")]
    public string mainMenuSceneName = "MainMenuScene";
    public string mapSceneName = "MapScene";
    public string investigationSceneName = "InvestigationScene";
    public string dialogueSceneName = "DialogueScene";
    public string casingSceneName = "CasingScene";

    [Header("Player Profile")]
    public string playerBackground = "Default";
    [Range(-1f, 1f)] public float policeReputationModifier = 0f;
    public float PoliceReputationModifier => policeReputationModifier;

    // --- Player Skills ---
    [Header("Debug Skill Overrides (For Testing)")]
    public int debugPerception = 30;
    public int debugLockpicking = 15;
    public int debugIntimidation = 20;
    public int debugPersuasion = 25;
    public int debugStreetwise = 10;
    public Dictionary<string, int> PlayerSkills { get; private set; } = new Dictionary<string, int>();

    // --- Case Management ---
    [Header("Case Progress Tracking")]
    // Outer Key: CaseID (string)
    // Inner Dictionary:
    //   "OverallStatus" -> CaseOverallStatus (enum)
    //   "ObjectiveStatus_ObjectiveID" -> ObjectiveStatus (enum)
    //   "CustomFlagNameForThisCase" -> bool
    public Dictionary<string, Dictionary<string, object>> CaseProgress { get; private set; } = new Dictionary<string, Dictionary<string, object>>();
    private Dictionary<string, CaseData> caseDataRegistry = new Dictionary<string, CaseData>(); // Loaded from Resources

    // --- Dialogue/Global State Tracking ---
    public HashSet<string> VisitedNodeIds { get; private set; } = new HashSet<string>();
    public Dictionary<string, bool> GlobalFlags { get; private set; } = new Dictionary<string, bool>();

    // --- Temporary State Data ---
    private object currentStateData = null; // For passing data with SwitchState

    protected virtual void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
            LoadAllCaseDataAssets(); // Load case definitions
            InitializeDefaultPlayerState(); // Initialize skills and other player data
            InitializeCaseProgressFromRegistry(); // Set initial status for all known cases
            Debug.Log("GameStateManager Awake: Instance Set & Initialized.");
        }
        else if (_instance != this)
        {
            Debug.LogWarning("GameStateManager Awake: Duplicate instance. Destroying self.");
            Destroy(gameObject);
        }
    }

    public virtual void InitializeDefaultPlayerState()
    {
        Debug.Log("GameStateManager: Initializing Default Player State.");
        PlayerSkills.Clear();
        PlayerSkills.Add("Perception", debugPerception);
        PlayerSkills.Add("Lockpicking", debugLockpicking);
        PlayerSkills.Add("Intimidation", debugIntimidation);
        PlayerSkills.Add("Persuasion", debugPersuasion);
        PlayerSkills.Add("Streetwise", debugStreetwise);

        VisitedNodeIds.Clear();
        GlobalFlags.Clear();
        // CaseProgress is initialized by LoadAllCaseDataAssets & InitializeCaseProgressFromRegistry
    }

    private void LoadAllCaseDataAssets()
    {
        caseDataRegistry.Clear();
        CaseData[] allCaseSOs = Resources.LoadAll<CaseData>("Cases"); // CaseData SOs MUST be in "Resources/Cases/"
        foreach (CaseData cd in allCaseSOs)
        {
            if (!string.IsNullOrEmpty(cd.CaseID) && !caseDataRegistry.ContainsKey(cd.CaseID))
            {
                caseDataRegistry.Add(cd.CaseID, cd);
            }
            else Debug.LogWarning($"Duplicate or invalid CaseID found in Resources/Cases: {cd.CaseID}");
        }
        Debug.Log($"Loaded {caseDataRegistry.Count} CaseData assets into registry.");
    }

    private void InitializeCaseProgressFromRegistry()
    {
        foreach (var pair in caseDataRegistry)
        {
            string caseID = pair.Key;
            if (!CaseProgress.ContainsKey(caseID)) // Only initialize if no prior (e.g., loaded save game) progress exists
            {
                CaseProgress[caseID] = new Dictionary<string, object>();
                CaseProgress[caseID]["OverallStatus"] = CaseOverallStatus.Unavailable; // All cases start Unavailable
                
                CaseData caseDef = pair.Value;
                foreach (var objDef in caseDef.Objectives)
                {
                    CaseProgress[caseID]["ObjectiveStatus_" + objDef.ObjectiveID] = ObjectiveStatus.Inactive;
                }
            }
        }
        Debug.Log("Initialized CaseProgress for all registered cases (defaulting to Unavailable/Inactive).");
    }


    public void SwitchState(GameState newState, object dataToPass = null)
    {
        string sceneToLoad = GetSceneNameForState(newState);
        bool isSameScene = (!string.IsNullOrEmpty(sceneToLoad) && SceneManager.GetActiveScene().name == sceneToLoad);

        if (currentGameState == newState && currentStateData == dataToPass && isSameScene)
        {
             Debug.Log($"GameStateManager: Already in state {newState} with the same data and scene. Re-invoking OnGameStateChanged for potential re-initialization of listeners.");
             OnGameStateChanged?.Invoke(newState, dataToPass);
             return;
        }

        Debug.Log($"GameStateManager: Switching from {currentGameState} (data: {currentStateData?.GetType().Name ?? "null"}) to {newState} (data: {dataToPass?.GetType().Name ?? "null"}). Target scene: {sceneToLoad ?? "None"}.");
        currentGameState = newState;
        currentStateData = dataToPass;

        if (!string.IsNullOrEmpty(sceneToLoad) && !isSameScene)
        {
            SceneManager.LoadScene(sceneToLoad);
            // OnGameStateChanged is invoked after scene load typically by the new scene's manager, or can be invoked here.
            // For now, let's assume target scene managers handle their own init based on GameStateManager.Instance.CurrentGameState
        }
        OnGameStateChanged?.Invoke(newState, dataToPass); // Invoke for non-scene-switching logic or persistent managers
        EvaluateCaseAndObjectiveTriggers(); // Evaluate case triggers whenever state changes
    }

    public string GetSceneNameForState(GameState state)
    {
        switch (state)
        {
            case GameState.MainMenu: return mainMenuSceneName;
            case GameState.Map: return mapSceneName;
            case GameState.Investigation: return investigationSceneName;
            case GameState.Dialogue: return dialogueSceneName;
            case GameState.Casing: return casingSceneName;
            default: Debug.LogWarning($"No scene name defined for state: {state}"); return null;
        }
    }

    // --- Player Skills ---
    public int GetSkillLevel(string skillName)
    {
        PlayerSkills.TryGetValue(skillName, out int level);
        return level;
    }
    public void SetSkillLevel(string skillName, int newLevel) { PlayerSkills[skillName] = newLevel; }
    public void ModifySkillLevel(string skillName, int amount) { PlayerSkills[skillName] = GetSkillLevel(skillName) + amount; }


    // --- Global Flags ---
    public void SetGlobalFlag(string flagID, bool value)
    {
        GlobalFlags[flagID] = value;
        Debug.Log($"Global Flag '{flagID}' set to {value}. Evaluating case triggers.");
        EvaluateCaseAndObjectiveTriggers(); // Flags can trigger case/objective changes
    }
    public bool CheckGlobalFlag(string flagID)
    {
        GlobalFlags.TryGetValue(flagID, out bool value);
        return value;
    }

    // --- Dialogue Node Tracking ---
    public void AddVisitedNode(string nodeId) { if (!VisitedNodeIds.Contains(nodeId)) VisitedNodeIds.Add(nodeId); }
    public bool HasVisitedNode(string nodeId) { return VisitedNodeIds.Contains(nodeId); }

    // --- CurrentStateData Get/Set ---
    public void SetCurrentStateData(object data) { currentStateData = data; }
    public T GetCurrentStateData<T>() where T : class { return currentStateData as T; }


    // === CASE MANAGEMENT LOGIC ===

    public CaseData GetCaseDataByID(string caseID)
    {
        caseDataRegistry.TryGetValue(caseID, out CaseData caseData);
        if (caseData == null && !string.IsNullOrEmpty(caseID)) Debug.LogWarning($"CaseData for ID '{caseID}' not found in registry.");
        return caseData;
    }

    // Call this when game events occur that might make cases available (e.g., after dialogues, level ups, other cases complete)
    public void EvaluateCaseAndObjectiveTriggers()
    {
        foreach (var caseEntry in caseDataRegistry)
        {
            string caseID = caseEntry.Key;
            CaseData caseData = caseEntry.Value;
            CaseOverallStatus currentOverallStatus = GetCaseOverallStatus(caseID);

            if (currentOverallStatus == CaseOverallStatus.Unavailable)
            {
                if (AreConditionsMet(caseData.MakeAvailable_Conditions, caseID))
                {
                    SetCaseOverallStatus(caseID, CaseOverallStatus.Inactive);
                }
            }
            else if (currentOverallStatus == CaseOverallStatus.Inactive)
            {
                // Auto-start conditions (if any, dialogue usually handles explicit start)
                if (AreConditionsMet(caseData.StartCase_Conditions, caseID))
                {
                    Internal_StartCaseLogic(caseID, caseData);
                }
            }
            else if (currentOverallStatus == CaseOverallStatus.InProgress)
            {
                // Check objectives
                foreach (var objDef in caseData.Objectives)
                {
                    ObjectiveStatus currentObjStatus = GetObjectiveStatus(caseID, objDef.ObjectiveID);
                    if (currentObjStatus == ObjectiveStatus.Inactive)
                    {
                        if (AreConditionsMet(objDef.TriggerToActivate, caseID, objDef.ObjectiveID))
                        {
                            SetObjectiveStatus(caseID, objDef.ObjectiveID, ObjectiveStatus.Active);
                        }
                    }
                    else if (currentObjStatus == ObjectiveStatus.Active)
                    {
                        if (AreConditionsMet(objDef.TriggerToComplete, caseID, objDef.ObjectiveID))
                        {
                            SetObjectiveStatus(caseID, objDef.ObjectiveID, ObjectiveStatus.Completed);
                            // SetObjectiveStatus will call CheckCaseOutcomeConditions
                        }
                        // Optional: Check for objective failure here if objDef.TriggerToFail_Objective exists
                    }
                }
                // Re-check overall outcome conditions (can be done here or more specifically when an objective completes)
                CheckCaseOutcomeConditions(caseID, caseData);
            }
        }
    }

    // Called by external game events (e.g., dialogue system choice)
    public void PlayerAcceptsCase(string caseID)
    {
        CaseData caseData = GetCaseDataByID(caseID);
        if (caseData == null) { Debug.LogError($"PlayerAcceptsCase: CaseID '{caseID}' not found."); return; }

        if (GetCaseOverallStatus(caseID) == CaseOverallStatus.Inactive)
        {
            // Your JSON "StartCase" trigger was "{ "Type": "PlayerAcceptsQuestDialogue" }"
            // This function call *is* that event. So we proceed to start it.
            // If CaseData.StartCase_Conditions had other requirements, they'd be checked here.
            if (AreConditionsMet(caseData.StartCase_Conditions, caseID)) { // Usually true if PlayerAcceptsQuestDialogue is the main trigger
                 Internal_StartCaseLogic(caseID, caseData);
            } else {
                Debug.LogWarning($"Case '{caseID}' not started after acceptance, StartCase_Conditions not met.");
            }
        }
        else
        {
            Debug.LogWarning($"Player tried to accept case '{caseID}', but it's not Inactive. Current: {GetCaseOverallStatus(caseID)}");
        }
    }
    
    private void Internal_StartCaseLogic(string caseID, CaseData caseData)
    {
        if (!CaseProgress.ContainsKey(caseID)) CaseProgress[caseID] = new Dictionary<string, object>();
        
        SetCaseOverallStatus(caseID, CaseOverallStatus.InProgress, caseData); // Sets status and OnStart flags

        foreach (CaseObjectiveDefinition objDef in caseData.Objectives)
        {
            // If objective isn't already beyond Inactive (e.g. loaded from save as Active/Completed)
            if (GetObjectiveStatus(caseID, objDef.ObjectiveID) == ObjectiveStatus.Inactive)
            {
                // Check activation conditions for this specific objective
                if (AreConditionsMet(objDef.TriggerToActivate, caseID, objDef.ObjectiveID))
                {
                    SetObjectiveStatus(caseID, objDef.ObjectiveID, ObjectiveStatus.Active);
                }
            }
        }
        Debug.Log($"Case '{caseData.CaseName}' ({caseID}) officially started (InProgress).");
    }

    public void SetCaseOverallStatus(string caseID, CaseOverallStatus newStatus, CaseData caseDefinitionIfKnown = null)
    {
        if (string.IsNullOrEmpty(caseID)) return;
        
        CaseData caseDef = caseDefinitionIfKnown ?? GetCaseDataByID(caseID);
        // Allow setting Unavailable even if caseDef is null initially, as registry might not be fully populated or it's a dynamic caseID
        if (caseDef == null && newStatus != CaseOverallStatus.Unavailable) 
        { 
            Debug.LogError($"SetCaseOverallStatus: CaseData for '{caseID}' not found. Cannot process flags/rewards.");
            if (!CaseProgress.ContainsKey(caseID)) CaseProgress[caseID] = new Dictionary<string, object>();
            CaseProgress[caseID]["OverallStatus"] = newStatus;
            OnGameStateChanged?.Invoke(CurrentGameState, null); 
            return;
        }

        if (!CaseProgress.ContainsKey(caseID)) CaseProgress[caseID] = new Dictionary<string, object>();
        
        CaseOverallStatus oldStatus = GetCaseOverallStatus(caseID);
        if (oldStatus == newStatus) return;

        CaseProgress[caseID]["OverallStatus"] = newStatus;
        Debug.Log($"Case '{caseDef?.CaseName ?? caseID}' OverallStatus changed from {oldStatus} to: {newStatus}");

        List<string> flagsToTrigger = null;
        if (caseDef != null) // Process flags/rewards only if definition is available
        {
            switch (newStatus)
            {
                case CaseOverallStatus.InProgress:
                    flagsToTrigger = caseDef.StartCase_Conditions.Find(c => c.Type == TriggerConditionType.FlagIsSet) != null ? 
                                     new List<string>() { caseDef.StartCase_Conditions.Find(c => c.Type == TriggerConditionType.FlagIsSet).StringParameterID } : 
                                     new List<string>(); // Simplified: Assume StartCase_Conditions only contains FlagIsSet for flags from JSON
                                     // This should be flagsToSetOnStart from CaseData as per refined design
                    // flagsToTrigger = caseDef.flagsToSetOnStart; // If CaseData had a direct list of flags for this
                    break;
                case CaseOverallStatus.Successful:
                    flagsToTrigger = caseDef.RewardsOnSuccess?.FlagsToSet; // Uses FlagsToSet from RewardSet
                    ProcessRewardSet(caseDef.RewardsOnSuccess);
                    break;
                case CaseOverallStatus.Failed:
                    flagsToTrigger = caseDef.RewardsOnFailure?.FlagsToSet; // Uses FlagsToSet from RewardSet
                    ProcessRewardSet(caseDef.RewardsOnFailure);
                    break;
            }
        }
        if (flagsToTrigger != null)
        {
            foreach (string flag in flagsToTrigger) if(!string.IsNullOrEmpty(flag)) SetGlobalFlag(flag, true);
        }
        OnGameStateChanged?.Invoke(CurrentGameState, caseDef);
    }

    public CaseOverallStatus GetCaseOverallStatus(string caseID)
    {
        if (CaseProgress.TryGetValue(caseID, out var caseDetails))
        {
            if (caseDetails.TryGetValue("OverallStatus", out var statusObj) && statusObj is CaseOverallStatus status)
            {
                return status;
            }
        }
        return caseDataRegistry.ContainsKey(caseID) ? CaseOverallStatus.Unavailable : CaseOverallStatus.Unavailable;
    }

    public void SetObjectiveStatus(string caseID, string objectiveID, ObjectiveStatus newStatus)
    {
        if (string.IsNullOrEmpty(caseID) || string.IsNullOrEmpty(objectiveID)) return;
        if (!CaseProgress.ContainsKey(caseID)) {
            Debug.LogWarning($"SetObjectiveStatus: CaseID '{caseID}' not found in CaseProgress. Starting case implicitly to set objective.");
            // Attempt to start the case if it's known but not in progress (e.g. if a clue tries to update an objective of an Inactive case)
            CaseData implicitCaseDef = GetCaseDataByID(caseID);
            if (implicitCaseDef != null) Internal_StartCaseLogic(caseID, implicitCaseDef);
            else { Debug.LogError($"SetObjectiveStatus: Cannot find CaseData for '{caseID}' to implicitly start."); return; }

            if (!CaseProgress.ContainsKey(caseID)) { Debug.LogError($"SetObjectiveStatus: Case '{caseID}' still not in progress after implicit start attempt."); return; }
        }

        string key = "ObjectiveStatus_" + objectiveID;
        ObjectiveStatus oldStatus = GetObjectiveStatus(caseID, objectiveID);
        if (oldStatus == newStatus && CaseProgress[caseID].ContainsKey(key)) return;

        CaseProgress[caseID][key] = newStatus;
        Debug.Log($"Objective '{objectiveID}' in Case '{caseID}' status changed from {oldStatus} to: {newStatus}");

        CaseData caseDef = GetCaseDataByID(caseID);
        if (caseDef != null) {
            if (newStatus == ObjectiveStatus.Completed)
            {
                // Process optional objective rewards
                foreach(var optRewardEntry in caseDef.RewardsForOptionalObjectiveCompletion)
                {
                    if (optRewardEntry.ObjectiveID == objectiveID)
                    {
                        ProcessRewardSet(optRewardEntry.Rewards);
                        break; 
                    }
                }
                // TODO: Set any flags defined in CaseObjectiveDefinition.flagsToSetOnCompletion
            }
            CheckCaseOutcomeConditions(caseID, caseDef);
        }
        OnGameStateChanged?.Invoke(CurrentGameState, caseDef);
    }

    public ObjectiveStatus GetObjectiveStatus(string caseID, string objectiveID)
    {
        if (CaseProgress.TryGetValue(caseID, out var caseDetails))
        {
            if (caseDetails.TryGetValue("ObjectiveStatus_" + objectiveID, out var statusObj) && statusObj is ObjectiveStatus status)
            {
                return status;
            }
        }
        // If objective status isn't found, but the case is known, assume Inactive for its objectives
        return (CaseProgress.ContainsKey(caseID) || caseDataRegistry.ContainsKey(caseID)) ? ObjectiveStatus.Inactive : ObjectiveStatus.Inactive; // Or a distinct "UnknownObjective"
    }

    // Called by ClueInteractable
    public void CompleteCaseObjective(string caseID, string objectiveID, bool completed = true)
    {
        Debug.Log($"GameStateManager: Request to mark Objective '{objectiveID}' for Case '{caseID}' as {(completed ? "Completed" : "Active")}.");
        SetObjectiveStatus(caseID, objectiveID, completed ? ObjectiveStatus.Completed : ObjectiveStatus.Active); 
    }

    // For general boolean flags within a specific case's progress
    public void SetCaseSpecificFlag(string caseID, string flagName, bool value)
    {
        if (string.IsNullOrEmpty(caseID) || string.IsNullOrEmpty(flagName)) return;
        if (!CaseProgress.ContainsKey(caseID))
        {
            CaseProgress[caseID] = new Dictionary<string, object>();
        }
        CaseProgress[caseID][flagName] = value;
        Debug.Log($"Case '{caseID}', Specific Flag '{flagName}' set to {value}. Evaluating case triggers.");
        EvaluateCaseAndObjectiveTriggers(); // Case-specific flags can trigger changes
    }

    public bool IsCaseSpecificFlagTrue(string caseID, string flagName)
    {
        if (CaseProgress.TryGetValue(caseID, out var caseDetails))
        {
            if (caseDetails.TryGetValue(flagName, out var valueObj) && valueObj is bool value)
            {
                return value;
            }
        }
        return false;
    }

    public bool AreConditionsMet(List<TriggerCondition> conditions, string currentCaseIDForContext, string currentObjectiveIDForContext = null)
    {
        if (conditions == null || conditions.Count == 0) return true;

        foreach (TriggerCondition condition in conditions)
        {
            bool conditionResult = false;
            switch (condition.Type)
            {
                case TriggerConditionType.FlagIsSet:
                    // Decide if FlagID refers to a GlobalFlag or a CaseSpecificFlag
                    // For now, assume GlobalFlag. If you want CaseSpecificFlag, you'd need to pass currentCaseIDForContext
                    // or have a naming convention for flags e.g. "Case_001_Dewey.MyFlag"
                    conditionResult = (CheckGlobalFlag(condition.StringParameterID) == condition.RequiredBoolState);
                    break;
                case TriggerConditionType.ObjectiveCompleted:
                    string targetCaseID_Obj = string.IsNullOrEmpty(condition.TargetCaseIDForCondition) ? currentCaseIDForContext : condition.TargetCaseIDForCondition;
                    string targetObjID = condition.StringParameterID; // StringParameterID now holds ObjectiveID
                    conditionResult = (GetObjectiveStatus(targetCaseID_Obj, targetObjID) == ObjectiveStatus.Completed) == condition.RequiredBoolState;
                    break;
                case TriggerConditionType.PlayerLevel:
                    // int playerLevel = GetPlayerLevel(); // Placeholder
                    // conditionResult = (playerLevel >= condition.IntParameter); // IntParameter for MinLevel
                    Debug.LogWarning("PlayerLevel condition check not implemented.");
                    conditionResult = true; // Defaulting for now
                    break;
                case TriggerConditionType.CaseStatusIs:
                    string caseToCheckID = string.IsNullOrEmpty(condition.StringParameterID) ? currentCaseIDForContext : condition.StringParameterID; // StringParameterID is CaseID to check
                    CaseOverallStatus statusOfCaseToCheck = GetCaseOverallStatus(caseToCheckID);
                    conditionResult = (statusOfCaseToCheck == (CaseOverallStatus)condition.IntParameter); // IntParameter is TargetCaseStatus enum as int
                    break;
                case TriggerConditionType.PlayerAcceptsQuestDialogue:
                    // This condition type implies an event has happened. In a static check like this,
                    // it's hard to evaluate unless a flag was set by that event.
                    // For "MakeAvailable" or "StartCase" it might imply those lists are only checked *after* such an event.
                    // Or, if this type is used in "conditionsToActivate" for an objective, it implies the case has started.
                    Debug.LogWarning("PlayerAcceptsQuestDialogue condition type is context-dependent for evaluation.");
                    conditionResult = true; // Assuming if it's in a list, its pre-condition (dialogue) was met to reach this check.
                    break;
            }
            if (!conditionResult) return false;
        }
        return true;
    }

    private void CheckCaseOutcomeConditions(string caseID, CaseData caseDef)
    {
        if (caseDef == null) { Debug.LogError($"CheckCaseOutcomeConditions: CaseData for '{caseID}' is null."); return; }
        
        CaseOverallStatus currentOverallStatus = GetCaseOverallStatus(caseID);
        if (currentOverallStatus != CaseOverallStatus.InProgress) return;

        if (caseDef.FailureConditions != null && AreConditionsMet(caseDef.FailureConditions, caseID))
        {
            SetCaseOverallStatus(caseID, CaseOverallStatus.Failed, caseDef);
            return; 
        }

        if (caseDef.SuccessConditions != null && AreConditionsMet(caseDef.SuccessConditions, caseID))
        {
            bool allPrimaryObjectivesAreActuallyComplete = true;
            foreach(var objDef in caseDef.Objectives) {
                if (!objDef.IsOptional && GetObjectiveStatus(caseID, objDef.ObjectiveID) != ObjectiveStatus.Completed) {
                    allPrimaryObjectivesAreActuallyComplete = false;
                    break;
                }
            }
            if(allPrimaryObjectivesAreActuallyComplete) {
                SetCaseOverallStatus(caseID, CaseOverallStatus.Successful, caseDef);
            } else {
                Debug.Log($"Case '{caseID}' met SuccessConditions list, but not all primary objectives are complete. Status remains InProgress.");
            }
        }
    }

    private void ProcessRewardSet(CaseOutcomeRewards rewardSet)
    {
        if (rewardSet == null) return;
        Debug.Log($"Processing Rewards: XP {rewardSet.Experience}");
        // if (PlayerStats.Instance != null) PlayerStats.Instance.AddExperience(rewardSet.Experience);
        
        if (rewardSet.Reputation != null) {
            foreach (var repChange in rewardSet.Reputation) {
                Debug.Log($"Reputation change for {repChange.FactionID}: {repChange.Change}");
                // if (FactionManager.Instance != null) FactionManager.Instance.ModifyReputation(repChange.FactionID, repChange.Change);
            }
        }
        if (rewardSet.FlagsToSet != null) {
            foreach (string flag in rewardSet.FlagsToSet) SetGlobalFlag(flag, true);
        }
        if (rewardSet.NewPartyMembers != null) {
            foreach (string memberID in rewardSet.NewPartyMembers) {
                Debug.Log($"Adding new party member: {memberID}");
                // PartyManager.Instance.AddMember(memberID);
            }
        }
    }
}