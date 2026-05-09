using System;
using System.Collections.Generic;
using UnityEngine;

namespace ToJam26.Gameplay.Slicing.External
{
    public enum MeshSide
    {
        Positive = 0,
        Negative = 1
    }

    internal class SlicesMetadata
    {
        private Mesh positiveSideMesh;
        private readonly List<Vector3> positiveSideVertices;
        private readonly List<int> positiveSideTriangles;
        private readonly List<int> positiveSideCapTriangles;
        private readonly List<Vector2> positiveSideUvs;
        private readonly List<Vector3> positiveSideNormals;

        private Mesh negativeSideMesh;
        private readonly List<Vector3> negativeSideVertices;
        private readonly List<int> negativeSideTriangles;
        private readonly List<int> negativeSideCapTriangles;
        private readonly List<Vector2> negativeSideUvs;
        private readonly List<Vector3> negativeSideNormals;

        private readonly List<Vector3> pointsAlongPlane;
        private readonly Plane plane;
        private readonly Mesh mesh;
        private readonly bool isSolid;
        private readonly bool useSharedVertices;
        private readonly bool smoothVertices;
        private readonly bool createReverseTriangleWindings;

        public Mesh PositiveSideMesh
        {
            get
            {
                positiveSideMesh ??= new Mesh();
                SetMeshData(MeshSide.Positive);
                return positiveSideMesh;
            }
        }

        public Mesh NegativeSideMesh
        {
            get
            {
                negativeSideMesh ??= new Mesh();
                SetMeshData(MeshSide.Negative);
                return negativeSideMesh;
            }
        }

        public SlicesMetadata(
            Plane plane,
            Mesh mesh,
            bool isSolid,
            bool createReverseTriangleWindings,
            bool shareVertices,
            bool smoothVertices)
        {
            positiveSideTriangles = new List<int>();
            positiveSideCapTriangles = new List<int>();
            positiveSideVertices = new List<Vector3>();
            negativeSideTriangles = new List<int>();
            negativeSideCapTriangles = new List<int>();
            negativeSideVertices = new List<Vector3>();
            positiveSideUvs = new List<Vector2>();
            negativeSideUvs = new List<Vector2>();
            positiveSideNormals = new List<Vector3>();
            negativeSideNormals = new List<Vector3>();
            pointsAlongPlane = new List<Vector3>();
            this.plane = plane;
            this.mesh = mesh;
            this.isSolid = isSolid;
            this.createReverseTriangleWindings = createReverseTriangleWindings;
            useSharedVertices = shareVertices;
            this.smoothVertices = smoothVertices;

            ComputeNewMeshes();
        }

        private void AddTrianglesNormalAndUvs(
            MeshSide side,
            Vector3 vertex1,
            Vector3? normal1,
            Vector2 uv1,
            Vector3 vertex2,
            Vector3? normal2,
            Vector2 uv2,
            Vector3 vertex3,
            Vector3? normal3,
            Vector2 uv3,
            bool shareVertices,
            bool isCap = false)
        {
            if (side == MeshSide.Positive)
            {
                AddTrianglesNormalsAndUvs(
                    positiveSideVertices,
                    isCap ? positiveSideCapTriangles : positiveSideTriangles,
                    positiveSideNormals,
                    positiveSideUvs,
                    vertex1,
                    normal1,
                    uv1,
                    vertex2,
                    normal2,
                    uv2,
                    vertex3,
                    normal3,
                    uv3,
                    shareVertices);
            }
            else
            {
                AddTrianglesNormalsAndUvs(
                    negativeSideVertices,
                    isCap ? negativeSideCapTriangles : negativeSideTriangles,
                    negativeSideNormals,
                    negativeSideUvs,
                    vertex1,
                    normal1,
                    uv1,
                    vertex2,
                    normal2,
                    uv2,
                    vertex3,
                    normal3,
                    uv3,
                    shareVertices);
            }
        }

        private void AddTrianglesNormalsAndUvs(
            List<Vector3> vertices,
            List<int> triangles,
            List<Vector3> normals,
            List<Vector2> uvs,
            Vector3 vertex1,
            Vector3? normal1,
            Vector2 uv1,
            Vector3 vertex2,
            Vector3? normal2,
            Vector2 uv2,
            Vector3 vertex3,
            Vector3? normal3,
            Vector2 uv3,
            bool shareVertices)
        {
            int tri1Index = vertices.IndexOf(vertex1);

            if (tri1Index > -1 && shareVertices)
            {
                triangles.Add(tri1Index);
            }
            else
            {
                normal1 ??= ComputeNormal(vertex1, vertex2, vertex3);
                AddVertNormalUv(vertices, normals, uvs, triangles, vertex1, normal1.Value, uv1);
            }

            int tri2Index = vertices.IndexOf(vertex2);
            if (tri2Index > -1 && shareVertices)
            {
                triangles.Add(tri2Index);
            }
            else
            {
                normal2 ??= ComputeNormal(vertex2, vertex3, vertex1);
                AddVertNormalUv(vertices, normals, uvs, triangles, vertex2, normal2.Value, uv2);
            }

            int tri3Index = vertices.IndexOf(vertex3);
            if (tri3Index > -1 && shareVertices)
            {
                triangles.Add(tri3Index);
            }
            else
            {
                normal3 ??= ComputeNormal(vertex3, vertex1, vertex2);
                AddVertNormalUv(vertices, normals, uvs, triangles, vertex3, normal3.Value, uv3);
            }
        }

        private static void AddVertNormalUv(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<int> triangles,
            Vector3 vertex,
            Vector3 normal,
            Vector2 uv)
        {
            vertices.Add(vertex);
            normals.Add(normal);
            uvs.Add(uv);
            triangles.Add(vertices.Count - 1);
        }

        private void AddReverseTriangleWinding()
        {
            int positiveVertsStartIndex = positiveSideVertices.Count;
            positiveSideVertices.AddRange(positiveSideVertices);
            positiveSideUvs.AddRange(positiveSideUvs);
            positiveSideNormals.AddRange(FlipNormals(positiveSideNormals));

            int numPositiveTriangles = positiveSideTriangles.Count;
            for (int i = 0; i < numPositiveTriangles; i += 3)
            {
                positiveSideTriangles.Add(positiveVertsStartIndex + positiveSideTriangles[i]);
                positiveSideTriangles.Add(positiveVertsStartIndex + positiveSideTriangles[i + 2]);
                positiveSideTriangles.Add(positiveVertsStartIndex + positiveSideTriangles[i + 1]);
            }

            int numPositiveCapTriangles = positiveSideCapTriangles.Count;
            for (int i = 0; i < numPositiveCapTriangles; i += 3)
            {
                positiveSideCapTriangles.Add(positiveVertsStartIndex + positiveSideCapTriangles[i]);
                positiveSideCapTriangles.Add(positiveVertsStartIndex + positiveSideCapTriangles[i + 2]);
                positiveSideCapTriangles.Add(positiveVertsStartIndex + positiveSideCapTriangles[i + 1]);
            }

            int negativeVertexStartIndex = negativeSideVertices.Count;
            negativeSideVertices.AddRange(negativeSideVertices);
            negativeSideUvs.AddRange(negativeSideUvs);
            negativeSideNormals.AddRange(FlipNormals(negativeSideNormals));

            int numNegativeTriangles = negativeSideTriangles.Count;
            for (int i = 0; i < numNegativeTriangles; i += 3)
            {
                negativeSideTriangles.Add(negativeVertexStartIndex + negativeSideTriangles[i]);
                negativeSideTriangles.Add(negativeVertexStartIndex + negativeSideTriangles[i + 2]);
                negativeSideTriangles.Add(negativeVertexStartIndex + negativeSideTriangles[i + 1]);
            }

            int numNegativeCapTriangles = negativeSideCapTriangles.Count;
            for (int i = 0; i < numNegativeCapTriangles; i += 3)
            {
                negativeSideCapTriangles.Add(negativeVertexStartIndex + negativeSideCapTriangles[i]);
                negativeSideCapTriangles.Add(negativeVertexStartIndex + negativeSideCapTriangles[i + 2]);
                negativeSideCapTriangles.Add(negativeVertexStartIndex + negativeSideCapTriangles[i + 1]);
            }
        }

        private void JoinPointsAlongPlane()
        {
            List<Vector3> orderedPoints = GetOrderedPlanePoints();
            if (orderedPoints.Count < 3)
                return;

            Vector3 center = GetAveragePoint(orderedPoints);
            Vector3 positiveNormal = plane.normal.normalized;
            Vector3 negativeNormal = -positiveNormal;

            for (int i = 0; i < orderedPoints.Count; i++)
            {
                Vector3 current = orderedPoints[i];
                Vector3 next = orderedPoints[(i + 1) % orderedPoints.Count];

                AddTrianglesNormalAndUvs(
                    MeshSide.Positive,
                    center,
                    positiveNormal,
                    Vector2.zero,
                    next,
                    positiveNormal,
                    Vector2.zero,
                    current,
                    positiveNormal,
                    Vector2.zero,
                    false,
                    true);

                AddTrianglesNormalAndUvs(
                    MeshSide.Negative,
                    center,
                    negativeNormal,
                    Vector2.zero,
                    current,
                    negativeNormal,
                    Vector2.zero,
                    next,
                    negativeNormal,
                    Vector2.zero,
                    false,
                    true);
            }
        }

        private Vector3 GetHalfwayPoint(out float distance)
        {
            if (pointsAlongPlane.Count == 0)
            {
                distance = 0f;
                return Vector3.zero;
            }

            Vector3 firstPoint = pointsAlongPlane[0];
            Vector3 furthestPoint = Vector3.zero;
            distance = 0f;

            foreach (Vector3 point in pointsAlongPlane)
            {
                float currentDistance = Vector3.Distance(firstPoint, point);
                if (currentDistance > distance)
                {
                    distance = currentDistance;
                    furthestPoint = point;
                }
            }

            return Vector3.Lerp(firstPoint, furthestPoint, 0.5f);
        }

        private List<Vector3> GetOrderedPlanePoints()
        {
            List<Vector3> uniquePoints = new List<Vector3>();
            const float duplicateThresholdSqr = 0.000001f;

            foreach (Vector3 point in pointsAlongPlane)
            {
                bool isDuplicate = false;
                for (int i = 0; i < uniquePoints.Count; i++)
                {
                    if ((uniquePoints[i] - point).sqrMagnitude <= duplicateThresholdSqr)
                    {
                        isDuplicate = true;
                        break;
                    }
                }

                if (!isDuplicate)
                    uniquePoints.Add(point);
            }

            if (uniquePoints.Count < 3)
                return uniquePoints;

            Vector3 center = GetAveragePoint(uniquePoints);
            BuildPlaneBasis(plane.normal.normalized, out Vector3 basisRight, out Vector3 basisUp);

            uniquePoints.Sort((a, b) =>
            {
                Vector3 offsetA = a - center;
                Vector3 offsetB = b - center;
                float angleA = Mathf.Atan2(Vector3.Dot(offsetA, basisUp), Vector3.Dot(offsetA, basisRight));
                float angleB = Mathf.Atan2(Vector3.Dot(offsetB, basisUp), Vector3.Dot(offsetB, basisRight));
                return angleA.CompareTo(angleB);
            });

            return uniquePoints;
        }

        private static Vector3 GetAveragePoint(List<Vector3> points)
        {
            Vector3 center = Vector3.zero;
            for (int i = 0; i < points.Count; i++)
                center += points[i];

            return center / Mathf.Max(1, points.Count);
        }

        private static void BuildPlaneBasis(Vector3 normal, out Vector3 basisRight, out Vector3 basisUp)
        {
            Vector3 referenceAxis = Mathf.Abs(Vector3.Dot(normal, Vector3.up)) > 0.99f ? Vector3.right : Vector3.up;
            basisRight = Vector3.Cross(referenceAxis, normal).normalized;
            basisUp = Vector3.Cross(normal, basisRight).normalized;
        }

        private void SetMeshData(MeshSide side)
        {
            if (side == MeshSide.Positive)
            {
                ApplyMeshData(positiveSideMesh, positiveSideVertices, positiveSideTriangles, positiveSideCapTriangles, positiveSideNormals, positiveSideUvs);
            }
            else
            {
                ApplyMeshData(negativeSideMesh, negativeSideVertices, negativeSideTriangles, negativeSideCapTriangles, negativeSideNormals, negativeSideUvs);
            }
        }

        private static void ApplyMeshData(
            Mesh targetMesh,
            List<Vector3> vertices,
            List<int> triangles,
            List<int> capTriangles,
            List<Vector3> normals,
            List<Vector2> uvs)
        {
            targetMesh.Clear();
            targetMesh.vertices = vertices.ToArray();
            targetMesh.normals = normals.ToArray();
            targetMesh.uv = uvs.ToArray();

            if (capTriangles.Count > 0)
            {
                targetMesh.subMeshCount = 2;
                targetMesh.SetTriangles(triangles, 0);
                targetMesh.SetTriangles(capTriangles, 1);
            }
            else
            {
                targetMesh.subMeshCount = 1;
                targetMesh.SetTriangles(triangles, 0);
            }

            targetMesh.RecalculateBounds();
        }

        private void ComputeNewMeshes()
        {
            int[] meshTriangles = mesh.triangles;
            Vector3[] meshVertices = mesh.vertices;
            Vector3[] meshNormals = mesh.normals;
            Vector2[] meshUvs = mesh.uv;

            for (int i = 0; i < meshTriangles.Length; i += 3)
            {
                Vector3 vert1 = meshVertices[meshTriangles[i]];
                int vert1Index = Array.IndexOf(meshVertices, vert1);
                Vector2 uv1 = meshUvs[vert1Index];
                Vector3 normal1 = meshNormals[vert1Index];
                bool vert1Side = plane.GetSide(vert1);

                Vector3 vert2 = meshVertices[meshTriangles[i + 1]];
                int vert2Index = Array.IndexOf(meshVertices, vert2);
                Vector2 uv2 = meshUvs[vert2Index];
                Vector3 normal2 = meshNormals[vert2Index];
                bool vert2Side = plane.GetSide(vert2);

                Vector3 vert3 = meshVertices[meshTriangles[i + 2]];
                bool vert3Side = plane.GetSide(vert3);
                int vert3Index = Array.IndexOf(meshVertices, vert3);
                Vector3 normal3 = meshNormals[vert3Index];
                Vector2 uv3 = meshUvs[vert3Index];

                if (vert1Side == vert2Side && vert2Side == vert3Side)
                {
                    MeshSide side = vert1Side ? MeshSide.Positive : MeshSide.Negative;
                    AddTrianglesNormalAndUvs(side, vert1, normal1, uv1, vert2, normal2, uv2, vert3, normal3, uv3, true);
                    continue;
                }

                Vector3 intersection1;
                Vector3 intersection2;
                Vector2 intersection1Uv;
                Vector2 intersection2Uv;
                MeshSide side1 = vert1Side ? MeshSide.Positive : MeshSide.Negative;
                MeshSide side2 = vert1Side ? MeshSide.Negative : MeshSide.Positive;

                if (vert1Side == vert2Side)
                {
                    intersection1 = GetRayPlaneIntersectionPointAndUv(vert2, uv2, vert3, uv3, out intersection1Uv);
                    intersection2 = GetRayPlaneIntersectionPointAndUv(vert3, uv3, vert1, uv1, out intersection2Uv);

                    AddTrianglesNormalAndUvs(side1, vert1, null, uv1, vert2, null, uv2, intersection1, null, intersection1Uv, useSharedVertices);
                    AddTrianglesNormalAndUvs(side1, vert1, null, uv1, intersection1, null, intersection1Uv, intersection2, null, intersection2Uv, useSharedVertices);
                    AddTrianglesNormalAndUvs(side2, intersection1, null, intersection1Uv, vert3, null, uv3, intersection2, null, intersection2Uv, useSharedVertices);
                }
                else if (vert1Side == vert3Side)
                {
                    intersection1 = GetRayPlaneIntersectionPointAndUv(vert1, uv1, vert2, uv2, out intersection1Uv);
                    intersection2 = GetRayPlaneIntersectionPointAndUv(vert2, uv2, vert3, uv3, out intersection2Uv);

                    AddTrianglesNormalAndUvs(side1, vert1, null, uv1, intersection1, null, intersection1Uv, vert3, null, uv3, useSharedVertices);
                    AddTrianglesNormalAndUvs(side1, intersection1, null, intersection1Uv, intersection2, null, intersection2Uv, vert3, null, uv3, useSharedVertices);
                    AddTrianglesNormalAndUvs(side2, intersection1, null, intersection1Uv, vert2, null, uv2, intersection2, null, intersection2Uv, useSharedVertices);
                }
                else
                {
                    intersection1 = GetRayPlaneIntersectionPointAndUv(vert1, uv1, vert2, uv2, out intersection1Uv);
                    intersection2 = GetRayPlaneIntersectionPointAndUv(vert1, uv1, vert3, uv3, out intersection2Uv);

                    AddTrianglesNormalAndUvs(side1, vert1, null, uv1, intersection1, null, intersection1Uv, intersection2, null, intersection2Uv, useSharedVertices);
                    AddTrianglesNormalAndUvs(side2, intersection1, null, intersection1Uv, vert2, null, uv2, vert3, null, uv3, useSharedVertices);
                    AddTrianglesNormalAndUvs(side2, intersection1, null, intersection1Uv, vert3, null, uv3, intersection2, null, intersection2Uv, useSharedVertices);
                }

                pointsAlongPlane.Add(intersection1);
                pointsAlongPlane.Add(intersection2);
            }

            if (isSolid)
                JoinPointsAlongPlane();
            else if (createReverseTriangleWindings)
                AddReverseTriangleWinding();

            if (smoothVertices)
                SmoothVertices();
        }

        private Vector3 GetRayPlaneIntersectionPointAndUv(
            Vector3 vertex1,
            Vector2 vertex1Uv,
            Vector3 vertex2,
            Vector2 vertex2Uv,
            out Vector2 uv)
        {
            float distance = GetDistanceRelativeToPlane(vertex1, vertex2, out Vector3 pointOfIntersection);
            uv = InterpolateUvs(vertex1Uv, vertex2Uv, distance);
            return pointOfIntersection;
        }

        private float GetDistanceRelativeToPlane(Vector3 vertex1, Vector3 vertex2, out Vector3 pointOfIntersection)
        {
            Ray ray = new Ray(vertex1, vertex2 - vertex1);
            plane.Raycast(ray, out float distance);
            pointOfIntersection = ray.GetPoint(distance);
            return distance;
        }

        private static Vector2 InterpolateUvs(Vector2 uv1, Vector2 uv2, float distance)
        {
            return Vector2.Lerp(uv1, uv2, distance);
        }

        private static Vector3 ComputeNormal(Vector3 vertex1, Vector3 vertex2, Vector3 vertex3)
        {
            Vector3 side1 = vertex2 - vertex1;
            Vector3 side2 = vertex3 - vertex1;
            return Vector3.Cross(side1, side2);
        }

        private static List<Vector3> FlipNormals(List<Vector3> currentNormals)
        {
            List<Vector3> flippedNormals = new List<Vector3>(currentNormals.Count);
            foreach (Vector3 normal in currentNormals)
                flippedNormals.Add(-normal);
            return flippedNormals;
        }

        private void SmoothVertices()
        {
            DoSmoothing(positiveSideVertices, positiveSideNormals, positiveSideTriangles, positiveSideCapTriangles);
            DoSmoothing(negativeSideVertices, negativeSideNormals, negativeSideTriangles, negativeSideCapTriangles);
        }

        private static void DoSmoothing(List<Vector3> vertices, List<Vector3> normals, List<int> triangles, List<int> capTriangles)
        {
            for (int i = 0; i < normals.Count; i++)
                normals[i] = Vector3.zero;

            AccumulateNormals(vertices, normals, triangles);
            AccumulateNormals(vertices, normals, capTriangles);

            for (int i = 0; i < normals.Count; i++)
                normals[i] = normals[i].normalized;
        }

        private static void AccumulateNormals(List<Vector3> vertices, List<Vector3> normals, List<int> triangles)
        {
            for (int i = 0; i < triangles.Count; i += 3)
            {
                int vertIndex1 = triangles[i];
                int vertIndex2 = triangles[i + 1];
                int vertIndex3 = triangles[i + 2];

                Vector3 triangleNormal = ComputeNormal(vertices[vertIndex1], vertices[vertIndex2], vertices[vertIndex3]);
                normals[vertIndex1] += triangleNormal;
                normals[vertIndex2] += triangleNormal;
                normals[vertIndex3] += triangleNormal;
            }
        }
    }
}
