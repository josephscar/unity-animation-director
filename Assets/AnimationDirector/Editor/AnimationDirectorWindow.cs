using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AnimationDirector.Editor
{
    /// <summary>
    /// Inspector-style editor window for creating and editing AnimationActionSequence assets.
    /// V1 is list- and frame-field-based (no custom timeline UI).
    /// </summary>
    public class AnimationDirectorWindow : EditorWindow
    {
        private AnimationActionSequence _sequence;
        private SerializedObject _serializedSequence;

        private int _currentFrame;
        private int _totalFrames;

        private readonly List<ActionKeyframe> _frameBuffer = new List<ActionKeyframe>(8);

        [MenuItem("Window/Animation Director")]
        public static void ShowWindow()
        {
            var window = GetWindow<AnimationDirectorWindow>("Animation Director");
            window.minSize = new Vector2(420, 260);
        }

        private void OnSelectionChange()
        {
            // Auto-pick an AnimationActionSequence if the user selects one in the Project.
            if (Selection.activeObject is AnimationActionSequence seq)
            {
                _sequence = seq;
                RebuildSerializedObject();
                Repaint();
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.Space();
            DrawSequenceSelector();

            if (_sequence == null)
            {
                EditorGUILayout.HelpBox(
                    "Assign an Animation Action Sequence asset, or create a new one via:\n" +
                    "Assets → Create → Animation Director → Action Sequence.",
                    MessageType.Info);
                return;
            }

            if (_serializedSequence == null)
            {
                RebuildSerializedObject();
            }

            _serializedSequence.Update();

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawClipInfoAndScrub();
            }

            EditorGUILayout.Space();

            using (var scroll = new EditorGUILayout.ScrollViewScope(_scrollPosition))
            {
                _scrollPosition = scroll.scrollPosition;
                DrawKeyframeList();
            }

            _serializedSequence.ApplyModifiedProperties();
        }

        #region Sequence selection

        private Vector2 _scrollPosition;

        private void DrawSequenceSelector()
        {
            EditorGUI.BeginChangeCheck();
            _sequence = (AnimationActionSequence)EditorGUILayout.ObjectField(
                "Sequence Asset",
                _sequence,
                typeof(AnimationActionSequence),
                false);
            if (EditorGUI.EndChangeCheck())
            {
                RebuildSerializedObject();
            }
        }

        private void RebuildSerializedObject()
        {
            if (_sequence != null)
            {
                _serializedSequence = new SerializedObject(_sequence);
                UpdateClipMetrics();
            }
            else
            {
                _serializedSequence = null;
                _totalFrames = 0;
                _currentFrame = 0;
            }
        }

        #endregion

        #region Clip info + scrub

        private void UpdateClipMetrics()
        {
            if (_sequence == null || _sequence.targetClip == null)
            {
                _totalFrames = 0;
                _currentFrame = 0;
                return;
            }

            var clip = _sequence.targetClip;
            // Unity's AnimationClip.length is in seconds.
            _totalFrames = Mathf.Max(0, Mathf.RoundToInt(clip.length * clip.frameRate));
            _currentFrame = Mathf.Clamp(_currentFrame, 0, Mathf.Max(0, _totalFrames - 1));
        }

        private void DrawClipInfoAndScrub()
        {
            EditorGUI.BeginChangeCheck();
            var clipProp = _serializedSequence.FindProperty("targetClip");
            EditorGUILayout.PropertyField(clipProp);
            if (EditorGUI.EndChangeCheck())
            {
                _serializedSequence.ApplyModifiedProperties();
                UpdateClipMetrics();
                EditorUtility.SetDirty(_sequence);
            }

            var clip = _sequence.targetClip;
            if (clip == null)
            {
                EditorGUILayout.HelpBox(
                    "Assign an AnimationClip to author actions against.",
                    MessageType.Warning);
                return;
            }

            float lengthSeconds = clip.length;
            float frameRate = clip.frameRate;

            EditorGUILayout.LabelField("Clip Info", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Length (seconds)", lengthSeconds.ToString("0.###"));
            EditorGUILayout.LabelField("Frame Rate", frameRate.ToString("0.##"));
            EditorGUILayout.LabelField("Total Frames", _totalFrames.ToString());

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Frame Scrub", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Current Frame", GUILayout.Width(96f));
                _currentFrame = EditorGUILayout.IntSlider(_currentFrame, 0, Mathf.Max(0, _totalFrames - 1));
                _currentFrame = EditorGUILayout.IntField(_currentFrame, GUILayout.Width(64f));
                _currentFrame = Mathf.Clamp(_currentFrame, 0, Mathf.Max(0, _totalFrames - 1));
            }

            if (GUILayout.Button("Add Keyframe at Current Frame"))
            {
                AddKeyframeAtCurrentFrame();
            }
        }

        private void AddKeyframeAtCurrentFrame()
        {
            if (_sequence == null)
                return;

            if (_sequence.keyframes == null)
            {
                _sequence.keyframes = new List<ActionKeyframe>();
            }

            var kf = new ActionKeyframe
            {
                frame = _currentFrame,
                type = ActionType.SpawnPrefab
            };

            _sequence.keyframes.Add(kf);
            _sequence.SortByFrame();

            EditorUtility.SetDirty(_sequence);
            AssetDatabase.SaveAssets();
        }

        #endregion

        #region Keyframe list

        private void DrawKeyframeList()
        {
            var keyframesProp = _serializedSequence.FindProperty("keyframes");
            if (keyframesProp == null)
            {
                EditorGUILayout.HelpBox("Keyframe list is missing or corrupted.", MessageType.Error);
                return;
            }

            EditorGUILayout.LabelField("Keyframes", EditorStyles.boldLabel);

            if (keyframesProp.arraySize == 0)
            {
                EditorGUILayout.HelpBox("No keyframes yet. Use 'Add Keyframe at Current Frame' to create one.", MessageType.Info);
                return;
            }

            int removeIndex = -1;

            for (int i = 0; i < keyframesProp.arraySize; i++)
            {
                var element = keyframesProp.GetArrayElementAtIndex(i);

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField($"Keyframe {i}", EditorStyles.boldLabel);
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("Remove", GUILayout.Width(72f)))
                        {
                            removeIndex = i;
                        }
                    }

                    DrawSingleKeyframe(element);
                }
            }

            if (removeIndex >= 0 && removeIndex < keyframesProp.arraySize)
            {
                keyframesProp.DeleteArrayElementAtIndex(removeIndex);
                _serializedSequence.ApplyModifiedProperties();
                if (_sequence != null)
                {
                    _sequence.SortByFrame();
                    EditorUtility.SetDirty(_sequence);
                    AssetDatabase.SaveAssets();
                }
            }
        }

        private void DrawSingleKeyframe(SerializedProperty kfProp)
        {
            var frameProp = kfProp.FindPropertyRelative("frame");
            var typeProp = kfProp.FindPropertyRelative("type");
            var prefabProp = kfProp.FindPropertyRelative("prefab");
            var anchorProp = kfProp.FindPropertyRelative("anchor");
            var lifetimeProp = kfProp.FindPropertyRelative("lifetime");
            var soundProp = kfProp.FindPropertyRelative("sound");
            var targetObjectProp = kfProp.FindPropertyRelative("targetObject");
            var targetIdProp = kfProp.FindPropertyRelative("targetId");

            // Frame field
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(frameProp);
            if (EditorGUI.EndChangeCheck())
            {
                if (_totalFrames > 0)
                {
                    frameProp.intValue = Mathf.Clamp(frameProp.intValue, 0, Mathf.Max(0, _totalFrames - 1));
                }
                else
                {
                    frameProp.intValue = Mathf.Max(0, frameProp.intValue);
                }

                _serializedSequence.ApplyModifiedProperties();
                if (_sequence != null)
                {
                    _sequence.SortByFrame();
                    EditorUtility.SetDirty(_sequence);
                    AssetDatabase.SaveAssets();
                }
            }

            // Action type
            EditorGUILayout.PropertyField(typeProp);

            // Conditional fields by type
            var type = (ActionType)typeProp.enumValueIndex;
            switch (type)
            {
                case ActionType.SpawnPrefab:
                    EditorGUILayout.PropertyField(prefabProp);
                    EditorGUILayout.PropertyField(anchorProp);
                    EditorGUILayout.PropertyField(lifetimeProp);
                    break;

                case ActionType.PlaySound:
                    EditorGUILayout.PropertyField(soundProp);
                    break;

                case ActionType.EnableObject:
                case ActionType.DisableObject:
                    EditorGUILayout.PropertyField(targetIdProp, new GUIContent("Target Id"));
                    EditorGUILayout.PropertyField(targetObjectProp, new GUIContent("Target Object (asset only)"));
                    break;

                case ActionType.DestroySpawned:
                    EditorGUILayout.HelpBox(
                        "DestroySpawned will destroy all instances spawned by this player (V1 behavior).",
                        MessageType.None);
                    break;
            }
        }

        #endregion
    }
}


