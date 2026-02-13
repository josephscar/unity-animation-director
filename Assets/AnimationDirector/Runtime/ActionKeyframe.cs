using System;
using UnityEngine;

namespace AnimationDirector
{
    /// <summary>
    /// A single action that should fire on a specific frame of an AnimationClip.
    /// </summary>
    [Serializable]
    public class ActionKeyframe
    {
        [Min(0)]
        public int frame;

        public ActionType type;

        [Header("Spawn Prefab")]
        public GameObject prefab;
        public TransformAnchor anchor;
        [Tooltip("Optional lifetime for spawned prefab. If <= 0, it will not be auto-destroyed.")]
        public float lifetime;

        [Header("Play Sound")]
        public AudioClip sound;

        [Header("Enable / Disable Object")]
        public GameObject targetObject;
    }
}


