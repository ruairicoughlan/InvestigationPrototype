using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using UnityEngine.Events; // Required for UnityEvent

public class GameStateManager : MonoBehaviour
{
    // Singleton instance
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
                    Debug.Log("GameStateManager instance created.");
                }
            }
            return _instance;
        }
    }

    public enum GameState
    {
        MainMenu,
        Map,
        Investigation,
        Dialogue,
        Casing
    }

    [Header("Current State")]
    [SerializeField]
    private GameState currentGameState = GameState.MainMenu;
    public GameState CurrentGameState => currentGameState;

    [System.Serializable]
    public class GameStateChangedEvent : UnityEvent<GameState, object> { }
    public GameStateChangedEvent OnGameStateChanged = new GameStateChangedEvent();

    [Header("Scene Configuration")]
    public string mainMenuSceneName = "MainMenuScene";
    public string mapSceneName = "MapScene";
    public string investigationSceneName = "InvestigationScene";
    public string dialogueSceneName = "DialogueScene";
    public string casingSceneName = "CasingScene";

    [Header("Player Profile & Progress")]
    public string playerBackground = "Default";
    [Range(-1f, 1f)]
    public float policeReputationModifier = 0f;
    public float PoliceReputationModifier => policeReputationModifier;

    // --- DEBUG: Temporary Skill Overrides for Testing ---
    [Header("Debug Skill Overrides (For Testing)")]
    public int debugPerception = 30;
    public int debugLockpicking = 15;
    public int debugIntimidation = 20;
    public int debugPersuasion = 25;
    public int debugStreetwise = 10;
    // --- End Debug Skill Overrides ---

    public Dictionary<string, int> PlayerSkills { get; private set; } = new Dictionary<string, int>();
    public Dictionary<string, Dictionary<string, object>> CaseProgress { get; private set; } = new Dictionary<string, Dictionary<string, object>>();
    private object currentStateData = null;

    // ---- Fields from old GameStateManager (from dialogue prototype) ----
    public HashSet<string> VisitedNodeIds { get; private set; } = new HashSet<string>();
    public Dictionary<string, bool> GlobalFlags { get; private set; } = new Dictionary<string, bool>();
    // CaseStatuses, CompletedCaseObjectives, CaseFlags are now part of CaseProgress or can be integrated if needed differently
    // For simplicity, I'm assuming CaseProgress will hold most case-specific data.
    // If you need the exact structure from your old GSM for these, we can add them back.

    protected virtual void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeDefaultState();
            Debug.Log("GameStateManager Awake: Instance set and marked DontDestroyOnLoad.");
        }
        else if (_instance != this)
        {
            Debug.LogWarning("GameStateManager Awake: Another instance already exists. Destroying this one.");
            Destroy(gameObject);
            return;
        }
    }

    public virtual void InitializeDefaultState()
    {
        Debug.Log("GameStateManager: Initializing Default State using debug values if set.");
        PlayerSkills.Clear();
        PlayerSkills.Add("Perception", debugPerception);
        PlayerSkills.Add("Lockpicking", debugLockpicking);
        PlayerSkills.Add("Intimidation", debugIntimidation);
        PlayerSkills.Add("Persuasion", debugPersuasion);
        PlayerSkills.Add("Streetwise", debugStreetwise);
        // Add other default skills or use debug overrides as needed

        CaseProgress.Clear();
        VisitedNodeIds.Clear();
        GlobalFlags.Clear();
        playerBackground = "Default"; // Reset or load
        policeReputationModifier = 0f; // Reset or load
    }

    public void SwitchState(GameState newState, object dataToPass = null)
    {
        if (currentGameState == newState && currentStateData == dataToPass && SceneManager.GetActiveScene().name == GetSceneNameForState(newState))
        {
             Debug.Log($"GameStateManager: Already in state {newState} with the same data and scene. Re-invoking OnGameStateChanged for potential re-initialization.");
             OnGameStateChanged.Invoke(newState, dataToPass); // Allow re-trigger for re-initialization
             return;
        }

        Debug.Log($"GameStateManager: Switching from {currentGameState} to {newState}.");
        currentGameState = newState;
        currentStateData = dataToPass;

        string sceneToLoad = GetSceneNameForState(newState);

        if (!string.IsNullOrEmpty(sceneToLoad))
        {
            // Load the scene and then invoke the event.
            // Listeners in the new scene (if they subscribe in Awake) should generally be ready by the time the event fires
            // if the event is invoked *after* LoadScene is called and the scene is loaded.
            // For more complex scenarios or immediate reaction, consider a loading screen or a two-step event process.
            SceneManager.LoadScene(sceneToLoad);
            // It's often better to let systems in the newly loaded scene react in their Start/Awake by checking GameStateManager.Instance.CurrentGameState
            // and GameStateManager.Instance.GetCurrentStateData().
            // However, invoking it here allows persistent managers to react immediately.
            OnGameStateChanged.Invoke(newState, dataToPass);
        }
        else
        {
            Debug.LogWarning($"GameStateManager: No scene configured for state {newState}. State changed but no scene loaded.");
            OnGameStateChanged.Invoke(newState, dataToPass);
        }
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
            default:
                Debug.LogWarning($"No scene name defined for game state: {state}");
                return null;
        }
    }

    public int GetSkillLevel(string skillName)
    {
        if (PlayerSkills.TryGetValue(skillName, out int level))
        {
            return level;
        }
        return 0;
    }

    public void SetSkillLevel(string skillName, int newLevel)
    {
        PlayerSkills[skillName] = newLevel; // Adds if not present, updates if present
        Debug.Log($"Player skill '{skillName}' set to {newLevel}.");
    }

    public void ModifySkillLevel(string skillName, int amount)
    {
        if (!PlayerSkills.ContainsKey(skillName)) PlayerSkills[skillName] = 0;
        PlayerSkills[skillName] += amount;
        Debug.Log($"Skill '{skillName}' modified by {amount}. New level: {PlayerSkills[skillName]}");
    }

    public void SetCaseFlag(string caseID, string flagName, bool value)
    {
        if (!CaseProgress.ContainsKey(caseID))
        {
            CaseProgress[caseID] = new Dictionary<string, object>();
        }
        CaseProgress[caseID][flagName] = value;
        Debug.Log($"Case '{caseID}', Flag '{flagName}' set to {value}.");
    }

    public bool IsCaseFlagTrue(string caseID, string flagName)
    {
        if (CaseProgress.TryGetValue(caseID, out var flags))
        {
            if (flags.TryGetValue(flagName, out var value) && value is bool boolValue)
            {
                return boolValue;
            }
        }
        return false;
    }

    public void CompleteCaseObjective(string caseID, string objectiveID, bool completed = true)
    {
        SetCaseFlag(caseID, "Objective_" + objectiveID, completed);
    }

    public bool IsObjectiveComplete(string caseID, string objectiveID)
    {
        return IsCaseFlagTrue(caseID, "Objective_" + objectiveID);
    }
    
    // --- Visited Node and Global Flag methods from your existing script ---
    public void AddVisitedNode(string nodeId)
    {
        if (!VisitedNodeIds.Contains(nodeId))
        {
            VisitedNodeIds.Add(nodeId);
        }
    }

    public bool HasVisitedNode(string nodeId)
    {
        return VisitedNodeIds.Contains(nodeId);
    }

    public bool CheckGlobalFlag(string flagId)
    {
        return GlobalFlags.ContainsKey(flagId) && GlobalFlags[flagId];
    }

    public void SetGlobalFlag(string flagId, bool value)
    {
        GlobalFlags[flagId] = value;
        Debug.Log($"Global Flag '{flagId}' set to {value}");
    }


    public void SetCurrentStateData(object data)
    {
        currentStateData = data;
    }

    public T GetCurrentStateData<T>() where T : class
    {
        return currentStateData as T;
    }

    // Method for InvestigationManager to update police reputation
    public void UpdatePoliceReputation(float change)
    {
        policeReputationModifier += change;
        policeReputationModifier = Mathf.Clamp(policeReputationModifier, -1f, 1f); // Example clamp
        Debug.Log($"Police Reputation Modifier changed by {change}. New value: {PoliceReputationModifier}");
    }
}