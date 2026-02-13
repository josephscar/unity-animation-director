using System.Collections.Generic;
using UnityEngine;

namespace AnimationDirector
{
    /// <summary>
    /// ScriptableObject that defines a sequence of frame-based actions
    /// for a specific AnimationClip.
    /// </summary>
    [CreateAssetMenu(
        fileName = "AnimationActionSequence",
        menuName = "Animation Director/Action Sequence")]
    public class AnimationActionSequence : ScriptableObject
    {
        [Tooltip("The AnimationClip this sequence is authored for. One sequence per clip is recommended.")]
        public AnimationClip targetClip;

        [Tooltip("Ordered list of keyframes. The editor will keep this sorted by frame.")]
        public List<ActionKeyframe> keyframes = new List<ActionKeyframe>();

        /// <summary>
        /// Returns all keyframes that should fire on the given frame.
        /// </summary>
        public void GetKeyframesForFrame(int frame, List<ActionKeyframe> results)
        {
            results.Clear();
            if (keyframes == null || keyframes.Count == 0)
                return;

            for (int i = 0; i < keyframes.Count; i++)
            {
                var k = keyframes[i];
                if (k != null && k.frame == frame)
                {
                    results.Add(k);
                }
            }
        }

        /// <summary>
        /// Ensures keyframes are sorted by frame number.
        /// </summary>
        public void SortByFrame()
        {
            if (keyframes == null)
                keyframes = new List<ActionKeyframe>();

            keyframes.Sort((a, b) => a.frame.CompareTo(b.frame));
        }
    }
}


