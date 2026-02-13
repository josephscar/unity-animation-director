using UnityEngine;

namespace AnimationDirector
{
    /// <summary>
    /// Describes where spawned prefabs should be attached.
    /// V1 keeps this simple but leaves room for extension.
    /// </summary>
    [System.Serializable]
    public class TransformAnchor
    {
        public AnchorSpace space = AnchorSpace.Self;
        public Transform customTransform;

        public Transform Resolve(Transform selfRoot)
        {
            switch (space)
            {
                case AnchorSpace.Self:
                    return selfRoot;
                case AnchorSpace.Root:
                    return selfRoot.root;
                case AnchorSpace.Custom:
                    return customTransform != null ? customTransform : selfRoot;
                default:
                    return selfRoot;
            }
        }
    }

    public enum AnchorSpace
    {
        Self,
        Root,
        Custom
    }
}


