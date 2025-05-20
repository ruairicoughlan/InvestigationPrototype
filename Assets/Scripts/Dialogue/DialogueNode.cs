using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class DialogueNode
{
    public string nodeId;
    public string speaker;
    public string listener;
    public string dialogueText;
    public string nextNodeId;
    public Vector2 position; // Position in the editor canvas
    
    // Skill Check Fields
    public bool requiresSkillCheck;
    public string requiredSkill;
    public int skillDifficulty;
    public string skillSuccessNodeId;
    public string skillFailureNodeId;

    // Prerequisites
    public List<string> requiresPreviousNodes;
    public string requiresPlayerBackground;
    public string requiresGlobalFlag;
    public Dictionary<string, string> casePrerequisites;

    // State Transition
    public bool isTransitionNode;
    public string transitionTargetState;
    public bool marksTransitionHint;

    // Options
    public bool isBadOption;

    // Actions
    [System.Serializable]
    public class DialogueAction
    {
        public string actionType; // e.g., "CompleteObjective", "SetFlag", etc.
        public string caseId;
        public string objectiveId;
        public string flagId;
        public bool flagValue;
    }
    public List<DialogueAction> actionsOnComplete;

    public DialogueNode()
    {
        requiresPreviousNodes = new List<string>();
        casePrerequisites = new Dictionary<string, string>();
        actionsOnComplete = new List<DialogueAction>();
        position = Vector2.zero;
    }
}
