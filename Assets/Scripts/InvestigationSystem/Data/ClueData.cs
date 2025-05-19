using UnityEngine;
using System.Collections.Generic; // For List if you prefer over array for points

// Define ENUMs here to be globally accessible or within a namespace
public enum InvestigationSkillType { None, Perception, Lockpicking, Intelligence, Strength, Forensics }
public enum ClueColliderType { Box, Polygon }

[CreateAssetMenu(fileName = "NewClueData", menuName = "Project Dublin/Clue Data")]
public class ClueData : ScriptableObject
{
    [Header("Clue Identification")]
    public string clueID;
    public string clueName;

    [Header("Initial Discovery")]
    [Tooltip("Skill required to even make this clue interactable. Leave as 'None' if always interactable once placed.")]
    public InvestigationSkillType initialPerceptionSkill = InvestigationSkillType.Perception;
    [Tooltip("DC for the initialPerceptionSkill to make the clue interactable. 0 if no check needed or if skill is 'None'.")]
    public int initialPerceptionDC = 0;
    [Tooltip("Optional: If the clue is a physical object in the scene. Leave null if it's part of the background.")]
    public Sprite clueWorldSprite;
    [Tooltip("Position in the investigation scene (often localPosition or anchoredPosition).")]
    public Vector2 worldPosition;
    [Tooltip("Visual scale of the clue's GameObject when spawned. Default is (1,1), affecting transform.localScale.")]
    public Vector2 visualScale = Vector2.one;

    [Header("Collider Configuration")]
    [Tooltip("What shape of collider should this clue use? Box or Polygon.")]
    public ClueColliderType colliderType = ClueColliderType.Box;
    [Tooltip("Size for the BoxCollider2D (if colliderType is Box).")]
    public Vector2 boxColliderSize = new Vector2(100f, 100f);
    [Tooltip("Offset for the BoxCollider2D from the clue's worldPosition (if colliderType is Box).")]
    public Vector2 boxColliderOffset = Vector2.zero;
    [Tooltip("Points for the PolygonCollider2D (if colliderType is Polygon). Define at least 3 points.")]
    public Vector2[] polygonColliderPoints = new Vector2[] {
        new Vector2(-50f, -50f), new Vector2(50f, -50f), new Vector2(50f, 50f), new Vector2(-50f, 50f)
    };

    [Header("Interaction & Information")]
    [TextArea(3, 5)]
    public string baseDescription;
    public Sprite cluePopupImage;

    [Header("Skill Check for More Information (Optional)")]
    public bool requiresSkillCheckForMoreInfo = false;
    public InvestigationSkillType skillCheckType = InvestigationSkillType.None;
    public int skillCheckDC = 30;
    [TextArea(3, 5)]
    public string successDescription;
    [TextArea(3, 5)]
    public string failureDescription;

    [Header("Case Progression")]
    public bool isKeyEvidence = false;
    public string relatedCaseID;
    public string relatedObjectiveID;
    public string caseFlagToSet;

    // Fields from your previously provided version (if any were missed, re-add them here)
    // For example:
    // [Header("Audio (Optional)")]
    // public AudioClip discoverySound;
    // public bool allowMultipleInteractions = false;
}