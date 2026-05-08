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
        private readonly List<Vector2> positiveSideUvs;
        private readonly List<Vector3> positiveSideNormals;

        private Mesh negativeSideMesh;
        private readonly List<Vector3> negativeSideVertices;
        private readonly List<int> negativeSideTriangles;
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
            positiveSideVertices = new List<Vector3>();
            negativeSideTriangles = new List<int>();
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
            bool addFirst)
        {
            if (side == MeshSide.Positive)
            {
                AddTrianglesNormalsAndUvs(
                    ref positiveSideVertices,
                    ref positiveSideTriangles,
                    ref positiveSideNormals,
                    ref positiveSideUvs,
                    vertex1,
                    normal1,
                    uv1,
                    vertex2,
                    normal2,
                    uv2,
                    vertex3,
                    normal3,
                    uv3,
                    shareVertices,
                    addFirst);
            }
            else
            {
                AddTrianglesNormalsAndUvs(
                    ref negativeSideVertices,
                    ref negativeSideTriangles,
                    ref negativeSideNormals,
                    ref negativeSideUvs,
                    vertex1,
                    normal1,
                    uv1,
                    vertex2,
                    normal2,
                    uv2,
                    vertex3,
                    normal3,
                    uv3,
                    shareVertices,
                    addFirst);
            }
        }

        private void AddTrianglesNormalsAndUvs(
            ref List<Vector3> vertices,
            ref List<int> triangles,
            ref List<Vector3> normals,
            ref List<Vector2> uvs,
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
            bool addFirst)
        {
            int tri1Index = vertices.IndexOf(vertex1);

            if (addFirst)
                ShiftTriangleIndices(ref triangles);

            if (tri1Index > -1 && shareVertices)
            {
                triangles.Add(tri1Index);
            }
            else
            {
                normal1 ??= ComputeNormal(vertex1, vertex2, vertex3);
                AddVertNormalUv(ref vertices, ref normals, ref uvs, ref triangles, vertex1, normal1.Value, uv1, addFirst ? 0 : null);
            }

            int tri2Index = vertices.IndexOf(vertex2);
            if (tri2Index > -1 && shareVertices)
            {
                triangles.Add(tri2Index);
            }
            else
            {
                normal2 ??= ComputeNormal(vertex2, vertex3, vertex1);
                AddVertNormalUv(ref vertices, ref normals, ref uvs, ref triangles, vertex2, normal2.Value, uv2, addFirst ? 1 : null);
            }

            int tri3Index = vertices.IndexOf(vertex3);
            if (tri3Index > -1 && shareVertices)
            {
                triangles.Add(tri3Index);
            }
            else
            {
                normal3 ??= ComputeNormal(vertex3, vertex1, vertex2);
                AddVertNormalUv(ref vertices, ref normals, ref uvs, ref triangles, vertex3, normal3.Value, uv3, addFirst ? 2 : null);
            }
        }

        private static void AddVertNormalUv(
            ref List<Vector3> vertices,
            ref List<Vector3> normals,
            ref List<Vector2> uvs,
            ref List<int> triangles,
            Vector3 vertex,
            Vector3 normal,
            Vector2 uv,
            int? index)
        {
            if (index != null)
            {
                int i = index.Value;
                vertices.Insert(i, vertex);
                uvs.Insert(i, uv);
                normals.Insert(i, normal);
                triangles.Insert(i, i);
            }
            else
            {
                vertices.Add(vertex);
                normals.Add(normal);
                uvs.Add(uv);
                triangles.Add(vertices.IndexOf(vertex));
            }
        }

        private static void ShiftTriangleIndices(ref List<int> triangles)
        {
            for (int j = 0; j < triangles.Count; j += 3)
            {
                triangles[j] += 3;
                triangles[j + 1] += 3;
                triangles[j + 2] += 3;
            }
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
        }

        private void JoinPointsAlongPlane()
        {
            Vector3 halfway = GetHalfwayPoint(out _);

            for (int i = 0; i < pointsAlongPlane.Count; i += 2)
            {
                Vector3 firstVertex = pointsAlongPlane[i];
                Vector3 secondVertex = pointsAlongPlane[i + 1];

                Vector3 normal = ComputeNormal(halfway, secondVertex, firstVertex).normalized;
                float direction = Vector3.Dot(normal, plane.normal);

                if (direction > 0)
                {
                    AddTrianglesNormalAndUvs(MeshSide.Positive, halfway, -normal, Vector2.zero, firstVertex, -normal, Vector2.zero, secondVertex, -normal, Vector2.zero, false, true);
                    AddTrianglesNormalAndUvs(MeshSide.Negative, halfway, normal, Vector2.zero, secondVertex, normal, Vector2.zero, firstVertex, normal, Vector2.zero, false, true);
                }
                else
                {
                    AddTrianglesNormalAndUvs(MeshSide.Positive, halfway, normal, Vector2.zero, secondVertex, normal, Vector2.zero, firstVertex, normal, Vector2.zero, false, true);
                    AddTrianglesNormalAndUvs(MeshSide.Negative, halfway, -normal, Vector2.zero, firstVertex, -normal, Vector2.zero, secondVertex, -normal, Vector2.zero, false, true);
                }
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

        private void SetMeshData(MeshSide side)
        {
            if (side == MeshSide.Positive)
            {
                positiveSideMesh.vertices = positiveSideVertices.ToArray();
                positiveSideMesh.triangles = positiveSideTriangles.ToArray();
                positiveSideMesh.normals = positiveSideNormals.ToArray();
                positiveSideMesh.uv = positiveSideUvs.ToArray();
            }
            else
            {
                negativeSideMesh.vertices = negativeSideVertices.ToArray();
                negativeSideMesh.triangles = negativeSideTriangles.ToArray();
                negativeSideMesh.normals = negativeSideNormals.ToArray();
                negativeSideMesh.uv = negativeSideUvs.ToArray();
            }
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
                    AddTrianglesNormalAndUvs(side, vert1, normal1, uv1, vert2, normal2, uv2, vert3, normal3, uv3, true, false);
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

                    AddTrianglesNormalAndUvs(side1, vert1, null, uv1, vert2, null, uv2, intersection1, null, intersection1Uv, useSharedVertices, false);
                    AddTrianglesNormalAndUvs(side1, vert1, null, uv1, intersection1, null, intersection1Uv, intersection2, null, intersection2Uv, useSharedVertices, false);
                    AddTrianglesNormalAndUvs(side2, intersection1, null, intersection1Uv, vert3, null, uv3, intersection2, null, intersection2Uv, useSharedVertices, false);
                }
                else if (vert1Side == vert3Side)
                {
                    intersection1 = GetRayPlaneIntersectionPointAndUv(vert1, uv1, vert2, uv2, out intersection1Uv);
                    intersection2 = GetRayPlaneIntersectionPointAndUv(vert2, uv2, vert3, uv3, out intersection2Uv);

                    AddTrianglesNormalAndUvs(side1, vert1, null, uv1, intersection1, null, intersection1Uv, vert3, null, uv3, useSharedVertices, false);
                    AddTrianglesNormalAndUvs(side1, intersection1, null, intersection1Uv, intersection2, null, intersection2Uv, vert3, null, uv3, useSharedVertices, false);
                    AddTrianglesNormalAndUvs(side2, intersection1, null, intersection1Uv, vert2, null, uv2, intersection2, null, intersection2Uv, useSharedVertices, false);
                }
                else
                {
                    intersection1 = GetRayPlaneIntersectionPointAndUv(vert1, uv1, vert2, uv2, out intersection1Uv);
                    intersection2 = GetRayPlaneIntersectionPointAndUv(vert1, uv1, vert3, uv3, out intersection2Uv);

                    AddTrianglesNormalAndUvs(side1, vert1, null, uv1, intersection1, null, intersection1Uv, intersection2, null, intersection2Uv, useSharedVertices, false);
                    AddTrianglesNormalAndUvs(side2, intersection1, null, intersection1Uv, vert2, null, uv2, vert3, null, uv3, useSharedVertices, false);
                    AddTrianglesNormalAndUvs(side2, intersection1, null, intersection1Uv, vert3, null, uv3, intersection2, null, intersection2Uv, useSharedVertices, false);
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
            DoSmoothing(ref positiveSideVertices, ref positiveSideNormals, ref positiveSideTriangles);
            DoSmoothing(ref negativeSideVertices, ref negativeSideNormals, ref negativeSideTriangles);
        }

        private static void DoSmoothing(ref List<Vector3> vertices, ref List<Vector3> normals, ref List<int> triangles)
        {
            for (int i = 0; i < normals.Count; i++)
                normals[i] = Vector3.zero;

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

            for (int i = 0; i < normals.Count; i++)
                normals[i] = normals[i].normalized;
        }
    }
}
