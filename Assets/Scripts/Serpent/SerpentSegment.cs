using UnityEngine;

namespace AetherSerpent.Serpent
{
    /// <summary>
    /// Represents an individual bone/segment within the serpent's physical spine.
    /// Stores physical positions for sub-step integration and interpolation.
    /// </summary>
    public class SerpentSegment : MonoBehaviour
    {
        public int Index { get; private set; }
        public Transform BoneTransform => transform;

        [Header("State")]
        public Vector3 PreviousPosition;
        public Vector3 CurrentPhysicsPosition;
        public Quaternion PreviousRotation;
        public Quaternion CurrentPhysicsRotation;

        [Header("Visual Mesh Reference")]
        [SerializeField] private Transform visualRoot;
        public Transform VisualRoot => visualRoot;

        public void Initialize(int index, Vector3 startPos, Quaternion startRot)
        {
            Index = index;
            transform.position = startPos;
            transform.rotation = startRot;

            PreviousPosition = startPos;
            CurrentPhysicsPosition = startPos;
            PreviousRotation = startRot;
            CurrentPhysicsRotation = startRot;

            if (visualRoot == null && transform.childCount > 0)
            {
                visualRoot = transform.GetChild(0);
            }
        }

        public void UpdatePhysicsTransform(Vector3 pos, Quaternion rot)
        {
            PreviousPosition = CurrentPhysicsPosition;
            PreviousRotation = CurrentPhysicsRotation;

            CurrentPhysicsPosition = pos;
            CurrentPhysicsRotation = rot;
        }

        public void InterpolateVisual(float alpha)
        {
            transform.position = Vector3.Lerp(PreviousPosition, CurrentPhysicsPosition, alpha);
            transform.rotation = Quaternion.Slerp(PreviousRotation, CurrentPhysicsRotation, alpha);
        }
    }
}
