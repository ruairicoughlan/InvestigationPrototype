using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewInvestigationSceneData", menuName = "Project Dublin/Investigation Scene Data")]
public class InvestigationSceneData : ScriptableObject
{
    [Header("Scene Visuals")]
    public Sprite backgroundImage; // The main panoramic image for the scene

    [Header("Interactable Elements")]
    public List<ClueData> cluesInScene; // List of all clues present in this scene
    public List<CharacterPlacementData> charactersInScene; // Characters physically present
    public List<WitnessData> availableWitnesses; // Witnesses available via pop-up

    [Header("Scene Mechanics")]
    public float basePoliceTimerSeconds = 120f; // Default time in seconds before police arrive
    [Tooltip("Optional: A thought the player character has upon entering this scene.")]
    public string entryCharacterThought;
    //  Future ideas: music track, ambient sounds, etc.
}