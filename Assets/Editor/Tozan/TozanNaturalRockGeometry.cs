using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Tozan.Editor
{
    /// <summary>
    /// Procedural unmarked rock meshes for STEP 13.
    /// No Vault tags, no DPS HandlePoints / Point, no DPS environment prefabs.
    /// Layer stays Default — the test is whether DPS can read shape alone.
    /// </summary>
    public static class TozanNaturalRockGeometry
    {
        const string MeshFolder = "Assets/Meshes/NaturalRocks";

        public static void BuildCourse(Transform parent, Material rockMat)
        {
            EnsureMeshFolder();

            // Confirmation wall: front face stays at z≈1.275 for existing climb tests.
            CreateBoxRock("Rock_VerticalWall", new Vector3(0f, 4f, 1.55f), new Vector3(12f, 8f, 0.55f),
                Quaternion.identity, parent, rockMat);

            CreateBoxRock("Rock_Slope80", new Vector3(-10f, 2.05f, 6f), new Vector3(3.2f, 4.1f, 0.55f),
                Quaternion.Euler(10f, 0f, 0f), parent, rockMat);

            var shelf = CreateEmpty("Rock_OverhangShelf", new Vector3(-5.5f, 0f, 6f), parent);
            CreateBoxRock("Wall", new Vector3(0f, 1.1f, 0.4f), new Vector3(3f, 2.2f, 0.5f),
                Quaternion.identity, shelf, rockMat);
            CreateBoxRock("Lip", new Vector3(0f, 2.32f, -0.35f), new Vector3(3f, 0.35f, 1.4f),
                Quaternion.identity, shelf, rockMat);

            var overhang = CreateEmpty("Rock_Overhang", new Vector3(5.5f, 0f, 6f), parent);
            CreateBoxRock("Wall", new Vector3(0f, 1.15f, 0.5f), new Vector3(3f, 2.3f, 0.5f),
                Quaternion.identity, overhang, rockMat);
            CreateBoxRock("Roof", new Vector3(0f, 2.45f, -0.7f), new Vector3(3.2f, 0.28f, 1.8f),
                Quaternion.identity, overhang, rockMat);

            var trapMesh = SaveMesh(CreateTrapezoidLedge(3.6f, 1.15f, 2.15f, 0.85f), "VariableWidthLedge");
            CreateMeshRock("Rock_VariableWidthLedge", new Vector3(10.5f, 0f, 6.2f), Quaternion.identity,
                trapMesh, rockMat, parent);

            var convex = CreateEmpty("Rock_ConvexCorner", new Vector3(-8f, 0f, 14f), parent);
            CreateBoxRock("FaceZ", new Vector3(1.2f, 1.2f, 0f), new Vector3(2.4f, 2.4f, 0.5f),
                Quaternion.identity, convex, rockMat);
            CreateBoxRock("FaceX", new Vector3(0f, 1.2f, 1.2f), new Vector3(0.5f, 2.4f, 2.4f),
                Quaternion.identity, convex, rockMat);

            var concave = CreateEmpty("Rock_ConcaveCorner", new Vector3(8f, 0f, 14f), parent);
            CreateBoxRock("Back", new Vector3(0f, 1.2f, 1.6f), new Vector3(3.6f, 2.4f, 0.5f),
                Quaternion.identity, concave, rockMat);
            CreateBoxRock("Side", new Vector3(1.8f, 1.2f, 0f), new Vector3(0.5f, 2.4f, 3.2f),
                Quaternion.identity, concave, rockMat);

            var stepped = CreateEmpty("Rock_SteppedLedges", new Vector3(0f, 0f, 16.5f), parent);
            CreateBoxRock("Shelf_Low", new Vector3(0f, 0.9f, 0f), new Vector3(2.6f, 0.35f, 0.85f),
                Quaternion.identity, stepped, rockMat);
            CreateBoxRock("Shelf_Mid", new Vector3(0f, 1.55f, 0.7f), new Vector3(2.1f, 0.35f, 0.85f),
                Quaternion.identity, stepped, rockMat);
            CreateBoxRock("Shelf_High", new Vector3(0f, 2.2f, 1.4f), new Vector3(1.6f, 0.35f, 0.85f),
                Quaternion.identity, stepped, rockMat);

            var irregular = SaveMesh(CreateDisplacedBox(new Vector3(2.8f, 2.6f, 1.4f), 0.22f, 13), "Irregular");
            CreateMeshRock("Rock_Irregular", new Vector3(0f, 1.3f, 22f), Quaternion.identity,
                irregular, rockMat, parent);
        }

        static Transform CreateEmpty(string name, Vector3 position, Transform parent)
        {
            var go = new GameObject(name);
            go.tag = "Untagged";
            go.layer = 0;
            go.transform.SetParent(parent, true);
            go.transform.position = position;
            return go.transform;
        }

        static GameObject CreateBoxRock(string name, Vector3 localPos, Vector3 scale, Quaternion rotation,
            Transform parent, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.tag = "Untagged";
            go.layer = 0;
            go.isStatic = true;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = rotation;
            go.transform.localScale = scale;
            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null && mat != null)
                renderer.sharedMaterial = mat;
            return go;
        }

        static GameObject CreateMeshRock(string name, Vector3 position, Quaternion rotation, Mesh mesh,
            Material mat, Transform parent)
        {
            var go = new GameObject(name);
            go.tag = "Untagged";
            go.layer = 0;
            go.isStatic = true;
            go.transform.SetParent(parent, true);
            go.transform.SetPositionAndRotation(position, rotation);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = mat;
            var col = go.AddComponent<MeshCollider>();
            col.sharedMesh = mesh;
            col.convex = false;
            return go;
        }

        static Mesh CreateTrapezoidLedge(float bottomWidth, float topWidth, float height, float depth)
        {
            var bw = bottomWidth * 0.5f;
            var tw = topWidth * 0.5f;
            var verts = new List<Vector3>();
            var tris = new List<int>();

            AddQuad(verts, tris,
                new Vector3(-bw, 0f, 0f), new Vector3(bw, 0f, 0f),
                new Vector3(tw, height, 0f), new Vector3(-tw, height, 0f));
            AddQuad(verts, tris,
                new Vector3(bw, 0f, depth), new Vector3(tw, height, depth),
                new Vector3(-tw, height, depth), new Vector3(-bw, 0f, depth));
            AddQuad(verts, tris,
                new Vector3(-tw, height, 0f), new Vector3(-tw, height, depth),
                new Vector3(tw, height, depth), new Vector3(tw, height, 0f));
            AddQuad(verts, tris,
                new Vector3(-bw, 0f, depth), new Vector3(-bw, 0f, 0f),
                new Vector3(bw, 0f, 0f), new Vector3(bw, 0f, depth));
            AddQuad(verts, tris,
                new Vector3(-bw, 0f, depth), new Vector3(-tw, height, depth),
                new Vector3(-tw, height, 0f), new Vector3(-bw, 0f, 0f));
            AddQuad(verts, tris,
                new Vector3(bw, 0f, 0f), new Vector3(tw, height, 0f),
                new Vector3(tw, height, depth), new Vector3(bw, 0f, depth));

            var mesh = new Mesh { name = "VariableWidthLedge" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static Mesh CreateDisplacedBox(Vector3 size, float amount, int seed)
        {
            var temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var mesh = Object.Instantiate(temp.GetComponent<MeshFilter>().sharedMesh);
            Object.DestroyImmediate(temp);
            mesh.name = "IrregularRock";

            var rng = new System.Random(seed);
            var verts = mesh.vertices;
            for (var i = 0; i < verts.Length; i++)
            {
                var v = verts[i];
                v.x *= size.x;
                v.y *= size.y;
                v.z *= size.z;
                v += new Vector3(
                    ((float)rng.NextDouble() - 0.5f) * amount,
                    ((float)rng.NextDouble() - 0.5f) * amount,
                    ((float)rng.NextDouble() - 0.5f) * amount);
                verts[i] = v;
            }

            mesh.vertices = verts;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static void AddQuad(List<Vector3> verts, List<int> tris, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            var i = verts.Count;
            verts.Add(a);
            verts.Add(b);
            verts.Add(c);
            verts.Add(d);
            tris.Add(i);
            tris.Add(i + 1);
            tris.Add(i + 2);
            tris.Add(i);
            tris.Add(i + 2);
            tris.Add(i + 3);
        }

        static Mesh SaveMesh(Mesh mesh, string name)
        {
            var path = $"{MeshFolder}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
                AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);
            return AssetDatabase.LoadAssetAtPath<Mesh>(path);
        }

        static void EnsureMeshFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Meshes"))
                AssetDatabase.CreateFolder("Assets", "Meshes");
            if (!AssetDatabase.IsValidFolder(MeshFolder))
                AssetDatabase.CreateFolder("Assets/Meshes", "NaturalRocks");
        }
    }
}
