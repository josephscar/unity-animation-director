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
        private readonly HashSet<ActionKeyframe> _firedKeyframes = new HashSet<ActionKeyframe>();
        private int _lastStateNameHash = 0; // Track which Animator state is active
        private float _lastNormalizedTime = -1f; // Track time to detect replays without state changes

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

            // Find the actual clip Unity is playing (handles blend trees and retimed states).
            if (!TryGetCurrentClip(out var currentClip))
                return;

            // Find the sequence that matches the currently playing clip
            AnimationActionSequence matchingSequence = FindMatchingSequence(currentClip);
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
                _lastNormalizedTime = -1f;
                _firedKeyframes.Clear();
            }

            // If Unity is currently playing a different clip (e.g., blend tree), don't execute.
            if (currentClip != _clip)
                return;

            // normalizedTime may be >1 for looping clips; track loop count.
            float normalizedTime = stateInfo.normalizedTime;
            int loopCount = Mathf.FloorToInt(normalizedTime);
            
            // Detect replays without a state change (e.g., re-entering the same state)
            if (_lastNormalizedTime >= 0f && normalizedTime < _lastNormalizedTime)
            {
                _lastFrame = -1;
                _lastLoopCount = 0;
                _firedKeyframes.Clear();
            }
            _lastNormalizedTime = normalizedTime;

            // Reset when clip loops, BUT only if the clip is actually set to loop.
            // For non-looping clips, normalizedTime will stay at 1.0+ after completion.
            // We only want to reset on actual loops, not when a non-looping clip finishes.
            bool isLooping = stateInfo.loop;
            if (isLooping && loopCount != _lastLoopCount)
            {
                // Clip looped - reset fired frames for next loop
                _lastLoopCount = loopCount;
                _lastFrame = -1;
                _firedKeyframes.Clear();
            }

            float t = isLooping ? Mathf.Repeat(normalizedTime, 1f) : Mathf.Clamp01(normalizedTime);

            int totalFrames = Mathf.Max(1, Mathf.RoundToInt(_clip.length * _clip.frameRate));
            int currentFrame = Mathf.FloorToInt(t * totalFrames);
            currentFrame = Mathf.Clamp(currentFrame, 0, Mathf.Max(0, totalFrames - 1));

            if (currentFrame == _lastFrame)
                return;

            int startFrame = _lastFrame < 0 ? 0 : _lastFrame + 1;
            int endFrame = currentFrame;

            if (endFrame < startFrame)
            {
                // Time jumped backwards without a reset; restart from frame 0.
                _firedKeyframes.Clear();
                startFrame = 0;
            }

            for (int frame = startFrame; frame <= endFrame; frame++)
            {
                // Fetch keyframes for this frame and execute any that haven't fired yet this playthrough.
                _activeSequence.GetKeyframesForFrame(frame, _frameKeyframes);
                if (_frameKeyframes.Count == 0)
                    continue;

                for (int i = 0; i < _frameKeyframes.Count; i++)
                {
                    var kf = _frameKeyframes[i];
                    if (kf == null)
                        continue;

                    // Skip if this keyframe already fired this playthrough
                    if (_firedKeyframes.Contains(kf))
                        continue;

                    ExecuteKeyframe(kf);
                    _firedKeyframes.Add(kf);
                }
            }

            _lastFrame = currentFrame;
        }

        /// <summary>
        /// Finds the sequence that matches the currently playing animation clip.
        /// </summary>
        private AnimationActionSequence FindMatchingSequence(AnimationClip currentClip)
        {
            if (currentClip == null)
                return null;

            // Rebuild lookup if sequences list changed
            if (_sequenceLookup == null || _sequenceLookup.Count != sequences.Count)
            {
                BuildSequenceLookup();
            }

            // Prefer exact clip match (fast and robust).
            if (_sequenceLookup != null && _sequenceLookup.TryGetValue(currentClip, out var directMatch))
            {
                return directMatch;
            }

            // Fallback: length-based match for older data or unusual setups.
            for (int i = 0; i < sequences.Count; i++)
            {
                var seq = sequences[i];
                if (seq == null || seq.targetClip == null)
                    continue;

                float lengthDiff = Mathf.Abs(currentClip.length - seq.targetClip.length);
                if (lengthDiff <= 0.01f)
                {
                    return seq;
                }
            }

            return null;
        }

        private bool TryGetCurrentClip(out AnimationClip clip)
        {
            clip = null;

            if (animator == null)
                return false;

            var clips = animator.GetCurrentAnimatorClipInfo(0);
            if (clips == null || clips.Length == 0)
                return false;

            int bestIndex = 0;
            float bestWeight = clips[0].weight;
            for (int i = 1; i < clips.Length; i++)
            {
                if (clips[i].weight > bestWeight)
                {
                    bestWeight = clips[i].weight;
                    bestIndex = i;
                }
            }

            clip = clips[bestIndex].clip;
            return clip != null;
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
            _lastNormalizedTime = -1f;
            _firedKeyframes.Clear();
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
            if (logActions)
            {
                string seqName = _activeSequence != null ? _activeSequence.name : "None";
                string clipName = _clip != null ? _clip.name : "None";
                Debug.Log($"[ActionSequencePlayer] Execute {keyframe.type} frame={keyframe.frame} seq='{seqName}' clip='{clipName}'.", this);
            }

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
            {
                if (logActions)
                {
                    Debug.LogWarning($"[ActionSequencePlayer] PlaySound has no AudioClip at frame {keyframe.frame}.", this);
                }
                return;
            }

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
