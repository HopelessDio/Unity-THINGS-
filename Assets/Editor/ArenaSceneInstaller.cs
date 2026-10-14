using System;
using System.Collections.Generic;
using System.IO;
using Survivor.Environment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Survivor.EditorTools
{
    // Installs the selected Ashen Courtyard concept as the active gameplay arena.
    public static class ArenaSceneInstaller
    {
        private const string ScenePath = "Assets/Scenes/Game.unity";
        private const string ModelPath = "Assets/Models/Arena/AshenCourtyard_Model02.fbx";
        private const string MaterialFolder = "Assets/Materials/AshenCourtyard";
        private const string InstalledRootName = "AshenCourtyard";
        private const string PendingInstallPath = "Assets/Editor/ArenaSceneInstall.pending";
        private const string PendingConceptLookPath = "Assets/Editor/ArenaConceptLook.pending";
        private const string SessionInstallKey = "Survivor.BuildableHellArena.Installing";
        private const string SessionConceptLookKey = "Survivor.BuildableHellArena.ApplyingConceptLook";

        [InitializeOnLoadMethod]
        private static void ApplyConceptLookWhenRequested()
        {
            if (!File.Exists(PendingConceptLookPath)
                || SessionState.GetBool(SessionConceptLookKey, false))
            {
                return;
            }

            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                {
                    SessionState.SetBool(SessionConceptLookKey, false);
                    ApplyConceptLookWhenRequested();
                    return;
                }

                SessionState.SetBool(SessionConceptLookKey, true);
                try
                {
                    ApplyConceptLook();
                    File.Delete(PendingConceptLookPath);
                    File.Delete(PendingConceptLookPath + ".meta");
                    AssetDatabase.Refresh();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
                finally
                {
                    SessionState.SetBool(SessionConceptLookKey, false);
                }
            };
        }

        [InitializeOnLoadMethod]
        private static void InstallWhenRequested()
        {
            if (!File.Exists(PendingInstallPath) || SessionState.GetBool(SessionInstallKey, false))
            {
                return;
            }

            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                {
                    SessionState.SetBool(SessionInstallKey, false);
                    InstallWhenRequested();
                    return;
                }

                SessionState.SetBool(SessionInstallKey, true);
                try
                {
                    Install();
                    File.Delete(PendingInstallPath);
                    File.Delete(PendingInstallPath + ".meta");
                    AssetDatabase.Refresh();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
                finally
                {
                    SessionState.SetBool(SessionInstallKey, false);
                }
            };
        }

        [MenuItem("Survivor/Install Ashen Courtyard Arena")]
        public static void Install()
        {
            AssetDatabase.ImportAsset(ModelPath,
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null)
            {
                throw new InvalidOperationException($"Could not import arena model at {ModelPath}.");
            }

            Scene currentScene = SceneManager.GetActiveScene();
            bool gameSceneWasAlreadyOpen = currentScene.path == ScenePath;
            Scene scene = gameSceneWasAlreadyOpen
                ? currentScene
                : EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            DisableLegacyArena(scene);
            RemovePreviousInstallation(scene);

            GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely,
                InteractionMode.AutomatedAction);
            root.name = InstalledRootName;
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            root.transform.localScale = Vector3.one;

            Dictionary<string, Material> materials = CreateMaterials();
            AssignMaterials(root, materials);
            ConfigureGeometry(root);
            CreateAbyssKillZone(root.transform);
            CreateConceptLighting(root.transform);
            ApplySceneLighting(scene);
            ApplyVolcanicAtmosphere(scene);
            ApplyPostProcessing(scene);
            PositionPlayerOnCentralDais(scene);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            if (!gameSceneWasAlreadyOpen)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
            Debug.Log("Ashen Courtyard installed into Game.unity with concept-matched lighting. The previous arena is disabled.");
        }

        [MenuItem("Survivor/Apply Arena Concept Look")]
        public static void ApplyConceptLook()
        {
            Scene currentScene = SceneManager.GetActiveScene();
            bool gameSceneWasAlreadyOpen = currentScene.path == ScenePath;
            Scene scene = gameSceneWasAlreadyOpen
                ? currentScene
                : EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

            GameObject arenaRoot = null;
            foreach (GameObject sceneRoot in scene.GetRootGameObjects())
            {
                if (sceneRoot.name == InstalledRootName)
                {
                    arenaRoot = sceneRoot;
                    break;
                }
            }

            if (arenaRoot == null)
            {
                throw new InvalidOperationException("AshenCourtyard is missing from Game.unity. Install the arena first.");
            }

            Dictionary<string, Material> materials = CreateMaterials();
            AssignMaterials(arenaRoot, materials);
            CreateConceptLighting(arenaRoot.transform);
            ApplySceneLighting(scene);
            ApplyVolcanicAtmosphere(scene);
            ApplyPostProcessing(scene);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            if (!gameSceneWasAlreadyOpen)
            {
                EditorSceneManager.CloseScene(scene, true);
            }

            Debug.Log("Arena concept materials and lighting applied to Game.unity.");
        }

        private static void DisableLegacyArena(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                {
                    if (transform.name == "ArenaGeometry" || transform.name == "ArenaFloor")
                    {
                        transform.gameObject.SetActive(false);
                    }
                }
            }
        }

        private static void RemovePreviousInstallation(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == InstalledRootName || root.name == "BuildableHellArena")
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }

        private static Dictionary<string, Material> CreateMaterials()
        {
            EnsureFolder("Assets/Materials", "AshenCourtyard");

            Material charcoal = GetStoneMaterial("CharcoalStone",
                new Color(0.18f, 0.205f, 0.25f),
                new Color(0.34f, 0.365f, 0.42f),
                new Color(0.025f, 0.03f, 0.042f),
                new Color(1f, 0.17f, 0.015f), 0.2f, 0.035f, 0.045f);
            Material broken = GetStoneMaterial("BrokenStone",
                new Color(0.055f, 0.068f, 0.09f),
                new Color(0.13f, 0.145f, 0.18f),
                new Color(0.012f, 0.014f, 0.021f),
                new Color(1f, 0.13f, 0.008f), 0.3f, 0.042f, 0.11f);

            return new Dictionary<string, Material>
            {
                ["Arena_CharcoalStone"] = charcoal,
                ["Arena_BrokenStone"] = broken,
                ["Arena_BlackIron"] = GetLitMaterial("BlackIron", new Color(0.025f, 0.031f, 0.04f), 0.88f, 0.34f),
                ["Arena_WornTrim"] = GetLitMaterial("WornTrim", new Color(0.17f, 0.085f, 0.035f), 0.72f, 0.42f),
                ["Arena_DriedBlood"] = GetLitMaterial("DriedBlood", new Color(0.16f, 0.012f, 0.008f), 0.05f, 0.62f),
                ["Arena_Abyss"] = GetLitMaterial("Abyss", new Color(0.001f, 0.002f, 0.005f), 0f, 0.95f),
                ["Arena_Lava"] = GetLavaMaterial("Lava", new Color(0.22f, 0.008f, 0f), new Color(1f, 0.29f, 0.018f), 3.1f, 0.32f, 0.38f),
                ["Arena_DeepLava"] = GetLavaMaterial("DeepLava", new Color(0.11f, 0.003f, 0f), new Color(1f, 0.16f, 0.006f), 2.8f, 0.18f, 0.22f),
                ["Arena_Ember"] = GetLavaMaterial("Ember", new Color(0.2f, 0.004f, 0f), new Color(1f, 0.31f, 0.02f), 4.5f, 0.5f, 0.55f),
            };
        }

        private static Material GetStoneMaterial(string filename, Color baseColor, Color variation,
            Color crackColor, Color crackGlowColor, float scale, float crackWidth, float crackGlow)
        {
            string path = $"{MaterialFolder}/{filename}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Survivor/CrackedArenaStone");
            if (shader == null)
            {
                throw new InvalidOperationException("The Survivor/CrackedArenaStone shader did not compile.");
            }

            if (material == null)
            {
                material = new Material(shader) { name = filename };
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }

            material.SetColor("_BaseColor", baseColor);
            material.SetColor("_VariationColor", variation);
            material.SetColor("_CrackColor", crackColor);
            material.SetColor("_CrackGlowColor", crackGlowColor);
            material.SetFloat("_Scale", scale);
            material.SetFloat("_CrackWidth", crackWidth);
            material.SetFloat("_CrackGlow", crackGlow);
            material.SetFloat("_Roughness", 0.84f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material GetLitMaterial(string filename, Color color, float metallic, float smoothness)
        {
            string path = $"{MaterialFolder}/{filename}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (material == null)
            {
                material = new Material(shader) { name = filename };
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }

            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material GetLavaMaterial(string filename, Color dark, Color hot,
            float emission, float scale, float speed)
        {
            string path = $"{MaterialFolder}/{filename}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Survivor/AnimatedLava");
            if (shader == null)
            {
                throw new InvalidOperationException("The Survivor/AnimatedLava shader did not compile.");
            }

            if (material == null)
            {
                material = new Material(shader) { name = filename };
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }

            material.SetColor("_DarkColor", dark);
            material.SetColor("_HotColor", hot);
            material.SetFloat("_EmissionStrength", emission);
            material.SetFloat("_Scale", scale);
            material.SetFloat("_Speed", speed);
            material.SetFloat("_Contrast", 3.2f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void AssignMaterials(GameObject root, IReadOnlyDictionary<string, Material> replacements)
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Material[] assigned = renderer.sharedMaterials;
                bool changed = false;
                for (int index = 0; index < assigned.Length; index++)
                {
                    Material replacement = null;
                    if (assigned[index] != null)
                    {
                        replacements.TryGetValue(assigned[index].name, out replacement);
                        if (replacement == null)
                        {
                            foreach (Material candidate in replacements.Values)
                            {
                                if (candidate.name == assigned[index].name)
                                {
                                    replacement = candidate;
                                    break;
                                }
                            }
                        }
                    }

                    if (replacement != null)
                    {
                        assigned[index] = replacement;
                        changed = true;
                    }
                }

                if (changed)
                {
                    renderer.sharedMaterials = assigned;
                }
            }
        }

        private static void ConfigureGeometry(GameObject root)
        {
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                string objectName = filter.gameObject.name;
                if (objectName.StartsWith("HazardCollider_Lava_", StringComparison.Ordinal))
                {
                    Renderer renderer = filter.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        renderer.enabled = false;
                    }

                    BoxCollider trigger = filter.gameObject.AddComponent<BoxCollider>();
                    trigger.center = filter.sharedMesh.bounds.center;
                    trigger.size = filter.sharedMesh.bounds.size;
                    trigger.isTrigger = true;
                    filter.gameObject.AddComponent<VoidKillZone>();
                    continue;
                }

                if (!NeedsSolidCollider(objectName))
                {
                    continue;
                }

                MeshCollider collider = filter.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = filter.sharedMesh;
                collider.convex = false;
                filter.gameObject.isStatic = true;
            }
        }

        private static bool NeedsSolidCollider(string objectName)
        {
            bool mainSlab = objectName.StartsWith("FloorSlab_", StringComparison.Ordinal)
                            && !objectName.Contains("_Border_", StringComparison.Ordinal);
            bool dais = objectName.StartsWith("CentralDais_", StringComparison.Ordinal)
                        || objectName.StartsWith("CentralAltar_", StringComparison.Ordinal);
            bool stairs = objectName.Contains("_Step_", StringComparison.Ordinal);
            bool wall = objectName.StartsWith("PerimeterWall_", StringComparison.Ordinal);
            bool pillar = objectName.StartsWith("PerimeterPillar_", StringComparison.Ordinal)
                          && !objectName.EndsWith("_EmberInset", StringComparison.Ordinal);
            bool brazier = objectName.Contains("_Base", StringComparison.Ordinal)
                           && objectName.StartsWith("Brazier_", StringComparison.Ordinal);
            return mainSlab || dais || stairs || wall || pillar || brazier;
        }

        private static void CreateAbyssKillZone(Transform parent)
        {
            GameObject killZone = new GameObject("AbyssKillZone");
            killZone.transform.SetParent(parent, false);
            killZone.transform.localPosition = new Vector3(0f, -4.5f, 0f);
            BoxCollider collider = killZone.AddComponent<BoxCollider>();
            collider.size = new Vector3(82f, 1.5f, 82f);
            collider.isTrigger = true;
            killZone.AddComponent<VoidKillZone>();
        }

        private static void CreateConceptLighting(Transform arenaRoot)
        {
            Transform previous = arenaRoot.Find("ConceptLighting");
            if (previous != null)
            {
                UnityEngine.Object.DestroyImmediate(previous.gameObject);
            }

            GameObject lightingRoot = new GameObject("ConceptLighting");
            lightingRoot.transform.SetParent(arenaRoot, false);

            foreach (Transform child in arenaRoot.GetComponentsInChildren<Transform>(true))
            {
                if (!child.name.StartsWith("Brazier_", StringComparison.Ordinal)
                    || !child.name.EndsWith("_Flame", StringComparison.Ordinal))
                {
                    continue;
                }

                CreatePointLight(lightingRoot.transform, $"{child.name}_Light",
                    child.position + Vector3.up * 0.55f,
                    new Color(1f, 0.16f, 0.025f), 7.5f, 9f, true);
            }

            Vector3[] lavaLightPositions =
            {
                new Vector3(-14f, 1.1f, 23f), new Vector3(0f, 1.1f, 23f), new Vector3(14f, 1.1f, 23f),
                new Vector3(23f, 1.1f, -14f), new Vector3(23f, 1.1f, 0f), new Vector3(23f, 1.1f, 14f),
            };
            for (int index = 0; index < lavaLightPositions.Length; index++)
            {
                CreatePointLight(lightingRoot.transform, $"LavaBounce_{index}",
                    lavaLightPositions[index], new Color(1f, 0.14f, 0.012f), 3.6f, 12f, false);
            }

            CreatePointLight(lightingRoot.transform, "CoolCombatFill", new Vector3(0f, 7f, 0f),
                new Color(0.38f, 0.5f, 0.78f), 4.2f, 30f, false);
        }

        private static void CreatePointLight(Transform parent, string name, Vector3 position,
            Color color, float intensity, float range, bool shadows)
        {
            GameObject lightObject = new GameObject(name);
            lightObject.transform.SetParent(parent, true);
            lightObject.transform.position = position;
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = shadows ? LightShadows.Soft : LightShadows.None;
        }

        private static void ApplySceneLighting(Scene scene)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.28f, 0.33f, 0.44f);
            RenderSettings.ambientEquatorColor = new Color(0.15f, 0.17f, 0.22f);
            RenderSettings.ambientGroundColor = new Color(0.09f, 0.035f, 0.02f);
            RenderSettings.ambientIntensity = 1.35f;

            foreach (GameObject sceneRoot in scene.GetRootGameObjects())
            {
                foreach (Light light in sceneRoot.GetComponentsInChildren<Light>(true))
                {
                    if (light.name == "Directional Light")
                    {
                        light.color = new Color(0.69f, 0.76f, 1f);
                        light.intensity = 1.85f;
                        light.shadowStrength = 0.68f;
                        return;
                    }
                }
            }
        }

        private static void ApplyVolcanicAtmosphere(Scene scene)
        {
            const string skyboxPath = MaterialFolder + "/VolcanicSkybox.mat";
            Shader skyShader = Shader.Find("Survivor/VolcanicSkybox");
            if (skyShader == null)
            {
                throw new InvalidOperationException("The Survivor/VolcanicSkybox shader did not compile.");
            }

            Material skybox = AssetDatabase.LoadAssetAtPath<Material>(skyboxPath);
            if (skybox == null)
            {
                skybox = new Material(skyShader) { name = "VolcanicSkybox" };
                AssetDatabase.CreateAsset(skybox, skyboxPath);
            }
            else
            {
                skybox.shader = skyShader;
            }

            // These colors are sampled to reproduce the selected Ashen Courtyard concept:
            // near-black smoke overhead, rust-orange horizon haze, and hot lava below.
            skybox.SetColor("_TopColor", new Color(0.028f, 0.009f, 0.008f));
            skybox.SetColor("_HorizonColor", new Color(0.46f, 0.115f, 0.038f));
            skybox.SetColor("_BottomColor", new Color(0.95f, 0.18f, 0.018f));
            skybox.SetColor("_HazeColor", new Color(0.72f, 0.19f, 0.065f));
            skybox.SetFloat("_HazeStrength", 0.42f);
            EditorUtility.SetDirty(skybox);

            RenderSettings.skybox = skybox;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.31f, 0.075f, 0.035f);
            RenderSettings.fogDensity = 0.0035f;
            DynamicGI.UpdateEnvironment();

            foreach (GameObject sceneRoot in scene.GetRootGameObjects())
            {
                foreach (Camera camera in sceneRoot.GetComponentsInChildren<Camera>(true))
                {
                    camera.clearFlags = CameraClearFlags.Skybox;
                    camera.backgroundColor = new Color(0.24f, 0.055f, 0.025f, 1f);
                }
            }
        }

        private static void ApplyPostProcessing(Scene scene)
        {
            foreach (GameObject sceneRoot in scene.GetRootGameObjects())
            {
                Volume volume = sceneRoot.GetComponentInChildren<Volume>(true);
                if (volume == null || volume.sharedProfile == null)
                {
                    continue;
                }

                VolumeProfile profile = volume.sharedProfile;
                if (!profile.TryGet(out ColorAdjustments color))
                {
                    color = profile.Add<ColorAdjustments>(true);
                }

                color.active = true;
                color.postExposure.Override(0.55f);
                color.contrast.Override(11f);
                color.saturation.Override(-6f);
                color.colorFilter.Override(new Color(1f, 0.96f, 0.91f));

                if (!profile.TryGet(out Bloom bloom))
                {
                    bloom = profile.Add<Bloom>(true);
                }

                bloom.active = true;
                bloom.intensity.Override(0.55f);
                bloom.threshold.Override(1.05f);

                if (!profile.TryGet(out Vignette vignette))
                {
                    vignette = profile.Add<Vignette>(true);
                }

                vignette.active = true;
                vignette.intensity.Override(0.16f);
                vignette.smoothness.Override(0.32f);
                EditorUtility.SetDirty(profile);
                return;
            }
        }

        private static void PositionPlayerOnCentralDais(Scene scene)
        {
            foreach (GameObject sceneRoot in scene.GetRootGameObjects())
            {
                foreach (Transform transform in sceneRoot.GetComponentsInChildren<Transform>(true))
                {
                    if (transform.name != "Player3d")
                    {
                        continue;
                    }

                    transform.position = new Vector3(0f, 2.15f, 0f);
                    return;
                }
            }
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }
    }
}
