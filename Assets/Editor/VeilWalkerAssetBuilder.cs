using System;
using System.Collections.Generic;
using System.IO;
using Survivor.Player;
using UnityEditor;
using UnityEngine;

namespace Survivor.EditorTools
{
    public static class VeilWalkerAssetBuilder
    {
        private const string CharacterPath = "Assets/Models/Player/VeilWalker_Model01.fbx";
        private const string SwordPath = "Assets/Models/Player/LeanKnight_Longsword01.fbx";
        private const string GunPath = "Assets/Models/Player/VeilWalker_Pistol01.fbx";
        private const string PendingPath = "Assets/Editor/VeilWalkerAssets.pending";
        private const string PrefabsPath = "Assets/Prefab/Player/VeilWalker";
        private const string MaterialsPath = "Assets/Materials/VeilWalker";

        [InitializeOnLoadMethod]
        private static void OnLoad()
        {
            if (File.Exists(PendingPath)) EditorApplication.delayCall += WhenReady;
        }

        private static void WhenReady()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlaying)
            {
                EditorApplication.delayCall += WhenReady;
                return;
            }
            try
            {
                Prepare();
                AssetDatabase.DeleteAsset(PendingPath);
                if (File.Exists(PendingPath)) File.Delete(PendingPath);
                AssetDatabase.Refresh();
            }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        [MenuItem("Survivor/Prepare Veil Walker Two-Handed Sword")]
        public static void Prepare()
        {
            Folder("Assets/Materials", "VeilWalker");
            Folder("Assets/Prefab", "Player");
            Folder("Assets/Prefab/Player", "VeilWalker");
            Import(CharacterPath, true);
            Import(SwordPath, false);
            var materials = new Dictionary<string, Material>
            {
                ["Veil_Slate"] = Material("Slate", new Color(.045f,.052f,.063f), .12f, .22f),
                ["Veil_Joints"] = Material("DarkFaceAndCloth", new Color(.014f,.017f,.022f), 0f, .05f),
                ["Veil_Burgundy"] = Material("BurgundyCape", new Color(.115f,.025f,.023f), 0f, .14f),
                ["Veil_IvoryMask"] = Material("IvoryMask", new Color(.66f,.59f,.46f), .03f, .36f),
                ["Veil_AgedBronze"] = Material("AgedBronze", new Color(.32f,.22f,.105f), .5f, .45f),
                ["Veil_EyeVoid"] = Material("EyeVoid", new Color(.001f,.002f,.003f), 0f, 0f),
                ["VeilGun_Steel"] = Material("GunSteel", new Color(.055f,.062f,.075f), .75f, .6f),
                ["VeilGun_Bronze"] = Material("GunBronze", new Color(.32f,.22f,.105f), .65f, .57f),
                ["VeilGun_Grip"] = Material("GunGrip", new Color(.02f,.013f,.015f), 0f, .2f),
                ["VeilGun_MuzzleDark"] = Material("GunMuzzleDark", new Color(.001f,.002f,.003f), 0f, 0f),
                ["LeanKnight_BladeSteel"] = Material("SwordSteel", new Color(.42f,.46f,.5f), .83f, .7f),
                ["LeanKnight_AgedBronze"] = Material("SwordBronze", new Color(.3f,.23f,.13f), .7f, .55f),
                ["LeanKnight_CharcoalCloth"] = Material("SwordGrip", new Color(.026f,.023f,.026f), 0f, .05f),
                ["LeanKnight_Visor"] = Material("SwordGripWrap", new Color(.003f,.004f,.006f), .1f, .4f),
            };
            GameObject sword = BuildPrefab(SwordPath, "VeilWalker_Sword", materials, false);
            GameObject character = BuildPrefab(CharacterPath, "VeilWalker", materials, true);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(character);
            try
            {
                instance.name = "VeilWalker_Equipped";
                PlayerWeaponEquipment equipment = instance.GetComponent<PlayerWeaponEquipment>();
                equipment.EquipSword(sword);
                equipment.EquipGun(null);
                if (equipment.SwordOffhandGrip == null || equipment.EquippedGun != null)
                    throw new InvalidOperationException("Default loadout must be a two-handed sword with no gun.");
                PrefabUtility.SaveAsPrefabAsset(instance, $"{PrefabsPath}/VeilWalker_Equipped.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
            AssetDatabase.SaveAssets();
            Debug.Log("Veil Walker prepared: default two-handed sword grip, independent sword, offhand target, and no equipped gun. Gameplay scene unchanged.");
        }

        private static void Import(string path, bool character)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) throw new InvalidOperationException($"Missing model: {path}");
            importer.animationType = character ? ModelImporterAnimationType.Generic : ModelImporterAnimationType.None;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.optimizeGameObjects = false;
            importer.globalScale = 1f;
            importer.SaveAndReimport();
        }

        private static GameObject BuildPrefab(string path, string name,
            IReadOnlyDictionary<string, Material> materials, bool character)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (source == null) throw new InvalidOperationException($"Cannot load {path}");
            GameObject instance = UnityEngine.Object.Instantiate(source);
            try
            {
                instance.name = name;
                foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
                {
                    Material[] slots = renderer.sharedMaterials;
                    for (int i = 0; i < slots.Length; i++)
                        if (slots[i] != null && materials.TryGetValue(slots[i].name, out var replacement))
                            slots[i] = replacement;
                    renderer.sharedMaterials = slots;
                }
                if (character)
                {
                    var skin = instance.GetComponentInChildren<SkinnedMeshRenderer>(true);
                    if (skin == null || skin.bones.Length < 18)
                        throw new InvalidOperationException("Character skeleton or skin weights were lost during import.");
                    Transform sword = Find(instance, "SwordSocket_R");
                    Transform gun = Find(instance, "GunSocket_L");
                    if (sword == null || gun == null)
                        throw new InvalidOperationException("Character weapon sockets are missing.");
                    Transform support = Find(instance, "SwordOffhandGrip");
                    if (support == null)
                        throw new InvalidOperationException("The sword offhand grip target is missing.");
                    PlayerWeaponEquipment equipment = instance.AddComponent<PlayerWeaponEquipment>();
                    equipment.ConfigureSockets(sword, gun);
                    equipment.ConfigureSwordSupport(support);
                }
                else if (name == "VeilWalker_Pistol" && Find(instance, "Muzzle") == null)
                    throw new InvalidOperationException("Pistol muzzle is missing.");
                return PrefabUtility.SaveAsPrefabAsset(instance, $"{PrefabsPath}/{name}.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }

        private static Transform Find(GameObject root, string name)
        {
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                if (transform.name == name) return transform;
            return null;
        }

        private static Material Material(string name, Color linearColor, float metal, float smoothness)
        {
            string path = $"{MaterialsPath}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (mat == null)
            {
                mat = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            mat.SetColor("_BaseColor", linearColor.gamma);
            mat.SetFloat("_Metallic", metal);
            mat.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void Folder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{child}")) AssetDatabase.CreateFolder(parent, child);
        }
    }
}
