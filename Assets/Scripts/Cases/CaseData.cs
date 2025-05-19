using UnityEngine;
using System.Collections.Generic;

// --- ENUMS needed by CaseData and GameStateManager ---
// Placed globally or in a shared namespace for accessibility
// (If you already have these from previous suggestions, ensure they match or update)

public enum CaseOverallStatus
{
    Unavailable,    // Not yet known to player or conditions not met to see it
    Inactive,       // Known to player, can be started (e.g., visible in a list)
    InProgress,     // Player has accepted/started the case
    Successful,     // Player has met all primary success conditions
    Failed          // Player has met a failure condition
}

public enum ObjectiveStatus
{
    Inactive,       // Objective is not yet active
    Active,         // Objective is currently active
    Completed,      // Objective has been successfully completed
    Failed          // Objective has been failed (optional)
}

public enum TriggerConditionType
{
    FlagIsSet,
    ObjectiveCompleted,   // Check status of an objective
    PlayerLevel,
    CaseStatusIs,         // Check status of this or another case
    PlayerAcceptsQuestDialogue // Symbolic, usually an event, but can be a check if a dialogue sequence was "won"
    // Add more types as your game logic requires: ItemPossessed, DialogueNodeVisited, etc.
}
// --- END ENUMS ---

[System.Serializable]
public class TriggerCondition // Represents a single condition in your JSON arrays
{
    public TriggerConditionType Type;

    [Tooltip("For FlagIsSet (FlagID), ObjectiveCompleted (ObjectiveID - assumes current case unless TargetCaseIDForCondition is set), CaseStatusIs (CaseID to check).")]
    public string StringParameterID;

    [Tooltip("For PlayerLevel (MinLevel), CaseStatusIs (target CaseOverallStatus enum as int for comparison if needed).")]
    public int IntParameter;

    [Tooltip("The expected boolean state for FlagIsSet or if ObjectiveCompleted means 'is completed' (true) or 'is not completed' (false).")]
    public bool RequiredBoolState = true;

    [Tooltip("(Optional) For ObjectiveCompleted or CaseStatusIs, if checking an objective/status of a DIFFERENT case than the one this condition belongs to.")]
    public string TargetCaseIDForCondition;
}

[System.Serializable]
public class CaseLogEntry
{
    public string EntryID;
    [TextArea(3, 7)]
    public string Text;
    [Tooltip("Log entry is only considered for display if the case is in one of these overall statuses.")]
    public List<CaseOverallStatus> DisplayOnStatus = new List<CaseOverallStatus>();
    [Tooltip("ALL these additional conditions must be met for this log entry to actually be displayed.")]
    public List<TriggerCondition> TriggerToShow = new List<TriggerCondition>();
}

[System.Serializable]
public class CaseObjectiveDefinition
{
    public string ObjectiveID;
    [TextArea(2, 5)]
    public string Text; // Renamed from "Text" in your JSON to "ObjectiveText" for clarity if needed, but using "Text" to match JSON
    public bool IsOptional = false;

    [Tooltip("ALL conditions that must be met for this objective's status to become 'Active'.")]
    public List<TriggerCondition> TriggerToActivate = new List<TriggerCondition>();
    [Tooltip("ALL conditions that must be met for this objective's status to become 'Completed'.")]
    public List<TriggerCondition> TriggerToComplete = new List<TriggerCondition>();
    // Optional:
    // public List<TriggerCondition> TriggerToFail_Objective;
    // public List<string> FlagsToSetOnActivation = new List<string>();
    // public List<string> FlagsToSetOnCompletion = new List<string>();
}

[System.Serializable]
public class ReputationRewardEntry
{
    public string FactionID;
    public int Change;
}

[System.Serializable]
public class CaseOutcomeRewards // For "OnSuccess" / "OnFailure"
{
    public int Experience = 0;
    public List<ReputationRewardEntry> Reputation = new List<ReputationRewardEntry>();
    public List<string> NewPartyMembers = new List<string>(); // Was NewPartyMembers_IDs
    public List<string> FlagsToSet = new List<string>();
    // public List<ItemData> itemsToGrant; // If you have an ItemData SO
}

[System.Serializable]
public class OptionalObjectiveRewardEntry // For "ForOptionalObjectiveCompletion"
{
    public string ObjectiveID; // Which optional objective this reward is for
    public CaseOutcomeRewards Rewards; // Re-use CaseOutcomeRewards structure
}


[CreateAssetMenu(fileName = "NewCase", menuName = "Project Dublin/Cases/Case Definition")]
public class CaseData : ScriptableObject
{
    [Header("--- Core Case Identification ---")]
    public string CaseID;
    public string CaseName;
    // CaseStatus is dynamic, so not here. GameStateManager tracks current status.
    [Tooltip("CharacterProfileData ID of the NPC associated with this case (e.g., quest giver). From your UI Mockup.")]
    public string CaseProviderNPC_ID;


    [Header("--- Triggers for Case Status Changes ---")] // Matches "Triggers" object in JSON
    [Tooltip("ALL conditions must be met for case to become 'Inactive' (available to player).")]
    public List<TriggerCondition> MakeAvailable_Conditions; // Was Triggers.MakeAvailable
    [Tooltip("ALL conditions for case to become 'InProgress'. Often empty if started by a direct game event like dialogue choice ('PlayerAcceptsQuestDialogue' type).")]
    public List<TriggerCondition> StartCase_Conditions; // Was Triggers.StartCase

    // DetermineOutcome
    [Tooltip("ALL conditions that must be met for this case to be 'Successful'.")]
    public List<TriggerCondition> SuccessConditions;
    [Tooltip("If ANY of these conditions are met, this case may be considered 'Failed'.")]
    public List<TriggerCondition> FailureConditions;


    [Header("--- Case Story & Details ---")]
    [TextArea(3, 10)]
    [Tooltip("Initial overall description for the case log/UI. Your JSON had this in CaseLog[0] essentially.")]
    public string OverallCaseDescription; // This can be your "Log_Entry_Initial" if always present
    public List<CaseLogEntry> CaseLog;      // Matches your JSON "CaseLog"


    [Header("--- Case Objectives ---")]
    public List<CaseObjectiveDefinition> Objectives; // Matches your JSON "Objectives"


    [Header("--- Rewards & Consequences ---")] // Matches "Rewards" object in JSON
    public CaseOutcomeRewards RewardsOnSuccess;
    public CaseOutcomeRewards RewardsOnFailure;
    public List<OptionalObjectiveRewardEntry> RewardsForOptionalObjectiveCompletion;

    [Header("--- Optional Player Guidance ---")]
    public int RecommendedLevel = 1; // Matches your JSON
}