using UnityEngine;

[CreateAssetMenu(fileName = "NewCharacterProfile", menuName = "Project Dublin/Data/Character Profile")]
public class CharacterProfileData : ScriptableObject
{
    [Tooltip("The unique system ID for this character, e.g., npc_librarian, player_heroName. This should match the 'speaker' ID in DialogueNodes.")]
    public string characterID; // This is crucial for linking

    [Tooltip("The display name for the character shown in UI elements.")]
    public string displayName = "Character Name";

    [Tooltip("Portrait image for this character (used in dialogue UI, witness pop-ups, etc.).")]
    public Sprite portraitImage;

    [Tooltip("Optional: Full body or scene sprite if different from portrait, for showing in investigation scenes if they are not part of the background.")]
    public Sprite worldSprite; // Sprite to display in the investigation scene if needed

    // Future potential additions:
    // public string shortBio;
    // public Faction characterFaction;
    // public List<CharacterTrait> traits;
}