using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System; // Added for Action delegate

/// <summary>
/// Manages the overall game state, including player progression,
/// skills, flags, case data, and handles transitions between game scenes/states.
/// This should be a Singleton and persist across scene loads.
/// </summary>
public class GameStateManager : MonoBehaviour
{
    public static GameStateManager Instance { get; private set; }

    public enum GameState
    {
        MainMenu, // Assuming you might have a main menu
        Map,
        Investigation,
        Dialogue,
        Casing // For the Casing System
        // Add other states like Combat, PauseMenu etc. as needed
    }

    public GameState CurrentState { get; private set; }
    private object _currentStateData; // To pass specific data to the new state

    // Event to notify other systems when the game state changes
    public event Action<GameState, object> OnGameStateChanged;

    // --- Core Game Data (from your dialogue prototype's GameStateManager) ---
    public Dictionary<string, int> PlayerSkills = new Dictionary<string, int>();
    public string PlayerBackground = "Default"; // Example: "StreetKid", "Detective", "Mystic"
    public HashSet<string> VisitedNodeIds = new HashSet<string>();
    public Dictionary<string, bool> GlobalFlags = new Dictionary<string, bool>();
    public Dictionary<string, string> CaseStatuses = new Dictionary<string, string>(); // e.g., <caseId, "NotStarted" / "Active" / "Completed" / "Failed">
    public Dictionary<string, HashSet<string>> CompletedCaseObjectives = new Dictionary<string, HashSet<string>>();
    public Dictionary<string, Dictionary<string, bool>> CaseFlags = new Dictionary<string, Dictionary<string, bool>>(); // For case-specific flags

    // --- Investigation Specific Data (Can be expanded) ---
    // Example: -0.25f for Negative Rank 1, 0.25f for Positive Rank 1
    // This will be used to modify the base police timer in investigation scenes.
    public float PoliceReputationModifier = 0f;

    // --- Scene Name Configuration (Makes it easier to manage scene names) ---
    public string MainMenuSceneName = "MainMenuScene"; // Example scene name
    public string MapSceneName = "MapScene";
    public string InvestigationSceneName = "InvestigationScene";
    public string DialogueSceneName = "DialogueScene";
    public string CasingSceneName = "CasingScene"; // Example scene name for Casing System

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Another instance of GameStateManager detected. Destroying this new one.");
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Make this object persist across scene loads
            InitializeDefaultState(); // Initialize or load game data
            Debug.Log("GameStateManager Initialized and Persisting.");
        }
    }

    void InitializeDefaultState()
    {
        // Clear and set up default values. In a real game, you'd load saved data here if it exists.
        PlayerSkills.Clear();
        VisitedNodeIds.Clear();
        GlobalFlags.Clear();
        CaseStatuses.Clear();
        CompletedCaseObjectives.Clear();
        CaseFlags.Clear();
        PlayerBackground = "Default";
        PoliceReputationModifier = 0f;

        // --- Example Starting Player Skills (Customize as needed) ---
        PlayerSkills.Add("Perception", 30);
        PlayerSkills.Add("Lockpicking", 15);
        PlayerSkills.Add("Persuasion", 25);
        PlayerSkills.Add("Intimidation", 20);
        PlayerSkills.Add("Streetwise", 20);
        // Add any other skills your game will use

        Debug.Log("Default Game State Initialized (Skills, Flags, Cases reset).");
    }

    /// <summary>
    /// Switches the game to a new state and loads the appropriate scene.
    /// </summary>
    /// <param name="newState">The state to switch to.</param>
    /// <param name="dataToPass">Optional data to pass to the new state (e.g., InvestigationSceneData, DialogueData).</param>
    public void SwitchState(GameState newState, object dataToPass = null)
    {
        if (CurrentState == newState && _currentStateData == dataToPass && SceneManager.GetActiveScene().name == GetSceneNameForState(newState))
        {
            Debug.LogWarning($"Attempting to switch to the same state ({newState}) with the same data and scene already loaded. Aborting switch.");
            return;
        }

        Debug.Log($"Switching from state: {CurrentState} to state: {newState}");
        CurrentState = newState;
        _currentStateData = dataToPass; // Store the data for the new state

        OnGameStateChanged?.Invoke(newState, _currentStateData); // Notify listeners

        string sceneToLoad = GetSceneNameForState(newState);

        if (!string.IsNullOrEmpty(sceneToLoad))
        {
            if (SceneManager.GetActiveScene().name != sceneToLoad)
            {
                SceneManager.LoadScene(sceneToLoad);
                Debug.Log($"Loaded scene: {sceneToLoad} for state: {newState}");
            }
            else
            {
                Debug.Log($"Scene {sceneToLoad} is already active. Not reloading, but state logic will proceed.");
            }
        }
        else
        {
            Debug.LogError($"No scene defined for state: {newState}");
        }
    }

    /// <summary>
    /// Gets the scene name associated with a game state.
    /// </summary>
    private string GetSceneNameForState(GameState state)
    {
        switch (state)
        {
            case GameState.MainMenu:
                return MainMenuSceneName;
            case GameState.Map:
                return MapSceneName;
            case GameState.Investigation:
                return InvestigationSceneName;
            case GameState.Dialogue:
                return DialogueSceneName;
            case GameState.Casing:
                return CasingSceneName;
            default:
                Debug.LogError($"Scene name not defined for GameState: {state}");
                return null;
        }
    }

    /// <summary>
    /// Retrieves the data passed to the current state.
    /// Call this in the Awake() or Start() of scripts in the newly loaded scene.
    /// </summary>
    public T GetCurrentStateData<T>() where T : class
    {
        return _currentStateData as T;
    }

    // --- Methods to GET and SET game state data (from your dialogue prototype's GameStateManager) ---

    public bool CheckGlobalFlag(string flagId)
    {
        return GlobalFlags.ContainsKey(flagId) && GlobalFlags[flagId];
    }

    public void SetGlobalFlag(string flagId, bool value)
    {
        GlobalFlags[flagId] = value;
        Debug.Log($"Flag '{flagId}' set to {value}");
    }

    public string GetCaseStatus(string caseId)
    {
        return CaseStatuses.ContainsKey(caseId) ? CaseStatuses[caseId] : "NotStarted";
    }

    public void SetCaseStatus(string caseId, string status)
    {
        CaseStatuses[caseId] = status;
        Debug.Log($"Case '{caseId}' status set to {status}");
    }

    public bool CheckCaseFlag(string caseId, string flagId)
    {
        return CaseFlags.ContainsKey(caseId) && CaseFlags[caseId].ContainsKey(flagId) && CaseFlags[caseId][flagId];
    }

    public void SetCaseFlag(string caseId, string flagId, bool value)
    {
        if (!CaseFlags.ContainsKey(caseId))
        {
            CaseFlags[caseId] = new Dictionary<string, bool>();
        }
        CaseFlags[caseId][flagId] = value;
        Debug.Log($"Case Flag '{caseId}/{flagId}' set to {value}");
    }

    public void CompleteCaseObjective(string caseId, string objectiveId)
    {
        if (!CompletedCaseObjectives.ContainsKey(caseId))
        {
            CompletedCaseObjectives[caseId] = new HashSet<string>();
        }
        if (CompletedCaseObjectives[caseId].Add(objectiveId))
        {
            Debug.Log($"Objective '{objectiveId}' for case '{caseId}' completed.");
        }
    }

    public bool HasCompletedCaseObjective(string caseId, string objectiveId)
    {
        return CompletedCaseObjectives.ContainsKey(caseId) && CompletedCaseObjectives[caseId].Contains(objectiveId);
    }

    public int GetSkillLevel(string skillName)
    {
        return PlayerSkills.ContainsKey(skillName) ? PlayerSkills[skillName] : 0;
    }

    public void SetSkillLevel(string skillName, int level)
    {
        PlayerSkills[skillName] = level;
        Debug.Log($"Skill '{skillName}' set to level {level}");
    }
    public void ModifySkillLevel(string skillName, int amount)
    {
        if (!PlayerSkills.ContainsKey(skillName)) PlayerSkills[skillName] = 0;
        PlayerSkills[skillName] += amount;
        Debug.Log($"Skill '{skillName}' modified by {amount}. New level: {PlayerSkills[skillName]}");
    }


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

    public void SetPlayerBackground(string background)
    {
        PlayerBackground = background;
        Debug.Log($"Player background set to: {background}");
    }

    // Example method for investigation system to update police reputation
    public void UpdatePoliceReputation(float change)
    {
        PoliceReputationModifier += change;
        // You might want to clamp this value to a min/max range
        Debug.Log($"Police Reputation Modifier changed by {change}. New value: {PoliceReputationModifier}");
    }
}