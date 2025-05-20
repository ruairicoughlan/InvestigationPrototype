using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using UnityEngine.Events; // Required for UnityEvent

public class GameStateManager : MonoBehaviour
{
    // --- Singleton Instance ---
    private static GameStateManager _instance;
    private static bool s_isInitialized = false;

    public static GameStateManager Instance
    {
        get
        {
            if (_instance == null)
            {
                Debug.LogWarning("[GameStateManager.Instance GETTER] Instance is NULL. Attempting to find existing...");
                _instance = FindObjectOfType<GameStateManager>();
                if (_instance == null)
                {
                    Debug.LogError("[GameStateManager.Instance GETTER] No instance found. CREATING NEW GameStateManager_Singleton object.");
                    GameObject singletonObject = new GameObject("GameStateManager_Singleton (Dynamically Created)");
                    _instance = singletonObject.AddComponent<GameStateManager>();
                    // Note: If created dynamically, its Awake() will handle initialization.
                }
                else
                {
                    Debug.Log($"[GameStateManager.Instance GETTER] Found existing instance in scene: {_instance.gameObject.name}. Its Awake should handle initialization if not already done.");
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

    // --- Case Update Events ---
    [System.Serializable] public class CaseStatusChangedEvent : UnityEvent<CaseData, CaseOverallStatus, CaseOverallStatus> { } // CaseData, OldStatus, NewStatus
    public CaseStatusChangedEvent OnCaseOverallStatusChanged = new CaseStatusChangedEvent();

    [System.Serializable] public class ObjectiveStatusChangedEvent : UnityEvent<CaseData, CaseObjectiveDefinition, ObjectiveStatus, ObjectiveStatus> { } // CaseData, ObjectiveDef, OldStatus, NewStatus
    public ObjectiveStatusChangedEvent OnObjectiveStatusChanged = new ObjectiveStatusChangedEvent();


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

    [Header("Debug Skill Overrides (For Testing)")]
    public int debugPerception = 30;
    public int debugLockpicking = 15;
    public int debugIntimidation = 20;
    public int debugPersuasion = 25;
    public int debugStreetwise = 10;

    public Dictionary<string, int> PlayerSkills { get; private set; } = new Dictionary<string, int>();
    public Dictionary<string, Dictionary<string, object>> CaseProgress { get; private set; } = new Dictionary<string, Dictionary<string, object>>();
    private Dictionary<string, CaseData> caseDataRegistry = new Dictionary<string, CaseData>();
    public HashSet<string> VisitedNodeIds { get; private set; } = new HashSet<string>();
    public Dictionary<string, bool> GlobalFlags { get; private set; } = new Dictionary<string, bool>();
    private object currentStateData = null;

    protected virtual void Awake()
    {
        Debug.Log($"[GameStateManager AWAKE START] Called for GameObject: {gameObject.name}, InstanceID: {GetInstanceID()}");
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log($"[GameStateManager AWAKE - Branch 1] _instance was null. SET to this (Name: {gameObject.name}). Calling InitializeManager.");
            InitializeManager();
        }
        else if (_instance != this)
        {
            Debug.LogWarning($"[GameStateManager AWAKE - Branch 2] Duplicate instance (Name: {gameObject.name}). Destroying self. Existing _instance is (Name: {_instance.gameObject.name}).");
            Destroy(gameObject); // This will trigger OnDestroy for this duplicate instance
        }
        else
        {
            Debug.Log($"[GameStateManager AWAKE - Branch 3] _instance is already this (Name: {gameObject.name}). Ensuring initialization by calling InitializeManager.");
            InitializeManager(); // Should be safe if s_isInitialized check is robust
        }
        Debug.Log($"[GameStateManager AWAKE END] Finished for GameObject: {gameObject.name}");
    }

    private void InitializeManager()
    {
        Debug.Log($"[GameStateManager InitializeManager START] Attempting initialization for instance (Name: {gameObject.name}, ID: {GetInstanceID()}). s_isInitialized: {s_isInitialized}, _instance == this: {(_instance == this)}");
        if (s_isInitialized && _instance == this) { Debug.Log($"[GameStateManager InitializeManager] Already initialized by this instance. Skipping."); return; }
        if (_instance != this) { Debug.LogWarning($"[GameStateManager InitializeManager] This instance (Name: {gameObject.name}) is not the designated singleton. Skipping initialization."); return; }

        Debug.Log($"[GameStateManager InitializeManager] Proceeding with initialization for singleton instance (Name: {gameObject.name}).");
        InitializeDefaultPlayerState();
        LoadAllCaseDataAssets();
        InitializeCaseProgressFromRegistry();
        s_isInitialized = true;
        Debug.Log("[GameStateManager InitializeManager] Core initialization complete, s_isInitialized set to true. Now evaluating initial triggers.");
        EvaluateCaseAndObjectiveTriggers();
        Debug.Log("[GameStateManager InitializeManager] Full initialization including first trigger evaluation complete.");
    }

    // ADDED/MODIFIED OnDestroy for detailed logging
    protected virtual void OnDestroy()
    {
        Debug.LogWarning($"[GameStateManager OnDestroy START] Called for GameObject: {gameObject.name}, InstanceID: {GetInstanceID()}. Current static _instance is: {(_instance != null ? _instance.gameObject.name + " (ID: " + _instance.GetInstanceID() + ")" : "null")}", this.gameObject);
        if (_instance == this)
        {
            _instance = null;
            s_isInitialized = false; // Reset initialization flag if the active instance is destroyed
            Debug.LogWarning($"[GameStateManager OnDestroy] This instance was the active singleton (_instance). Static _instance and s_isInitialized have been reset to null/false.");
        }
        else if (_instance != null)
        {
            Debug.LogWarning($"[GameStateManager OnDestroy] This instance (Name: {gameObject.name}) was NOT the active singleton. The active singleton _instance is (Name: {_instance.gameObject.name}). No changes made to static _instance by this OnDestroy call.");
        }
        else // _instance was already null
        {
            Debug.LogWarning($"[GameStateManager OnDestroy] This instance (Name: {gameObject.name}) is being destroyed, and static _instance was already null.");
        }
        Debug.LogWarning($"[GameStateManager OnDestroy END] For GameObject: {gameObject.name}");
    }


    public virtual void InitializeDefaultPlayerState()
    {
        Debug.Log("[GameStateManager - InitializeDefaultPlayerState] Initializing Player State.");
        PlayerSkills.Clear();
        PlayerSkills.Add("Perception", debugPerception); PlayerSkills.Add("Lockpicking", debugLockpicking); PlayerSkills.Add("Intimidation", debugIntimidation);
        PlayerSkills.Add("Persuasion", debugPersuasion); PlayerSkills.Add("Streetwise", debugStreetwise);
        VisitedNodeIds.Clear(); GlobalFlags.Clear();
    }

    private void LoadAllCaseDataAssets()
    {
        Debug.Log("[GameStateManager - LoadAllCaseDataAssets] Attempting to load CaseData assets from 'Resources/Cases'...");
        caseDataRegistry.Clear();
        CaseData[] allCaseSOs = Resources.LoadAll<CaseData>("Cases");
        Debug.Log($"[GameStateManager - LoadAllCaseDataAssets] Resources.LoadAll found {allCaseSOs.Length} assets.");
        foreach (CaseData cd in allCaseSOs) {
            if (cd != null && !string.IsNullOrEmpty(cd.CaseID)) {
                string cleanCaseID = cd.CaseID.Trim();
                if (!caseDataRegistry.ContainsKey(cleanCaseID)) {
                    caseDataRegistry.Add(cleanCaseID, cd);
                    Debug.Log($"[GameStateManager - LoadAllCaseDataAssets] Added CaseData '{cd.name}' with ID '{cleanCaseID}' to registry.");
                } else { Debug.LogWarning($"[GameStateManager - LoadAllCaseDataAssets] Duplicate CaseID '{cleanCaseID}' for asset '{cd.name}'. Original: '{caseDataRegistry[cleanCaseID].name}'. Skipping."); }
            } else { Debug.LogWarning($"[GameStateManager - LoadAllCaseDataAssets] Found null CaseData or one with empty CaseID. Asset Name: {(cd != null ? cd.name : "NULL_ASSET")}");}
        }
        Debug.Log($"[GameStateManager - LoadAllCaseDataAssets] Finished. Registry contains {caseDataRegistry.Count} entries.");
    }

    private void InitializeCaseProgressFromRegistry()
    {
        Debug.Log($"[GameStateManager - InitializeCaseProgressFromRegistry] Initializing CaseProgress. Registry count: {caseDataRegistry.Count}");
        CaseProgress.Clear();
        foreach (var pair in caseDataRegistry) {
            string caseID = pair.Key; CaseData caseDef = pair.Value;
            if (caseDef == null) { Debug.LogWarning($"[GameStateManager - InitializeCaseProgressFromRegistry] Null CaseData for ID '{caseID}'. Skipping."); continue; }
            CaseProgress[caseID] = new Dictionary<string, object>();
            CaseProgress[caseID]["OverallStatus"] = CaseOverallStatus.Unavailable;
            if (caseDef.Objectives != null) {
                foreach (var objDef in caseDef.Objectives) {
                     if (objDef != null && !string.IsNullOrEmpty(objDef.ObjectiveID)) CaseProgress[caseID]["ObjectiveStatus_" + objDef.ObjectiveID.Trim()] = ObjectiveStatus.Inactive;
                     else Debug.LogWarning($"[GameStateManager - InitializeCaseProgressFromRegistry] Case '{caseID}' has a null or invalid ObjectiveDefinition.");
                }
            }
        }
        Debug.Log("[GameStateManager - InitializeCaseProgressFromRegistry] Finished initializing CaseProgress dictionary.");
    }

    public void SwitchState(GameState newState, object dataToPass = null)
    {
        string sceneToLoad = GetSceneNameForState(newState);
        bool isSameScene = (!string.IsNullOrEmpty(sceneToLoad) && SceneManager.GetActiveScene().name == sceneToLoad);
        if (currentGameState == newState && currentStateData == dataToPass && isSameScene) {
             Debug.Log($"GameStateManager: Already in state {newState}. Re-invoking OnGameStateChanged.");
             OnGameStateChanged?.Invoke(newState, dataToPass); return;
        }
        Debug.Log($"GameStateManager: Switching from {currentGameState} to {newState}. Target scene: {sceneToLoad ?? "None"}.");
        currentGameState = newState; currentStateData = dataToPass;
        if (!string.IsNullOrEmpty(sceneToLoad) && !isSameScene) SceneManager.LoadScene(sceneToLoad);
        OnGameStateChanged?.Invoke(newState, dataToPass);
        EvaluateCaseAndObjectiveTriggers();
    }

    public string GetSceneNameForState(GameState state) {
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
    public int GetSkillLevel(string skillName) { PlayerSkills.TryGetValue(skillName, out int level); return level; }
    public void SetSkillLevel(string skillName, int newLevel) { PlayerSkills[skillName] = newLevel; }
    public void ModifySkillLevel(string skillName, int amount) { PlayerSkills[skillName] = GetSkillLevel(skillName) + amount; }
    public void SetGlobalFlag(string flagID, bool value) { GlobalFlags[flagID] = value; Debug.Log($"Global Flag '{flagID}' set to {value}. Evaluating triggers."); EvaluateCaseAndObjectiveTriggers(); }
    public bool CheckGlobalFlag(string flagID) { GlobalFlags.TryGetValue(flagID, out bool value); return value; }
    public void AddVisitedNode(string nodeId) { if (!VisitedNodeIds.Contains(nodeId)) VisitedNodeIds.Add(nodeId); }
    public bool HasVisitedNode(string nodeId) { return VisitedNodeIds.Contains(nodeId); }
    public void SetCurrentStateData(object data) { currentStateData = data; }
    public T GetCurrentStateData<T>() where T : class { return currentStateData as T; }
    public void UpdatePoliceReputation(float change) { policeReputationModifier = Mathf.Clamp(policeReputationModifier + change, -1f, 1f); Debug.Log($"Police Reputation: {PoliceReputationModifier}"); }

    public CaseData GetCaseDataByID(string caseID)
    {
        if (string.IsNullOrEmpty(caseID)) { Debug.LogWarning("[GameStateManager - GetCaseDataByID] Requested CaseID is null or empty."); return null; }
        string cleanCaseID = caseID.Trim();
        if (caseDataRegistry == null) { Debug.LogError("[GameStateManager - GetCaseDataByID] caseDataRegistry is NULL!"); return null; }
        caseDataRegistry.TryGetValue(cleanCaseID, out CaseData caseData);
        if (caseData == null) Debug.LogWarning($"[GameStateManager - GetCaseDataByID] CaseData for ID '{cleanCaseID}' not found in registry. Registry count: {caseDataRegistry.Count}");
        return caseData;
    }

    public CaseObjectiveDefinition GetObjectiveDefinition(CaseData caseData, string objectiveID)
    {
        if (caseData == null || caseData.Objectives == null || string.IsNullOrEmpty(objectiveID)) return null;
        string cleanObjectiveID = objectiveID.Trim();
        foreach (var objDef in caseData.Objectives)
        {
            if (objDef != null && objDef.ObjectiveID?.Trim() == cleanObjectiveID)
            {
                return objDef;
            }
        }
        Debug.LogWarning($"[GameStateManager] Objective definition for ID '{cleanObjectiveID}' not found in Case '{caseData.CaseID}'.");
        return null;
    }


    public void ActivateCase(string caseID)
    {
        if (string.IsNullOrEmpty(caseID)) { Debug.LogError("ActivateCase: Provided caseID is null or empty."); return; }
        string cleanCaseID = caseID.Trim();
        CaseData caseData = GetCaseDataByID(cleanCaseID);
        if (caseData == null) { Debug.LogError($"ActivateCase: CaseID '{cleanCaseID}' not found in registry."); return; }
        CaseOverallStatus currentOverallStatus = GetCaseOverallStatus(cleanCaseID);
        if (currentOverallStatus == CaseOverallStatus.Inactive || currentOverallStatus == CaseOverallStatus.Unavailable) {
            if (AreConditionsMet(caseData.StartCase_Conditions, cleanCaseID)) Internal_StartCaseLogic(cleanCaseID, caseData);
            else Debug.LogWarning($"Case '{cleanCaseID}' not started (explicit activation), StartCase_Conditions not met. Current Status: {currentOverallStatus}");
        } else Debug.LogWarning($"Tried to activate case '{cleanCaseID}', but not Inactive/Unavailable. Status: {currentOverallStatus}");
    }

    private void Internal_StartCaseLogic(string caseID, CaseData caseData)
    {
        if (!CaseProgress.ContainsKey(caseID)) CaseProgress[caseID] = new Dictionary<string, object>();

        bool objectivesActivatedThisPass = false;
        if (caseData.Objectives != null) {
            foreach (CaseObjectiveDefinition objDef in caseData.Objectives) {
                if (objDef != null && !string.IsNullOrEmpty(objDef.ObjectiveID) && GetObjectiveStatus(caseID, objDef.ObjectiveID) == ObjectiveStatus.Inactive) {
                    if (AreConditionsMet(objDef.TriggerToActivate, caseID, objDef.ObjectiveID)) {
                        string key = "ObjectiveStatus_" + objDef.ObjectiveID.Trim();
                        ObjectiveStatus oldObjStat = GetObjectiveStatus(caseID, objDef.ObjectiveID);
                        CaseProgress[caseID][key] = ObjectiveStatus.Active;
                        Debug.Log($"Objective '{objDef.ObjectiveID.Trim()}' in Case '{caseID}' status: {oldObjStat} -> Active (during StartCaseLogic)");
                        OnObjectiveStatusChanged?.Invoke(caseData, objDef, oldObjStat, ObjectiveStatus.Active);
                        objectivesActivatedThisPass = true;
                    }
                }
            }
        }

        SetCaseOverallStatus(caseID, CaseOverallStatus.InProgress, caseData);
        Debug.Log($"Case '{caseData.CaseName}' ({caseID}) set to InProgress. Initial objectives evaluated for activation.");
        if (objectivesActivatedThisPass) EvaluateCaseAndObjectiveTriggers();
    }

    public void EvaluateCaseAndObjectiveTriggers()
    {
        if (!s_isInitialized) { Debug.LogWarning("[GameStateManager - EvaluateCaseAndObjectiveTriggers] Called before GameStateManager is fully initialized. Skipping."); return; }
        if (caseDataRegistry == null || caseDataRegistry.Count == 0) { Debug.LogWarning("[GameStateManager - EvaluateCaseAndObjectiveTriggers] CaseDataRegistry is null or empty. No triggers to evaluate."); return; }

        foreach (var caseEntry in caseDataRegistry) {
            string caseID = caseEntry.Key; CaseData caseData = caseEntry.Value;
            if (caseData == null) { Debug.LogWarning($"Null CaseData in registry for ID '{caseID}' during trigger eval."); continue; }
            CaseOverallStatus currentOverallStatus = GetCaseOverallStatus(caseID);

            if (currentOverallStatus == CaseOverallStatus.Unavailable) {
                if (AreConditionsMet(caseData.MakeAvailable_Conditions, caseID)) SetCaseOverallStatus(caseID, CaseOverallStatus.Inactive, caseData);
            } else if (currentOverallStatus == CaseOverallStatus.Inactive) {
                if (AreConditionsMet(caseData.StartCase_Conditions, caseID)) Internal_StartCaseLogic(caseID, caseData);
            } else if (currentOverallStatus == CaseOverallStatus.InProgress) {
                bool anyObjectiveStateChangedInThisPass = false;
                if (caseData.Objectives != null) {
                    foreach (var objDef in caseData.Objectives) {
                        if (objDef == null || string.IsNullOrEmpty(objDef.ObjectiveID)) continue;
                        ObjectiveStatus oldObjStatus = GetObjectiveStatus(caseID, objDef.ObjectiveID);
                        ObjectiveStatus newObjStatus = oldObjStatus;

                        if (oldObjStatus == ObjectiveStatus.Inactive && AreConditionsMet(objDef.TriggerToActivate, caseID, objDef.ObjectiveID)) newObjStatus = ObjectiveStatus.Active;
                        else if (oldObjStatus == ObjectiveStatus.Active && AreConditionsMet(objDef.TriggerToComplete, caseID, objDef.ObjectiveID)) newObjStatus = ObjectiveStatus.Completed;

                        if (newObjStatus != oldObjStatus) {
                            string key = "ObjectiveStatus_" + objDef.ObjectiveID.Trim();
                            CaseProgress[caseID][key] = newObjStatus;
                            Debug.Log($"Objective '{objDef.ObjectiveID.Trim()}' in Case '{caseID}' status: {oldObjStatus} -> {newObjStatus} (during EvaluateCaseAndObjectiveTriggers)");
                            OnObjectiveStatusChanged?.Invoke(caseData, objDef, oldObjStatus, newObjStatus);
                            anyObjectiveStateChangedInThisPass = true;
                        }
                    }
                }
                CheckCaseOutcomeConditions(caseID, caseData);
                if(anyObjectiveStateChangedInThisPass) EvaluateCaseAndObjectiveTriggers();
            }
        }
    }

    public void SetCaseOverallStatus(string caseID, CaseOverallStatus newStatus, CaseData caseDefinitionIfKnown = null)
    {
        if (string.IsNullOrEmpty(caseID)) return;
        string cleanCaseID = caseID.Trim();
        CaseData caseDef = caseDefinitionIfKnown ?? GetCaseDataByID(cleanCaseID);
        if (!CaseProgress.ContainsKey(cleanCaseID)) CaseProgress[cleanCaseID] = new Dictionary<string, object>();
        CaseOverallStatus oldStatus = GetCaseOverallStatus(cleanCaseID);
        if (oldStatus == newStatus && CaseProgress[cleanCaseID].ContainsKey("OverallStatus")) return;

        CaseProgress[cleanCaseID]["OverallStatus"] = newStatus;
        Debug.Log($"Case '{(caseDef?.CaseName ?? cleanCaseID)}' OverallStatus: {oldStatus} -> {newStatus}");
        OnCaseOverallStatusChanged?.Invoke(caseDef, oldStatus, newStatus);

        if (caseDef != null) {
            if (newStatus == CaseOverallStatus.Successful) ProcessRewardSet(caseDef.RewardsOnSuccess);
            else if (newStatus == CaseOverallStatus.Failed) ProcessRewardSet(caseDef.RewardsOnFailure);
        }
        EvaluateCaseAndObjectiveTriggers();
    }

    public CaseOverallStatus GetCaseOverallStatus(string caseID)
    {
        if (string.IsNullOrEmpty(caseID)) return CaseOverallStatus.Unavailable;
        string cleanCaseID = caseID.Trim();
        if (CaseProgress.TryGetValue(cleanCaseID, out var caseDetails) && caseDetails.TryGetValue("OverallStatus", out var statusObj) && statusObj is CaseOverallStatus status) {
            return status;
        }
        return CaseOverallStatus.Unavailable;
    }

    public void SetObjectiveStatus(string caseID, string objectiveID, ObjectiveStatus newStatus)
    {
        if (string.IsNullOrEmpty(caseID) || string.IsNullOrEmpty(objectiveID)) return;
        string cleanCaseID = caseID.Trim(); string cleanObjectiveID = objectiveID.Trim();

        if (!CaseProgress.ContainsKey(cleanCaseID)) {
            Debug.LogWarning($"SetObjectiveStatus: CaseID '{cleanCaseID}' not in CaseProgress. Attempting to find/activate for objective '{cleanObjectiveID}'.");
            CaseData caseToStart = GetCaseDataByID(cleanCaseID);
            if (caseToStart != null) {
                CaseOverallStatus currentCaseStatus = GetCaseOverallStatus(cleanCaseID);
                if (currentCaseStatus == CaseOverallStatus.Unavailable && AreConditionsMet(caseToStart.MakeAvailable_Conditions, cleanCaseID)) {
                    SetCaseOverallStatus(cleanCaseID, CaseOverallStatus.Inactive, caseToStart);
                } else if (currentCaseStatus == CaseOverallStatus.Inactive && AreConditionsMet(caseToStart.StartCase_Conditions, cleanCaseID)) {
                     Internal_StartCaseLogic(cleanCaseID, caseToStart);
                }
            }
            if (!CaseProgress.ContainsKey(cleanCaseID)) {
                Debug.LogError($"SetObjectiveStatus: Case '{cleanCaseID}' still not in progress for objective '{cleanObjectiveID}'. Status not set."); return;
            }
        }

        string key = "ObjectiveStatus_" + cleanObjectiveID;
        ObjectiveStatus oldStatus = GetObjectiveStatus(cleanCaseID, cleanObjectiveID);
        if (oldStatus == newStatus && CaseProgress[cleanCaseID].ContainsKey(key)) return;

        CaseProgress[cleanCaseID][key] = newStatus;
        Debug.Log($"Objective '{cleanObjectiveID}' in Case '{cleanCaseID}' status: {oldStatus} -> {newStatus}");

        CaseData caseDef = GetCaseDataByID(cleanCaseID);
        CaseObjectiveDefinition objDef = caseDef != null ? GetObjectiveDefinition(caseDef, cleanObjectiveID) : null;
        OnObjectiveStatusChanged?.Invoke(caseDef, objDef, oldStatus, newStatus);

        if (caseDef != null) {
            if (newStatus == ObjectiveStatus.Completed) {
                if (caseDef.RewardsForOptionalObjectiveCompletion != null) {
                    foreach(var optRewardEntry in caseDef.RewardsForOptionalObjectiveCompletion) {
                        if (optRewardEntry != null && !string.IsNullOrEmpty(optRewardEntry.ObjectiveID) && optRewardEntry.ObjectiveID.Trim() == cleanObjectiveID) {
                            ProcessRewardSet(optRewardEntry.Rewards); break;
                        }
                    }
                }
                CheckCaseOutcomeConditions(cleanCaseID, caseDef);
            }
        }
        EvaluateCaseAndObjectiveTriggers();
    }

    public ObjectiveStatus GetObjectiveStatus(string caseID, string objectiveID)
    {
        if (string.IsNullOrEmpty(caseID) || string.IsNullOrEmpty(objectiveID)) return ObjectiveStatus.Inactive;
        string cleanCaseID = caseID.Trim(); string cleanObjectiveID = objectiveID.Trim();
        if (CaseProgress.TryGetValue(cleanCaseID, out var caseDetails) && caseDetails.TryGetValue("ObjectiveStatus_" + cleanObjectiveID, out var statusObj) && statusObj is ObjectiveStatus status) {
            return status;
        }
        return ObjectiveStatus.Inactive;
    }

    public void UpdateObjectiveFromClue(string caseID, string objectiveID, string caseSpecificFlagToSet)
    {
        if (!s_isInitialized) { Debug.LogError("UpdateObjectiveFromClue called before GameStateManager is initialized!"); return; }
        if (string.IsNullOrEmpty(caseID)) { Debug.LogError("UpdateObjectiveFromClue: caseID is null or empty!"); return; }
        string cleanCaseID = caseID.Trim();

        if (!string.IsNullOrEmpty(objectiveID)) {
            string cleanObjectiveID = objectiveID.Trim();
            Debug.Log($"UpdateObjectiveFromClue: Attempting to complete Objective '{cleanObjectiveID}' for Case '{cleanCaseID}'.");
            SetObjectiveStatus(cleanCaseID, cleanObjectiveID, ObjectiveStatus.Completed);
        }
        if (!string.IsNullOrEmpty(caseSpecificFlagToSet)) {
            string cleanFlag = caseSpecificFlagToSet.Trim();
            Debug.Log($"UpdateObjectiveFromClue: Attempting to set CaseSpecificFlag '{cleanFlag}' for Case '{cleanCaseID}'.");
            SetCaseSpecificFlag(cleanCaseID, cleanFlag, true);
        }
    }

    public void SetCaseSpecificFlag(string caseID, string flagName, bool value)
    {
        if (!s_isInitialized) { Debug.LogError("SetCaseSpecificFlag called before GameStateManager is initialized!"); return; }
        if (string.IsNullOrEmpty(caseID) || string.IsNullOrEmpty(flagName)) return;
        string cleanCaseID = caseID.Trim(); string cleanFlagName = flagName.Trim();

        if (!CaseProgress.ContainsKey(cleanCaseID)) {
            Debug.LogWarning($"SetCaseSpecificFlag: CaseID '{cleanCaseID}' not found in CaseProgress. Creating new entry to set flag '{cleanFlagName}'.");
            CaseProgress[cleanCaseID] = new Dictionary<string, object>();
            if (caseDataRegistry.ContainsKey(cleanCaseID) && !CaseProgress[cleanCaseID].ContainsKey("OverallStatus")) {
                CaseProgress[cleanCaseID]["OverallStatus"] = CaseOverallStatus.Unavailable;
            }
        }
        CaseProgress[cleanCaseID][cleanFlagName] = value;
        Debug.Log($"Case '{cleanCaseID}', Specific Flag '{cleanFlagName}' set to {value}. Evaluating case triggers.");
        EvaluateCaseAndObjectiveTriggers();
    }

    public bool IsCaseSpecificFlagTrue(string caseID, string flagName)
    {
        if (string.IsNullOrEmpty(caseID) || string.IsNullOrEmpty(flagName)) return false;
        string cleanCaseID = caseID.Trim(); string cleanFlagName = flagName.Trim();
        if (CaseProgress.TryGetValue(cleanCaseID, out var caseDetails) && caseDetails.TryGetValue(cleanFlagName, out var valueObj) && valueObj is bool value) {
            return value;
        }
        return false;
    }

    public bool AreConditionsMet(List<TriggerCondition> conditions, string currentCaseIDForContext, string currentObjectiveIDForContext = null)
    {
        if (!s_isInitialized && (conditions != null && conditions.Count > 0) ) {
             Debug.LogWarning("AreConditionsMet called before GameStateManager initialized AND there are conditions to check. Returning false as a precaution.");
             return false;
        }
        if (conditions == null || conditions.Count == 0) return true;
        string cleanContextCaseID = currentCaseIDForContext?.Trim();

        foreach (TriggerCondition condition in conditions) {
            if (condition == null) { Debug.LogWarning("Null condition found in list during AreConditionsMet."); continue; }
            string cleanStringParam = condition.StringParameterID?.Trim();
            bool conditionResult = false;
            switch (condition.Type) {
                case TriggerConditionType.FlagIsSet:
                    if (string.IsNullOrEmpty(cleanStringParam)) { Debug.LogWarning("FlagIsSet condition has empty StringParameterID."); continue; }
                    conditionResult = (CheckGlobalFlag(cleanStringParam) == condition.RequiredBoolState);
                    break;
                case TriggerConditionType.ObjectiveCompleted:
                    if (string.IsNullOrEmpty(cleanStringParam)) { Debug.LogWarning("ObjectiveCompleted condition has empty StringParameterID (ObjectiveID)."); continue; }
                    string targetCaseID_Obj = string.IsNullOrEmpty(condition.TargetCaseIDForCondition) ? cleanContextCaseID : condition.TargetCaseIDForCondition.Trim();
                    if (string.IsNullOrEmpty(targetCaseID_Obj)) { Debug.LogWarning("ObjectiveCompleted condition has no valid target CaseID."); continue; }
                    conditionResult = (GetObjectiveStatus(targetCaseID_Obj, cleanStringParam) == ObjectiveStatus.Completed) == condition.RequiredBoolState;
                    break;
                case TriggerConditionType.PlayerLevel:
                    Debug.LogWarning("PlayerLevel condition check not implemented."); conditionResult = true; break;
                case TriggerConditionType.CaseStatusIs:
                    string caseToCheckID = string.IsNullOrEmpty(cleanStringParam) ? cleanContextCaseID : cleanStringParam;
                     if (string.IsNullOrEmpty(caseToCheckID)) { Debug.LogWarning("CaseStatusIs condition has no valid CaseID to check."); continue; }
                    CaseOverallStatus statusOfCaseToCheck = GetCaseOverallStatus(caseToCheckID);
                    conditionResult = (statusOfCaseToCheck == (CaseOverallStatus)condition.IntParameter);
                    break;
                case TriggerConditionType.PlayerAcceptsQuestDialogue:
                    Debug.LogWarning("PlayerAcceptsQuestDialogue condition type is context-dependent and usually event-driven, not polled. Assuming true for now in this check."); conditionResult = true; break;
                default:
                    Debug.LogWarning($"Unknown TriggerConditionType: {condition.Type}");
                    break;
            }
            if (!conditionResult) return false;
        }
        return true;
    }

    private void CheckCaseOutcomeConditions(string caseID, CaseData caseDef)
    {
        if (caseDef == null) { Debug.LogError($"[CheckCaseOutcomeConditions] CaseData for '{caseID}' is null."); return; }
        string cleanCaseID = caseID.Trim();
        CaseOverallStatus currentOverallStatus = GetCaseOverallStatus(cleanCaseID);
        if (currentOverallStatus != CaseOverallStatus.InProgress) return;

        bool explicitFailure = false;
        if (caseDef.FailureConditions != null && caseDef.FailureConditions.Count > 0)
        {
            if (AreConditionsMet(caseDef.FailureConditions, cleanCaseID))
            {
                Debug.Log($"[CheckCaseOutcomeConditions] Case '{cleanCaseID}' - All defined FailureConditions ARE MET.");
                explicitFailure = true;
            }
        }

        if (explicitFailure)
        {
            SetCaseOverallStatus(cleanCaseID, CaseOverallStatus.Failed, caseDef);
            return;
        }

        bool successConditionsMet = false;
        if (caseDef.SuccessConditions != null && caseDef.SuccessConditions.Count > 0)
        {
             if (AreConditionsMet(caseDef.SuccessConditions, cleanCaseID))
             {
                successConditionsMet = true;
             }
        }
        else
        {
            successConditionsMet = true;
        }

        if (successConditionsMet) {
            bool allPrimaryObjectivesAreComplete = true;
            if (caseDef.Objectives != null && caseDef.Objectives.Count > 0) {
                foreach(var objDef in caseDef.Objectives) {
                    if (objDef != null && !string.IsNullOrEmpty(objDef.ObjectiveID) && !objDef.IsOptional) {
                        ObjectiveStatus objStatus = GetObjectiveStatus(cleanCaseID, objDef.ObjectiveID);
                        if (objStatus != ObjectiveStatus.Completed) {
                            allPrimaryObjectivesAreComplete = false;
                            Debug.Log($"[CheckCaseOutcomeConditions] Case '{cleanCaseID}' - Primary objective '{objDef.ObjectiveID}' is NOT COMPLETED (Status: {objStatus}).");
                            break;
                        }
                    }
                }
            }
            else {
                 Debug.LogWarning($"[CheckCaseOutcomeConditions] Case '{cleanCaseID}' has no objectives defined. If SuccessConditions were also empty, it will auto-succeed. This is considered success for this check if no objectives are primary.");
            }

            if(allPrimaryObjectivesAreComplete) {
                Debug.Log($"[CheckCaseOutcomeConditions] Case '{cleanCaseID}' - All primary objectives are complete AND success conditions (if any) were met. Setting to Successful.");
                SetCaseOverallStatus(cleanCaseID, CaseOverallStatus.Successful, caseDef);
            }
            else if (successConditionsMet) {
                 Debug.Log($"[CheckCaseOutcomeConditions] Case '{cleanCaseID}' met explicit SuccessConditions, but not all primary objectives complete. Remains InProgress.");
            }
        }
    }

    private void ProcessRewardSet(CaseOutcomeRewards rewardSet)
    {
        if (rewardSet == null) return;
        Debug.Log($"Processing Rewards: XP {rewardSet.Experience}");
        if (rewardSet.Reputation != null) {
            foreach (var repChange in rewardSet.Reputation) {
                 if (repChange != null && !string.IsNullOrEmpty(repChange.FactionID)) Debug.Log($"Reputation change for {repChange.FactionID}: {repChange.Change}");
            }
        }
        if (rewardSet.FlagsToSet != null) {
            foreach (string flag in rewardSet.FlagsToSet) if(!string.IsNullOrEmpty(flag)) SetGlobalFlag(flag.Trim(), true);
        }
        if (rewardSet.NewPartyMembers != null) {
            foreach (string memberID in rewardSet.NewPartyMembers) {
                if (!string.IsNullOrEmpty(memberID)) Debug.Log($"Adding new party member: {memberID.Trim()}");
            }
        }
    }
}