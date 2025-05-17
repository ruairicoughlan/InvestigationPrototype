using UnityEngine;
using System.Collections.Generic; // For potential future use, like multiple skill check options

// Enum to define what skill is needed for a check.
// You can expand this with any skills relevant to your game (e.g., Science, Streetwise, Technology).
public enum InvestigationSkillType
{
    None, // Use this if no specific skill is tied to the base interaction or further info
    Perception,
    Lockpicking,
    Intelligence, // Example: for deciphering or understanding complex clues
    Strength,     // Example: for moving an object to reveal a clue
    Forensics     // Example: for analyzing a bloodstain or fingerprints
    // Add more skills as your game design requires
}

[CreateAssetMenu(fileName = "NewClueData", menuName = "Project Dublin/Clue Data")]
public class ClueData : ScriptableObject
{
    [Header("Clue Identification")]
    public string clueID; // Unique ID for this clue, e.g., "Library_Skylight", "Docks_StrangeSymbol"
    public string clueName; // Player-facing name, e.g., "Scratched Skylight Pane", "Mysterious Symbol"

    [Header("Initial Discovery")]
    [Tooltip("Skill required to even make this clue interactable. Leave as 'None' if always interactable once placed.")]
    public InvestigationSkillType initialPerceptionSkill = InvestigationSkillType.Perception;
    [Tooltip("DC for the initialPerceptionSkill to make the clue interactable. 0 if no check needed or if skill is 'None'.")]
    public int initialPerceptionDC = 0;
    public Sprite clueWorldSprite; // Optional: If the clue is a physical object in the scene (like the golf card). Leave null if it's part of the background.
    public Vector2 worldPosition; // If it's a placed sprite, its position in the investigation scene.
    public Vector2 colliderSize = new Vector2(1,1); // Size of the clickable area for this clue.

    [Header("Interaction & Information")]
    [Tooltip("Text shown if the player fails the 'Skill Check for More Info' (if applicable), or the default/only text if no further skill check is needed.")]
    public string baseDescription;
    [Tooltip("Optional: Image to show in the information pop-up when this clue is inspected.")]
    public Sprite cluePopupImage;

    [Header("Skill Check for More Information (Optional)")]
    public bool requiresSkillCheckForMoreInfo = false;
    public InvestigationSkillType skillCheckType = InvestigationSkillType.None;
    public int skillCheckDC = 30; // The difficulty class for the skillCheckType
    [TextArea(3, 5)]
    [Tooltip("Additional text revealed if the skill check for more info is passed.")]
    public string successDescription; // Text if player passes the skillCheckForMoreInfo
    [TextArea(3, 5)]
    [Tooltip("Optional: Text revealed if the skill check for more info is FAILED. If empty, baseDescription is used.")]
    public string failureDescription; // Text if player fails skillCheckForMoreInfo (can be same as base or slightly less)

    [Header("Case Progression")]
    public bool isKeyEvidence = false; // Does this clue update the case file?
    [Tooltip("ID of the case this clue might update in GameStateManager.")]
    public string relatedCaseID;
    [Tooltip("ID of the objective this clue might complete or update.")]
    public string relatedObjectiveID;
    [Tooltip("Specific flag within the case to set if this clue is found (and isKeyEvidence is true).")]
    public string caseFlagToSet;


    // --- Helper methods could be added later, e.g. ---
    // public string GetInteractionText(int playerSkillLevel)
    // {
    //     if (requiresSkillCheckForMoreInfo)
    //     {
    //         // Compare playerSkillLevel (obtained from GameStateManager) with skillCheckDC
    //         // Return successDescription or failureDescription (or baseDescription)
    //     }
    //     return baseDescription;
    // }
}