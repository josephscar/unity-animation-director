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

        [Tooltip("If true, spawned prefab is parented to the anchor transform and moves with it. If false, it is spawned in world space (no parent).")]
        public bool parentToAnchor = true;

        [Tooltip("Optional lifetime for spawned prefab. If <= 0, it will not be auto-destroyed.")]
        public float lifetime;

        [Header("Play Sound")]
        public AudioClip sound;

        [Header("Enable / Disable Object")]
        [Tooltip("Optional direct asset reference. For scene objects, prefer Target Id and configure bindings on ActionSequencePlayer.")]
        public GameObject targetObject;

        [Tooltip("Logical id used to look up a scene object via ActionSequencePlayer bindings.")]
        public string targetId;

        [Tooltip("If true and this keyframe is enabling the object, it will be detached from its parent so it no longer follows that parent’s movement.")]
        public bool detachOnEnable;
    }
}


