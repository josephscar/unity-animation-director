using System.Collections.Generic;
using UnityEngine;

namespace AnimationDirector
{
    /// <summary>
    /// Plays AnimationActionSequences in sync with an Animator's current clip.
    /// Supports multiple sequences - automatically selects the sequence that matches the currently playing animation.
    /// Checks the current frame each Update and executes any matching keyframes once per playthrough.
    /// </summary>
    [DisallowMultipleComponent]
    public class ActionSequencePlayer : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Animator animator;
        
        [Tooltip("List of animation action sequences. Each sequence should target a different AnimationClip. " +
                 "The player will automatically use the sequence that matches the currently playing animation.")]
        [SerializeField] private List<AnimationActionSequence> sequences = new List<AnimationActionSequence>();
        
        [Tooltip("(Legacy) Single sequence for backward compatibility. If set, it will be added to the sequences list.")]
        [SerializeField] private AnimationActionSequence sequence;

        [Header("Debug")]
        [SerializeField] private bool logActions;

        [Header("Target Bindings (for Enable/Disable)")]
        [SerializeField] private List<TargetBinding> targetBindings = new List<TargetBinding>();

        // Runtime tracking
        private AnimationClip _clip;
        private AnimationActionSequence _activeSequence; // Currently active sequence
        private int _lastFrame = -1;
        private int _lastLoopCount = 0;
        private readonly HashSet<int> _firedFrames = new HashSet<int>();
        private int _lastStateNameHash = 0; // Track which Animator state is active

        // For SpawnPrefab / DestroySpawned
        private readonly List<GameObject> _spawnedInstances = new List<GameObject>();

        // Buffer reused for per-frame lookup
        private readonly List<ActionKeyframe> _frameKeyframes = new List<ActionKeyframe>(8);

        // Cache for fast id -> GameObject lookup
        private Dictionary<string, GameObject> _bindingLookup;
        
        // Cache for fast clip -> sequence lookup
        private Dictionary<AnimationClip, AnimationActionSequence> _sequenceLookup;

        private void Reset()
        {
            animator = GetComponent<Animator>();
        }

        private void Awake()
        {
            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }

            // Migrate legacy single sequence to list if needed
            if (sequence != null && !sequences.Contains(sequence))
            {
                sequences.Add(sequence);
            }

            BuildSequenceLookup();
            BuildBindingLookup();
        }
        
        private void OnEnable()
        {
            // Rebuild lookups when component is enabled
            BuildSequenceLookup();
        }

        private void Update()
        {
            if (animator == null)
                return;

            // Find the sequence that matches the currently playing clip
            AnimationActionSequence matchingSequence = FindMatchingSequence();
            if (matchingSequence == null || matchingSequence.targetClip == null)
                return;

            // Update active sequence if it changed
            if (_activeSequence != matchingSequence)
            {
                _activeSequence = matchingSequence;
                EnsureClipCached();
            }

            if (_clip == null)
                return;

            var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            if (stateInfo.length <= 0f)
                return;

            // CRITICAL: Check if the Animator has transitioned to a different state.
            // This prevents firing keyframes when playing a different animation.
            int currentStateNameHash = stateInfo.fullPathHash;
            if (currentStateNameHash != _lastStateNameHash)
            {
                // State changed - reset everything
                _lastStateNameHash = currentStateNameHash;
                _lastFrame = -1;
                _lastLoopCount = 0;
                _firedFrames.Clear();
            }

            // Check if the currently playing clip actually matches our target clip.
            // We can't directly get the clip from AnimatorStateInfo, but we can check
            // if the state length matches our target clip length (close enough for V1).
            // If lengths don't match, this state is playing a different clip - don't execute.
            float lengthDiff = Mathf.Abs(stateInfo.length - _clip.length);
            if (lengthDiff > 0.01f) // Allow small floating point differences
            {
                // Different clip is playing - don't execute keyframes
                return;
            }

            // normalizedTime may be >1 for looping clips; track loop count.
            float normalizedTime = stateInfo.normalizedTime;
            int loopCount = Mathf.FloorToInt(normalizedTime);
            float t = Mathf.Repeat(normalizedTime, 1f);

            // Reset when clip loops, BUT only if the clip is actually set to loop.
            // For non-looping clips, normalizedTime will stay at 1.0+ after completion.
            // We only want to reset on actual loops, not when a non-looping clip finishes.
            bool isLooping = _clip.isLooping;
            if (isLooping && loopCount != _lastLoopCount)
            {
                // Clip looped - reset fired frames for next loop
                _lastLoopCount = loopCount;
                _lastFrame = -1;
                _firedFrames.Clear();
            }
            else if (!isLooping && normalizedTime >= 1.0f)
            {
                // Non-looping clip finished - don't reset, just stop executing
                // (firedFrames stays populated so keyframes won't fire again)
                return;
            }

            int totalFrames = Mathf.Max(1, Mathf.RoundToInt(_clip.length * _clip.frameRate));
            int currentFrame = Mathf.FloorToInt(t * totalFrames);
            currentFrame = Mathf.Clamp(currentFrame, 0, Mathf.Max(0, totalFrames - 1));

            if (currentFrame == _lastFrame)
                return;

            _lastFrame = currentFrame;

            // Fetch keyframes for this frame and execute any that haven't fired yet this playthrough.
            _activeSequence.GetKeyframesForFrame(currentFrame, _frameKeyframes);
            if (_frameKeyframes.Count == 0)
                return;

            for (int i = 0; i < _frameKeyframes.Count; i++)
            {
                var kf = _frameKeyframes[i];
                if (kf == null)
                    continue;

                // Skip if this frame already fired this playthrough
                if (_firedFrames.Contains(kf.frame))
                    continue;

                ExecuteKeyframe(kf);
                _firedFrames.Add(kf.frame);
            }
        }

        /// <summary>
        /// Finds the sequence that matches the currently playing animation clip.
        /// </summary>
        private AnimationActionSequence FindMatchingSequence()
        {
            if (animator == null)
                return null;

            var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            if (stateInfo.length <= 0f)
                return null;

            // Rebuild lookup if sequences list changed
            if (_sequenceLookup == null || _sequenceLookup.Count != sequences.Count)
            {
                BuildSequenceLookup();
            }

            // Find sequence whose target clip length matches the current state length
            // (We can't directly get the clip from AnimatorStateInfo, so we match by length)
            foreach (var seq in sequences)
            {
                if (seq == null || seq.targetClip == null)
                    continue;

                float lengthDiff = Mathf.Abs(stateInfo.length - seq.targetClip.length);
                if (lengthDiff <= 0.01f) // Allow small floating point differences
                {
                    return seq;
                }
            }

            return null;
        }

        private void BuildSequenceLookup()
        {
            if (_sequenceLookup == null)
            {
                _sequenceLookup = new Dictionary<AnimationClip, AnimationActionSequence>();
            }
            else
            {
                _sequenceLookup.Clear();
            }

            if (sequences == null)
                return;

            for (int i = 0; i < sequences.Count; i++)
            {
                var seq = sequences[i];
                if (seq == null || seq.targetClip == null)
                    continue;

                // If multiple sequences target the same clip, the last one wins
                _sequenceLookup[seq.targetClip] = seq;
            }
        }

        private void EnsureClipCached()
        {
            if (_activeSequence == null)
            {
                _clip = null;
                return;
            }

            if (_clip == _activeSequence.targetClip)
                return;

            _clip = _activeSequence.targetClip;
            _lastFrame = -1;
            _lastLoopCount = 0;
            _lastStateNameHash = 0; // Reset state tracking
            _firedFrames.Clear();
            _spawnedInstances.Clear();
            BuildBindingLookup();
        }

        private void BuildBindingLookup()
        {
            if (targetBindings == null)
            {
                _bindingLookup = null;
                return;
            }

            if (_bindingLookup == null)
            {
                _bindingLookup = new Dictionary<string, GameObject>();
            }
            else
            {
                _bindingLookup.Clear();
            }

            for (int i = 0; i < targetBindings.Count; i++)
            {
                var binding = targetBindings[i];
                if (binding == null)
                    continue;

                if (string.IsNullOrEmpty(binding.id) || binding.target == null)
                    continue;

                if (!_bindingLookup.ContainsKey(binding.id))
                {
                    _bindingLookup.Add(binding.id, binding.target);
                }
            }
        }

        private void ExecuteKeyframe(ActionKeyframe keyframe)
        {
            switch (keyframe.type)
            {
                case ActionType.SpawnPrefab:
                    HandleSpawnPrefab(keyframe);
                    break;

                case ActionType.PlaySound:
                    HandlePlaySound(keyframe);
                    break;

                case ActionType.EnableObject:
                    HandleToggleObject(keyframe, true);
                    break;

                case ActionType.DisableObject:
                    HandleToggleObject(keyframe, false);
                    break;

                case ActionType.DestroySpawned:
                    HandleDestroySpawned();
                    break;
            }
        }

        private void HandleSpawnPrefab(ActionKeyframe keyframe)
        {
            if (keyframe.prefab == null)
                return;

            Transform anchorTransform = transform;
            if (keyframe.anchor != null)
            {
                anchorTransform = keyframe.anchor.Resolve(transform);
            }

            GameObject instance;

            if (keyframe.parentToAnchor)
            {
                // Spawn as child of the anchor so it moves with it.
                instance = Instantiate(
                    keyframe.prefab,
                    anchorTransform.position,
                    anchorTransform.rotation,
                    anchorTransform);
            }
            else
            {
                // Spawn in world space (no parent) but at the anchor's position/rotation.
                instance = Instantiate(
                    keyframe.prefab,
                    anchorTransform.position,
                    anchorTransform.rotation);
            }

            _spawnedInstances.Add(instance);

            if (keyframe.lifetime > 0f)
            {
                Destroy(instance, keyframe.lifetime);
            }

            if (logActions)
            {
                Debug.Log($"[ActionSequencePlayer] Spawned prefab '{keyframe.prefab.name}' at frame {keyframe.frame} (parented={keyframe.parentToAnchor}).", this);
            }
        }

        private void HandlePlaySound(ActionKeyframe keyframe)
        {
            if (keyframe.sound == null)
                return;

            // Use PlayClipAtPoint for simplicity in V1.
            AudioSource.PlayClipAtPoint(keyframe.sound, transform.position);

            if (logActions)
            {
                Debug.Log($"[ActionSequencePlayer] Played sound '{keyframe.sound.name}' at frame {keyframe.frame}.", this);
            }
        }

        private void HandleToggleObject(ActionKeyframe keyframe, bool enabled)
        {
            GameObject target = null;

            // Prefer id-based binding so ScriptableObject does not need scene references.
            if (!string.IsNullOrEmpty(keyframe.targetId))
            {
                if (_bindingLookup == null)
                {
                    BuildBindingLookup();
                }

                if (_bindingLookup != null)
                {
                    _bindingLookup.TryGetValue(keyframe.targetId, out target);
                }
            }

            // Fallback: direct reference (works only for asset objects, not scene instances).
            if (target == null)
            {
                target = keyframe.targetObject;
            }

            if (target == null)
                return;

            target.SetActive(enabled);

            if (enabled && keyframe.detachOnEnable)
            {
                // Detach so it no longer follows its previous parent, but keep world position.
                target.transform.SetParent(null, true);
            }

            if (logActions)
            {
                Debug.Log($"[ActionSequencePlayer] Set '{target.name}' active={enabled} at frame {keyframe.frame} (detachOnEnable={keyframe.detachOnEnable}).", this);
            }
        }

        private void HandleDestroySpawned()
        {
            for (int i = 0; i < _spawnedInstances.Count; i++)
            {
                var go = _spawnedInstances[i];
                if (go != null)
                {
                    Destroy(go);
                }
            }

            _spawnedInstances.Clear();

            if (logActions)
            {
                Debug.Log("[ActionSequencePlayer] Destroyed all spawned instances.", this);
            }
        }

        /// <summary>
        /// Assigns the Animator and sequence at runtime if needed.
        /// (Legacy method - use AddSequence or SetSequences instead)
        /// </summary>
        public void SetData(Animator targetAnimator, AnimationActionSequence actionSequence)
        {
            animator = targetAnimator;
            if (actionSequence != null && !sequences.Contains(actionSequence))
            {
                sequences.Add(actionSequence);
                BuildSequenceLookup();
            }
        }
        
        /// <summary>
        /// Adds a sequence to the list. If a sequence for the same clip already exists, it will be replaced.
        /// </summary>
        public void AddSequence(AnimationActionSequence sequence)
        {
            if (sequence == null)
                return;

            // Remove existing sequence for the same clip if it exists
            for (int i = sequences.Count - 1; i >= 0; i--)
            {
                if (sequences[i] != null && sequences[i].targetClip == sequence.targetClip)
                {
                    sequences.RemoveAt(i);
                }
            }

            sequences.Add(sequence);
            BuildSequenceLookup();
        }
        
        /// <summary>
        /// Sets the entire sequences list at runtime.
        /// </summary>
        public void SetSequences(List<AnimationActionSequence> newSequences)
        {
            sequences = newSequences ?? new List<AnimationActionSequence>();
            BuildSequenceLookup();
        }

        [System.Serializable]
        public class TargetBinding
        {
            public string id;
            public GameObject target;
        }
    }
}


