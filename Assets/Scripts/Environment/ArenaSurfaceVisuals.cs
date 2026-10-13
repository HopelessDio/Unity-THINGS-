using System.Collections.Generic;
using UnityEngine;

namespace Survivor.Environment
{
    public sealed class ArenaSurfaceVisuals : MonoBehaviour
    {
        [Header("Surface Decals")]
        [SerializeField] private Material bloodMaterial;
        [SerializeField] private Material crackMaterial;

        [Header("Abyss")]
        [SerializeField] private Material stoneMaterial;
        [SerializeField] private Material abyssGlowMaterial;
        [SerializeField, Min(0f)] private float pitDepth = 6f;

        private readonly List<Mesh> generatedMeshes = new();

        private static readonly Vector4[] BloodPlacements =
        {
            new(-22f, 13f, 6.5f, 18f),
            new(-10f, 19f, 4.4f, -31f),
            new(5f, 21f, 5.2f, 63f),
            new(19f, 12f, 3.7f, -12f),
            new(-19f, -2f, 4.8f, 42f),
            new(-7f, 4f, 3.3f, -67f),
            new(8f, -5f, 6.2f, 9f),
            new(21f, -7f, 4.1f, 74f),
            new(-20f, -19f, 5.7f, -46f),
            new(-6f, -22f, 3.8f, 27f),
            new(7f, -17f, 4.6f, -8f),
            new(14f, 5f, 3.1f, 51f)
        };

        private void Awake()
        {
            BuildArenaDetails();
        }

        private void BuildArenaDetails()
        {
            if (transform.Find("GeneratedArenaDetails") != null)
            {
                return;
            }

            Transform pit = FindPit();
            if (pit == null)
            {
                Debug.LogWarning("ArenaSurfaceVisuals could not find FloorSlabPit.", this);
                return;
            }

            Transform detailsRoot = new GameObject("GeneratedArenaDetails").transform;
            detailsRoot.SetParent(transform, false);

            CreateBloodDecals(detailsRoot, pit.position);
            CreateDecal(
                "PitFractures",
                detailsRoot,
                new Vector3(pit.position.x, 0.035f, pit.position.z),
                new Vector2(14f, 14f),
                17f,
                crackMaterial,
                12);

            CreateAbyss(detailsRoot, pit.position);
        }

        private Transform FindPit()
        {
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                if (child.name.StartsWith("FloorSlabPit"))
                {
                    return child;
                }
            }

            return null;
        }

        private void CreateBloodDecals(Transform parent, Vector3 pitPosition)
        {
            foreach (Vector4 placement in BloodPlacements)
            {
                Vector3 position = new(placement.x, 0.02f, placement.y);
                Vector2 flatOffset = new(position.x - pitPosition.x, position.z - pitPosition.z);
                if (flatOffset.sqrMagnitude < 55f)
                {
                    continue;
                }

                float aspect = 1.5f;
                CreateDecal(
                    "BloodStain",
                    parent,
                    position,
                    new Vector2(placement.z * aspect, placement.z),
                    placement.w,
                    bloodMaterial,
                    10);
            }
        }

        private void CreateAbyss(Transform parent, Vector3 pitPosition)
        {
            Transform abyssRoot = new GameObject("HellDepth").transform;
            abyssRoot.SetParent(parent, false);
            abyssRoot.position = pitPosition;

            for (int level = 0; level < 4; level++)
            {
                float y = -0.75f - level * 1.25f;
                float size = 5.65f - level * 0.55f;
                Material material = level % 2 == 0 ? abyssGlowMaterial : stoneMaterial;
                CreateDepthRing(abyssRoot, y, size, material, level);
            }

            CreateDecal(
                "AbyssCore",
                abyssRoot,
                new Vector3(0f, -pitDepth, 0f),
                new Vector2(3.2f, 3.2f),
                0f,
                abyssGlowMaterial,
                0,
                false);

            GameObject lightObject = new("AbyssCrimsonLight");
            lightObject.transform.SetParent(abyssRoot, false);
            lightObject.transform.localPosition = new Vector3(0f, -3.5f, 0f);
            Light abyssLight = lightObject.AddComponent<Light>();
            abyssLight.type = LightType.Point;
            abyssLight.color = new Color(1f, 0.015f, 0.04f);
            abyssLight.intensity = 2.25f;
            abyssLight.range = 8f;
            abyssLight.shadows = LightShadows.None;
        }

        private void CreateDepthRing(Transform parent, float y, float size, Material material, int level)
        {
            const float thickness = 0.14f;
            const float height = 0.12f;

            CreateBar($"DepthRing{level}_North", parent, new Vector3(0f, y, size * 0.5f), new Vector3(size, height, thickness), material);
            CreateBar($"DepthRing{level}_South", parent, new Vector3(0f, y, size * -0.5f), new Vector3(size, height, thickness), material);
            CreateBar($"DepthRing{level}_East", parent, new Vector3(size * 0.5f, y, 0f), new Vector3(thickness, height, size), material);
            CreateBar($"DepthRing{level}_West", parent, new Vector3(size * -0.5f, y, 0f), new Vector3(thickness, height, size), material);
        }

        private static void CreateBar(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bar.name = name;
            bar.transform.SetParent(parent, false);
            bar.transform.localPosition = position;
            bar.transform.localScale = scale;

            Collider barCollider = bar.GetComponent<Collider>();
            barCollider.enabled = false;
            Destroy(barCollider);

            MeshRenderer renderer = bar.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private void CreateDecal(
            string objectName,
            Transform parent,
            Vector3 position,
            Vector2 size,
            float rotation,
            Material material,
            int sortingOrder,
            bool worldSpace = true)
        {
            GameObject decal = new(objectName);
            decal.transform.SetParent(parent, false);
            if (worldSpace)
            {
                decal.transform.position = position;
            }
            else
            {
                decal.transform.localPosition = position;
            }
            decal.transform.localRotation = Quaternion.Euler(0f, rotation, 0f);
            decal.transform.localScale = new Vector3(size.x, 1f, size.y);

            MeshFilter filter = decal.AddComponent<MeshFilter>();
            Mesh mesh = CreateHorizontalQuad();
            filter.sharedMesh = mesh;
            generatedMeshes.Add(mesh);

            MeshRenderer renderer = decal.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = sortingOrder;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static Mesh CreateHorizontalQuad()
        {
            Mesh mesh = new() { name = "ArenaDetailQuad" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, 0f, -0.5f),
                new Vector3(-0.5f, 0f, 0.5f),
                new Vector3(0.5f, 0f, 0.5f),
                new Vector3(0.5f, 0f, -0.5f)
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 0f)
            };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private void OnDestroy()
        {
            foreach (Mesh mesh in generatedMeshes)
            {
                if (mesh != null)
                {
                    Destroy(mesh);
                }
            }
        }
    }
}
