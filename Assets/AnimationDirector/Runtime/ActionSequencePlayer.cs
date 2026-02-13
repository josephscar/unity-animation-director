using System.Collections.Generic;
using UnityEngine;

namespace AnimationDirector
{
    /// <summary>
    /// Plays an AnimationActionSequence in sync with an Animator's current clip.
    /// Checks the current frame each Update and executes any matching keyframes once per playthrough.
    /// </summary>
    [DisallowMultipleComponent]
    public class ActionSequencePlayer : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Animator animator;
        [SerializeField] private AnimationActionSequence sequence;

        [Header("Debug")]
        [SerializeField] private bool logActions;

        [Header("Target Bindings (for Enable/Disable)")]
        [SerializeField] private List<TargetBinding> targetBindings = new List<TargetBinding>();

        // Runtime tracking
        private AnimationClip _clip;
        private int _lastFrame = -1;
        private int _lastLoopCount = 0;
        private readonly HashSet<int> _firedFrames = new HashSet<int>();

        // For SpawnPrefab / DestroySpawned
        private readonly List<GameObject> _spawnedInstances = new List<GameObject>();

        // Buffer reused for per-frame lookup
        private readonly List<ActionKeyframe> _frameKeyframes = new List<ActionKeyframe>(8);

        // Cache for fast id -> GameObject lookup
        private Dictionary<string, GameObject> _bindingLookup;

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

            BuildBindingLookup();
        }

        private void Update()
        {
            if (sequence == null || sequence.targetClip == null)
                return;

            if (animator == null)
                return;

            EnsureClipCached();
            if (_clip == null)
                return;

            var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            if (stateInfo.length <= 0f)
                return;

            // normalizedTime may be >1 for looping clips; track loop count.
            float normalizedTime = stateInfo.normalizedTime;
            int loopCount = Mathf.FloorToInt(normalizedTime);
            float t = Mathf.Repeat(normalizedTime, 1f);

            // Reset when clip loops or changes.
            if (loopCount != _lastLoopCount)
            {
                _lastLoopCount = loopCount;
                _lastFrame = -1;
                _firedFrames.Clear();
            }

            int totalFrames = Mathf.Max(1, Mathf.RoundToInt(_clip.length * _clip.frameRate));
            int currentFrame = Mathf.FloorToInt(t * totalFrames);
            currentFrame = Mathf.Clamp(currentFrame, 0, Mathf.Max(0, totalFrames - 1));

            if (currentFrame == _lastFrame)
                return;

            _lastFrame = currentFrame;

            // Fetch keyframes for this frame and execute any that haven't fired yet this loop.
            sequence.GetKeyframesForFrame(currentFrame, _frameKeyframes);
            if (_frameKeyframes.Count == 0)
                return;

            for (int i = 0; i < _frameKeyframes.Count; i++)
            {
                var kf = _frameKeyframes[i];
                if (kf == null)
                    continue;

                // Optionally we could track by index instead of frame, but per-frame is fine for V1.
                if (_firedFrames.Contains(kf.frame))
                    continue;

                ExecuteKeyframe(kf);
                _firedFrames.Add(kf.frame);
            }
        }

        private void EnsureClipCached()
        {
            if (sequence == null)
            {
                _clip = null;
                return;
            }

            if (_clip == sequence.targetClip)
                return;

            _clip = sequence.targetClip;
            _lastFrame = -1;
            _lastLoopCount = 0;
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

            var instance = Instantiate(
                keyframe.prefab,
                anchorTransform.position,
                anchorTransform.rotation,
                anchorTransform);

            _spawnedInstances.Add(instance);

            if (keyframe.lifetime > 0f)
            {
                Destroy(instance, keyframe.lifetime);
            }

            if (logActions)
            {
                Debug.Log($"[ActionSequencePlayer] Spawned prefab '{keyframe.prefab.name}' at frame {keyframe.frame}.", this);
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

            if (logActions)
            {
                Debug.Log($"[ActionSequencePlayer] Set '{target.name}' active={enabled} at frame {keyframe.frame}.", this);
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
        /// </summary>
        public void SetData(Animator targetAnimator, AnimationActionSequence actionSequence)
        {
            animator = targetAnimator;
            sequence = actionSequence;
            EnsureClipCached();
        }

        [System.Serializable]
        public class TargetBinding
        {
            public string id;
            public GameObject target;
        }
    }
}


