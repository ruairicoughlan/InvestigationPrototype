using UnityEngine;

// This attribute allows you to create instances of this data type
// directly in the Unity Project window: Right Click -> Create -> Project Dublin -> Data -> Witness Data
[CreateAssetMenu(fileName = "NewWitnessData", menuName = "Project Dublin/Data/Witness Data")]
public class WitnessData : ScriptableObject // Ensure it inherits from ScriptableObject
{
    [Header("Witness Identity")]
    [Tooltip("Reference to the CharacterProfileData ScriptableObject for this witness. This provides their name and portrait.")]
    public CharacterProfileData characterProfile; // This is the field InvestigationManager needs

    [Header("Dialogue Interaction")]
    [Tooltip("The ID of the dialogue graph/JSON file to load when this witness is interacted with (e.g., LibraryWitnesses, LorraineBrascoConvo). This refers to your .json.txt files in Resources/DialogueNodes.")]
    public string dialogueGraphID;

    [Tooltip("The specific nodeID within the dialogueGraphID to start the conversation from (e.g., Lorraine_InitialContact).")]
    public string startingNodeID;

    [Header("Availability & Behavior")]
    [Tooltip("Can the player talk to this witness multiple times? If false, the pop-up might disappear after one conversation, or the dialogue might change to reflect they've already spoken.")]
    public bool canBeRevisited = true;

    [Tooltip("Optional: A Global Flag ID from GameStateManager that must be TRUE for this witness to be available/visible. Leave empty if the witness is always available or their appearance is controlled by other scene logic.")]
    public string requiredGlobalFlag = "";

    // Future ideas for more complex witness behavior:
    // public bool hidePopupAfterFirstInteraction = false;
    // public string objectiveIDToRevealThisWitness; // Witness only appears after a certain objective is met
    // public GameStateManager.GameState stateToReturnToAfterDialogue = GameStateManager.GameState.Investigation; // If they can take you elsewhere
}