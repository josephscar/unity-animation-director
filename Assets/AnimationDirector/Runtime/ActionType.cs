using UnityEngine;

namespace AnimationDirector
{
    /// <summary>
    /// Types of actions that can be triggered by an animation keyframe.
    /// </summary>
    public enum ActionType
    {
        SpawnPrefab,
        PlaySound,
        EnableObject,
        DisableObject,
        DestroySpawned
    }
}


