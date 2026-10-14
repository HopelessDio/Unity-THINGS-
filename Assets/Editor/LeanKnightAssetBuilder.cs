using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Survivor.EditorTools
{
    /// <summary>Prepares standalone character and weapon assets without changing the arena scene.</summary>
    public static class LeanKnightAssetBuilder
    {
        private const string KnightPath = "Assets/Models/Player/LeanKnight_Model01.fbx";
        private const string SwordPath = "Assets/Models/Player/LeanKnight_Longsword01.fbx";
        private const string PendingPath = "Assets/Editor/LeanKnightAssets.pending";
        private const string MaterialsPath = "Assets/Materials/LeanKnight";
        private const string PrefabsPath = "Assets/Prefab/Player";

        [InitializeOnLoadMethod]
        private static void WhenRequested()
        {
            if (File.Exists(PendingPath))
            {
                EditorApplication.delayCall += PrepareWhenReady;
            }
        }

        private static void PrepareWhenReady()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlaying)
            {
                EditorApplication.delayCall += PrepareWhenReady;
                return;
            }

            try
            {
                Prepare();
                File.Delete(PendingPath);
                AssetDatabase.Refresh();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        [MenuItem("Survivor/Prepare Lean Knight and Separate Sword")]
        public static void Prepare()
        {
            EnsureFolder("Assets/Materials", "LeanKnight");
            EnsureFolder("Assets/Prefab", "Player");
            Import(KnightPath, true);
            Import(SwordPath, false);

            var materials = new Dictionary<string, Material>
            {
                ["LeanKnight_SlateSteel"] = MakeMaterial("SlateSteel", new Color(0.18f, 0.21f, 0.25f), 0.68f, 0.48f),
                ["LeanKnight_AgedBronze"] = MakeMaterial("AgedBronze", new Color(0.43f, 0.33f, 0.2f), 0.7f, 0.45f),
                ["LeanKnight_CharcoalCloth"] = MakeMaterial("CharcoalCloth", new Color(0.055f, 0.048f, 0.055f), 0f, 0.05f),
                ["LeanKnight_CrimsonCape"] = MakeMaterial("CrimsonCape", new Color(0.31f, 0.065f, 0.059f), 0f, 0.1f),
                ["LeanKnight_Visor"] = MakeMaterial("Visor", new Color(0.008f, 0.009f, 0.013f), 0.1f, 0.15f),
                ["LeanKnight_BladeSteel"] = MakeMaterial("BladeSteel", new Color(0.55f, 0.59f, 0.63f), 0.83f, 0.7f),
            };

            SavePrefab(KnightPath, "LeanKnight", materials, true);
            SavePrefab(SwordPath, "LeanKnight_Longsword", materials, false);
            AssetDatabase.SaveAssets();
            Debug.Log("Lean knight assets prepared: rigged LeanKnight.prefab and independent LeanKnight_Longsword.prefab. SwordSocket_R is ready for equipping; arena scene was not edited.");
        }

        private static void Import(string path, bool rigged)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                throw new InvalidOperationException($"Model is missing: {path}");
            }

            importer.animationType = rigged ? ModelImporterAnimationType.Generic : ModelImporterAnimationType.None;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.isReadable = false;
            importer.globalScale = 1f;
            importer.SaveAndReimport();
        }

        private static void SavePrefab(string modelPath, string name,
            IReadOnlyDictionary<string, Material> materials, bool rigged)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null) throw new InvalidOperationException($"Model could not be loaded: {modelPath}");
            GameObject instance = UnityEngine.Object.Instantiate(model);
            try
            {
                instance.name = name;
                foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
                {
                    Material[] assigned = renderer.sharedMaterials;
                    for (int i = 0; i < assigned.Length; i++)
                    {
                        if (assigned[i] != null && materials.TryGetValue(assigned[i].name, out Material replacement))
                        {
                            assigned[i] = replacement;
                        }
                    }

                    renderer.sharedMaterials = assigned;
                }

                if (rigged)
                {
                    SkinnedMeshRenderer skin = instance.GetComponentInChildren<SkinnedMeshRenderer>(true);
                    // Unity may omit unweighted Root/Spine bones from the renderer's
                    // bone array even though they remain in the imported hierarchy.
                    if (skin == null || skin.bones.Length < 14)
                        throw new InvalidOperationException("Knight import did not preserve the skeleton and skinned mesh.");
                    bool hasSocket = false;
                    foreach (Transform child in instance.GetComponentsInChildren<Transform>(true))
                        hasSocket |= child.name == "SwordSocket_R";
                    if (!hasSocket) throw new InvalidOperationException("Right-hand sword socket is missing.");
                }

                PrefabUtility.SaveAsPrefabAsset(instance, $"{PrefabsPath}/{name}.prefab");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static Material MakeMaterial(string name, Color color, float metallic, float smoothness)
        {
            string path = $"{MaterialsPath}/{name}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (mat == null)
            {
                mat = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{name}"))
                AssetDatabase.CreateFolder(parent, name);
        }
    }
}
