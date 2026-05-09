using System;
using UnityEngine;

namespace ToJam26.Gameplay.Slicing.External
{
    public static class Slicer
    {
        public static GameObject[] Slice(Plane plane, GameObject objectToCut, Material insideMaterial = null)
        {
            if (objectToCut == null)
                throw new ArgumentNullException(nameof(objectToCut));

            MeshFilter meshFilter = objectToCut.GetComponent<MeshFilter>();
            MeshRenderer meshRenderer = objectToCut.GetComponent<MeshRenderer>();
            Sliceable sliceable = objectToCut.GetComponent<Sliceable>();

            if (meshFilter == null || meshFilter.sharedMesh == null)
                throw new InvalidOperationException($"Cannot slice {objectToCut.name} without a readable MeshFilter.");

            if (meshRenderer == null)
                throw new InvalidOperationException($"Cannot slice {objectToCut.name} without a MeshRenderer.");

            if (!meshFilter.sharedMesh.isReadable)
                throw new InvalidOperationException($"Mesh '{meshFilter.sharedMesh.name}' on {objectToCut.name} is not readable.");

            if (sliceable == null)
            {
                throw new NotSupportedException(
                    "Cannot slice non-sliceable object. Add ToJam26.Gameplay.Slicing.External.Sliceable first.");
            }

            Mesh mesh = meshFilter.mesh;
            SlicesMetadata slicesMeta = new SlicesMetadata(
                plane,
                mesh,
                sliceable.IsSolid,
                sliceable.ReverseWireTriangles,
                sliceable.ShareVertices,
                sliceable.SmoothVertices);

            GameObject positiveObject = CreateMeshGameObject(objectToCut);
            positiveObject.name = $"{objectToCut.name}_positive";

            GameObject negativeObject = CreateMeshGameObject(objectToCut);
            negativeObject.name = $"{objectToCut.name}_negative";

            Mesh positiveSideMesh = slicesMeta.PositiveSideMesh;
            Mesh negativeSideMesh = slicesMeta.NegativeSideMesh;

            positiveObject.GetComponent<MeshFilter>().mesh = positiveSideMesh;
            negativeObject.GetComponent<MeshFilter>().mesh = negativeSideMesh;
            ConfigureMaterials(positiveObject.GetComponent<MeshRenderer>(), originalMaterials: meshRenderer.materials, positiveSideMesh, insideMaterial);
            ConfigureMaterials(negativeObject.GetComponent<MeshRenderer>(), originalMaterials: meshRenderer.materials, negativeSideMesh, insideMaterial);

            SetupMeshCollider(positiveObject, positiveSideMesh);
            SetupMeshCollider(negativeObject, negativeSideMesh);

            return new[] { positiveObject, negativeObject };
        }

        private static GameObject CreateMeshGameObject(GameObject originalObject)
        {
            Sliceable originalSliceable = originalObject.GetComponent<Sliceable>();

            GameObject meshGameObject = new GameObject();
            meshGameObject.AddComponent<MeshFilter>();
            meshGameObject.AddComponent<MeshRenderer>();

            Sliceable sliceable = meshGameObject.AddComponent<Sliceable>();
            sliceable.IsSolid = originalSliceable.IsSolid;
            sliceable.ReverseWireTriangles = originalSliceable.ReverseWireTriangles;
            sliceable.UseGravity = originalSliceable.UseGravity;
            sliceable.ShareVertices = originalSliceable.ShareVertices;
            sliceable.SmoothVertices = originalSliceable.SmoothVertices;

            meshGameObject.transform.SetPositionAndRotation(originalObject.transform.position, originalObject.transform.rotation);
            meshGameObject.transform.localScale = originalObject.transform.lossyScale;
            meshGameObject.tag = originalObject.tag;
            meshGameObject.layer = originalObject.layer;

            return meshGameObject;
        }

        private static void SetupMeshCollider(GameObject gameObject, Mesh mesh)
        {
            MeshCollider meshCollider = gameObject.AddComponent<MeshCollider>();
            meshCollider.sharedMesh = mesh;
            meshCollider.convex = true;
        }

        private static void ConfigureMaterials(MeshRenderer meshRenderer, Material[] originalMaterials, Mesh mesh, Material insideMaterial)
        {
            if (meshRenderer == null)
                return;

            if (mesh == null || mesh.subMeshCount <= 1)
            {
                meshRenderer.materials = originalMaterials;
                return;
            }

            int outsideCount = Mathf.Max(1, mesh.subMeshCount - 1);
            Material fallbackMaterial =
                originalMaterials != null && originalMaterials.Length > 0
                    ? originalMaterials[Mathf.Min(originalMaterials.Length - 1, outsideCount - 1)]
                    : null;

            Material[] assignedMaterials = new Material[mesh.subMeshCount];
            for (int i = 0; i < outsideCount; i++)
            {
                assignedMaterials[i] =
                    originalMaterials != null && originalMaterials.Length > 0
                        ? originalMaterials[Mathf.Min(i, originalMaterials.Length - 1)]
                        : fallbackMaterial;
            }

            assignedMaterials[mesh.subMeshCount - 1] = insideMaterial != null ? insideMaterial : fallbackMaterial;
            meshRenderer.materials = assignedMaterials;
        }
    }
}
