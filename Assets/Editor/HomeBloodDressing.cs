using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Survivor.EditorTools
{
    /// <summary>Texture-free surface dressing; does not alter room lighting or collision.</summary>
    public static class HomeBloodDressing
    {
        public static void Build(Transform parent, Func<float, float, float, Vector3> convert, Vector3 xAxis, Vector3 zAxis)
        {
            var root = new GameObject("BloodDressing").transform;
            root.SetParent(parent, false);
            var dark = Material("DiscoveryBlood", new Color(.19f, .008f, .017f), .20f);
            var wet = Material("DiscoveryBloodFresh", new Color(.34f, .012f, .025f), .58f);
            Mesh blob = Blob();
            void Splat(string name, float x, float z, float sx, float sz, float angle, Material material, float y = .014f)
            {
                var item = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
                item.transform.SetParent(root, false);
                item.transform.position = convert(x, y, z);
                item.transform.rotation = Quaternion.LookRotation(zAxis, Vector3.up) * Quaternion.Euler(0, angle, 0);
                item.transform.localScale = new Vector3(sx, 1, sz);
                item.GetComponent<MeshFilter>().sharedMesh = blob;
                var renderer = item.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = true;
            }
            Splat("Blood pool - irregular outer edge", -3.05f, 2.65f, 2.55f, 1.42f, 9, dark);
            Splat("Blood pool - fresh centre", -3.15f, 2.62f, 1.55f, .94f, -16, wet, .017f);
            Splat("Blood pool - side lobe", -2.37f, 2.42f, .82f, .52f, 30, wet, .019f);
            Splat("Blood pool - head-side lobe", -3.95f, 2.76f, .57f, .65f, -22, dark, .018f);
            for (int i = 0; i < 5; i++)
                Splat("Broken drag smear " + i, -2.7f + Mathf.Sin(i * 2f) * .12f, 2.12f - i * .34f,
                    .22f - i * .018f, .43f, -8 + i * 9, i % 2 == 0 ? wet : dark, .018f);
            var random = new System.Random(22102026);
            for (int i = 0; i < 26; i++)
            {
                float angle = (float)random.NextDouble() * Mathf.PI * 2;
                float radius = .7f + (float)random.NextDouble() * .85f;
                float size = .035f + (float)random.NextDouble() * .095f;
                Splat("Blood droplet " + i, -3.05f + Mathf.Cos(angle) * radius,
                    2.65f + Mathf.Sin(angle) * radius * .66f, size, size * 1.45f, angle * Mathf.Rad2Deg, wet, .016f);
            }
            Splat("Bloody partial footprint A", -2.48f, .62f, .12f, .29f, -18, dark);
            Splat("Bloody partial footprint B", -2.75f, .08f, .11f, .25f, 12, dark);
            for (int i = 0; i < 6; i++)
            {
                var item = new GameObject("Hearth blood fleck " + i, typeof(MeshFilter), typeof(MeshRenderer));
                item.transform.SetParent(root, false);
                item.transform.position = convert(-5.176f, .24f + i * .13f, 2.3f + Mathf.Sin(i * 2.2f) * .33f);
                item.transform.rotation = Quaternion.FromToRotation(Vector3.up, xAxis.normalized);
                item.transform.localScale = new Vector3(.10f + i * .018f, 1, .18f);
                item.GetComponent<MeshFilter>().sharedMesh = blob;
                var renderer = item.GetComponent<MeshRenderer>(); renderer.sharedMaterial = dark;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
            Debug.Log("HOME BLOOD PASS: shared 26-triangle stain mesh, two materials, irregular layered pool, droplets/smears/partial footprints/hearth flecks; zero stain colliders, no lighting changes.");
        }
        private static Material Material(string name, Color color, float smoothness)
        {
            string path = "Assets/Materials/HomeInterior/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name }; AssetDatabase.CreateAsset(material, path); }
            material.SetColor("_BaseColor", color); material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material); return material;
        }
        private static Mesh Blob()
        {
            const string path = "Assets/Models/Levels/HomeInterior/BloodStain.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) return existing;
            const int segments = 26;
            var vertices = new Vector3[segments + 1]; var indices = new int[segments * 3];
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2 / segments;
                float r = .5f * (1 + .14f * Mathf.Sin(angle * 5 + .3f) + .08f * Mathf.Cos(angle * 9));
                vertices[i + 1] = new Vector3(Mathf.Cos(angle) * r, 0, Mathf.Sin(angle) * r);
                indices[i * 3] = 0; indices[i * 3 + 1] = (i + 1) % segments + 1; indices[i * 3 + 2] = i + 1;
            }
            var mesh = new Mesh { name = "Irregular blood stain", vertices = vertices, triangles = indices };
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh, path); return mesh;
        }
    }
}
