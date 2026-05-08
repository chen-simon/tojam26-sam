using UnityEngine;

namespace ToJam26.Gameplay.Slicing.External
{
    public class Sliceable : MonoBehaviour
    {
        [SerializeField] private bool isSolid = true;
        [SerializeField] private bool reverseWindTriangles = false;
        [SerializeField] private bool useGravity = false;
        [SerializeField] private bool shareVertices = false;
        [SerializeField] private bool smoothVertices = false;

        public bool IsSolid
        {
            get => isSolid;
            set => isSolid = value;
        }

        public bool ReverseWireTriangles
        {
            get => reverseWindTriangles;
            set => reverseWindTriangles = value;
        }

        public bool UseGravity
        {
            get => useGravity;
            set => useGravity = value;
        }

        public bool ShareVertices
        {
            get => shareVertices;
            set => shareVertices = value;
        }

        public bool SmoothVertices
        {
            get => smoothVertices;
            set => smoothVertices = value;
        }
    }
}
