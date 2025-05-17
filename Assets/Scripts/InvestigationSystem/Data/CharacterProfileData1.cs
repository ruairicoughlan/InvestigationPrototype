using UnityEngine;

[CreateAssetMenu(fileName = "NewCharacterPlacement", menuName = "Project Dublin/Data/Character Placement Data")]
public class CharacterPlacementData : ScriptableObject
{
    [Tooltip("Reference to the CharacterProfileData ScriptableObject for this character.")]
    public CharacterProfileData characterProfile;

    [Header("Scene Presence")]
    [Tooltip("Position of this character within the panoramic investigation scene. (0,0) is usually bottom-left or center depending on your setup.")]
    public Vector2 scenePosition = Vector2.zero;

    [Tooltip("The sprite to display for this character in the investigation scene, if they are visually present. Can be different from their portrait or null if they are part of the main background image.")]
    public Sprite characterWorldSprite; // This could be the same as CharacterProfileData.worldSprite, or specific to this placement.

    [Tooltip("Initial scale of the character's sprite in the scene.")]
    public Vector2 initialScale = Vector2.one;

    [Tooltip("Is this character initially visible and interactable in the scene?")]
    public bool startsActive = true;

    [Header("Dialogue Interaction")]
    [Tooltip("The ID of the dialogue graph/JSON file to load when this character is interacted with (e.g., LibraryDialogue, DocksGuardConvo). This refers to your .json.txt files in Resources/DialogueNodes.")]
    public string dialogueGraphID; // e.g., "LibraryDialogue"

    [Tooltip("The specific nodeID within the dialogueGraphID to start the conversation from.")]
    public string startingNodeID; // e.g., "Librarian_Intro_01"

    // Future ideas:
    // public string characterStateAnimation; // e.g., "idle_nervous", "standing_guard"
    // public bool lookAtPlayer;
}