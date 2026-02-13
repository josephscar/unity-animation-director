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
        [Tooltip("Optional direct asset reference. For scene objects, prefer Target Id and configure bindings on ActionSequencePlayer.")]
        public GameObject targetObject;

        [Tooltip("Logical id used to look up a scene object via ActionSequencePlayer bindings.")]
        public string targetId;
    }
}


