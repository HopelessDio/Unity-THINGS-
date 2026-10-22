using System;
using System.IO;
using System.Linq;
using Survivor.CameraSystem;
using Survivor.Enemies;
using Survivor.Player;
using Survivor.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.EventSystems;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Survivor.EditorTools
{
    [InitializeOnLoad]
    public static class HomeInteriorSetup
    {
        public const string ScenePath = "Assets/Scenes/Level01_HomeInterior.unity";
        private const string Pending = "Assets/Editor/HomeInteriorSetup.pending";
        private const string Model = "Assets/Models/Levels/HomeInterior/HomeInterior_CombatOpen.fbx";
        private const string LayoutPath = "Assets/Models/Levels/HomeInterior/HomeInterior_CombatOpen_Layout.json";
        private const string MaterialFolder = "Assets/Materials/HomeInterior";
        private const string RootName = "HomeInterior_Environment";
        [Serializable] public class Surface { public string id,source; public Color color; public float emission; }
        [Serializable] public class Box { public string name,layer; public Vector3 center,size; }
        [Serializable] public class Lamp { public string name; public Vector3 position; public Color color; public float intensity,range; }
        [Serializable] public class Layout
        {
            public Surface[] materials; public Box[] boxes; public Lamp[] lamps;
            public Vector3 player,mother,motherForward,camera,cameraTarget;
            public Vector3[] route; public float verticalSpan,horizontalSpan;
            public int renderBatches,triangles;
        }

        static HomeInteriorSetup() { EditorApplication.update += Tick; }
        private static void Tick()
        {
            if (!File.Exists(Pending) || EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode) return;
            try { Install(); }
            catch (Exception e) { Debug.LogException(e); }
            finally { AssetDatabase.DeleteAsset(Pending); }
        }

        [MenuItem("Survivor/Levels/Build and Open House Interior")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before building the interior.");
            Scene active = SceneManager.GetActiveScene();
            if (active.isDirty) throw new InvalidOperationException("Save your current scene edits before opening the interior.");
            bool fresh = !File.Exists(ScenePath);
            string outsideHash = Hash("Assets/Scenes/Game.unity");
            Scene scene;
            if (fresh)
            {
                // Copy the existing gameplay/UI wiring, not individual objects with
                // cross-scene references. The outdoor scene is never saved over.
                if (active.path != "Assets/Scenes/Game.unity")
                    throw new InvalidOperationException("Open the saved Game scene for the first interior build.");
                if (!EditorSceneManager.SaveScene(active,ScenePath,true))
                    throw new InvalidOperationException("Could not create the separate interior scene.");
                AssetDatabase.ImportAsset(ScenePath,ImportAssetOptions.ForceSynchronousImport);
                scene = EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
                foreach (var item in scene.GetRootGameObjects())
                {
                    bool keep = item.GetComponentInChildren<PlayerMovement>(true) != null ||
                        item.GetComponent<Camera>() != null || item.GetComponentInChildren<Canvas>(true) != null ||
                        item.GetComponentInChildren<EventSystem>(true) != null ||
                        item.GetComponentsInChildren<MonoBehaviour>(true).Any(b => b != null && b.GetType().Namespace == "Survivor.UI");
                    if (!keep) UnityEngine.Object.DestroyImmediate(item);
                }
            }
            else
            {
                EnsureFolder("Assets/Scenes/Backups");
                string backup = "Assets/Scenes/Backups/HomeInterior_BeforeRebuild_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity";
                AssetDatabase.CopyAsset(ScenePath,backup);
                scene = active.path == ScenePath ? active : EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
                var previous = scene.GetRootGameObjects().FirstOrDefault(r => r.name == RootName);
                if (previous != null) UnityEngine.Object.DestroyImmediate(previous);
                var oldMother = scene.GetRootGameObjects().FirstOrDefault(r => r.name == "Mother");
                if (oldMother != null) UnityEngine.Object.DestroyImmediate(oldMother);
            }
            var player = FindInScene<PlayerMovement>(scene);
            var camera = FindInScene<Camera>(scene);
            if (player == null || camera == null) throw new InvalidOperationException("Player/Main Camera missing from copied gameplay scene.");
            var root = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(root,scene);
            AssetDatabase.ImportAsset(Model,ImportAssetOptions.ForceSynchronousImport);
            var importer = (ModelImporter)AssetImporter.GetAtPath(Model);
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.isReadable = false;
            importer.SaveAndReimport();
            var layout = JsonUtility.FromJson<Layout>(File.ReadAllText(LayoutPath));
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Model),scene);
            PrefabUtility.UnpackPrefabInstance(model,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            model.name = "RoomGeometry";
            model.transform.SetParent(root.transform,false);
            var transforms = model.GetComponentsInChildren<Transform>();
            Vector3 origin = transforms.Single(t => t.name == "H1_Origin").position;
            Vector3 ax = transforms.Single(t => t.name == "H1_BasisX").position-origin;
            Vector3 ay = transforms.Single(t => t.name == "H1_BasisY").position-origin;
            Vector3 az = transforms.Single(t => t.name == "H1_BasisZ").position-origin;
            if (Mathf.Abs(ax.magnitude-1)>.01f || Mathf.Abs(ay.magnitude-1)>.01f || Mathf.Abs(az.magnitude-1)>.01f)
                throw new InvalidOperationException("Interior FBX metre conversion is incorrect.");
            Vector3 Convert(Vector3 p) => origin+ax*p.x+ay*p.y+az*p.z;
            Vector3 Direction(Vector3 p) => ax*p.x+ay*p.y+az*p.z;
            EnsureFolder(MaterialFolder);
            var surfaces = layout.materials.ToDictionary(s => s.id);
            int batches = 0;
            foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>())
            {
                if (!surfaces.TryGetValue(renderer.name,out var surface))
                    throw new InvalidOperationException("Unknown room batch " + renderer.name);
                string path = MaterialFolder+"/"+surface.id+".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = surface.source };
                    AssetDatabase.CreateAsset(material,path);
                }
                material.SetColor("_BaseColor",surface.color.gamma);
                material.SetFloat("_Smoothness",.10f);
                if (surface.emission > 0)
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor",surface.color*surface.emission);
                }
                else { material.DisableKeyword("_EMISSION"); material.SetColor("_EmissionColor",Color.black); }
                renderer.sharedMaterial = material;
                EditorUtility.SetDirty(material);
                batches++;
            }
            var collisionRoot = new GameObject("RoomCollisions").transform;
            collisionRoot.SetParent(root.transform,false);
            foreach (var box in layout.boxes)
            {
                var item = new GameObject(box.name);
                item.transform.SetParent(collisionRoot,false);
                item.transform.position = Convert(box.center);
                item.layer = LayerMask.NameToLayer(box.layer);
                var collider = item.AddComponent<BoxCollider>();
                collider.size = Abs(ax*box.size.x)+Abs(ay*box.size.y)+Abs(az*box.size.z);
            }
            foreach (var follow in camera.GetComponents<CameraFollow>()) UnityEngine.Object.DestroyImmediate(follow);
            foreach (var follow in camera.GetComponents<LevelOneCamera>()) UnityEngine.Object.DestroyImmediate(follow);
            var picture = camera.transform.Find("Level01_PictureBackdrop");
            if (picture != null) UnityEngine.Object.DestroyImmediate(picture.gameObject);
            camera.transform.SetPositionAndRotation(Convert(layout.camera),Quaternion.LookRotation(Direction(layout.cameraTarget-layout.camera),Direction(Vector3.up)));
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.025f,.012f,.021f);
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 150f;
            camera.allowHDR = true;
            var additional = camera.GetUniversalAdditionalCameraData();
            additional.renderPostProcessing = true;
            additional.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            additional.antialiasingQuality = AntialiasingQuality.High;
            additional.renderShadows = true;
            var roomView = camera.GetComponent<RoomCamera>();
            if (roomView == null) roomView = camera.gameObject.AddComponent<RoomCamera>();
            roomView.Configure(layout.horizontalSpan,layout.verticalSpan);
            var body = player.GetComponent<Rigidbody>();
            body.position = Convert(layout.player);
            body.rotation = Quaternion.LookRotation(Direction(Vector3.forward));
            player.transform.SetPositionAndRotation(body.position,body.rotation);
            player.ConfigureMovementView(camera.transform);
            player.GetComponent<PlayerModelVisual>().RebuildVisual();
            var motherAsset = AssetDatabase.LoadAssetAtPath<GameObject>(VillageMotherSetup.PrefabPath);
            if (motherAsset == null) throw new InvalidOperationException("Build the mother prefab first.");
            var mother = (GameObject)PrefabUtility.InstantiatePrefab(motherAsset,scene);
            mother.name = "Mother";
            mother.transform.SetPositionAndRotation(Convert(layout.mother),Quaternion.LookRotation(Direction(layout.motherForward)));
            PoseMother(mother);
            foreach (var lamp in layout.lamps)
            {
                var item = new GameObject(lamp.name);
                item.transform.SetParent(root.transform,false);
                item.transform.position = Convert(lamp.position);
                var light = item.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = lamp.color;
                light.intensity = lamp.intensity;
                light.range = lamp.range;
                light.shadows = LightShadows.None;
            }
            var keyObject = new GameObject("Soft room key");
            keyObject.transform.SetParent(root.transform,false);
            keyObject.transform.rotation = Quaternion.LookRotation(Direction(new Vector3(-5,-9,8)));
            var key = keyObject.AddComponent<Light>();
            key.type = LightType.Directional;
            key.color = new Color(1f,.86f,.72f);
            key.intensity = 1.3f;
            key.shadows = LightShadows.Soft;
            key.shadowStrength = .45f;
            RenderSettings.sun = key;
            RenderSettings.skybox = null;
            RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.34f,.30f,.29f);
            RenderSettings.ambientEquatorColor = new Color(.26f,.22f,.21f);
            RenderSettings.ambientGroundColor = new Color(.10f,.075f,.085f);
            RenderSettings.ambientIntensity = 1.1f;
            CreateLook(root.transform);
            var entry = new GameObject("Home_EntryMarker");
            entry.transform.SetParent(root.transform,false);
            entry.transform.position = Convert(layout.player);
            var approach = new GameObject("Mother_ApproachMarker");
            approach.transform.SetParent(root.transform,false);
            approach.transform.position = Convert(layout.route.Last()+Vector3.up*1.03f);
            Physics.SyncTransforms();
            Validate(scene,root,player,mother,camera,layout,Convert,batches);
            var settings = EditorBuildSettings.scenes.ToList();
            if (!settings.Any(s => s.path == ScenePath))
            {
                settings.Add(new EditorBuildSettingsScene(ScenePath,true));
                EditorBuildSettings.scenes = settings.ToArray();
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            RenderPreview(camera);
            EditorSceneManager.SaveScene(scene);
            if (Hash("Assets/Scenes/Game.unity") != outsideHash)
                throw new InvalidOperationException("Unexpected outdoor scene file change during interior setup.");
            Selection.activeGameObject = mother;
            Debug.Log($"HOME INTERIOR PASS: {ScenePath}; {batches} room batches, {layout.triangles} triangles, {layout.boxes.Length} simple colliders, mother placed by hearth, original player/camera-relative controls/UI/retry retained. Outdoor scene hash unchanged. Door transition not connected yet.");
        }

        private static void PoseMother(GameObject mother)
        {
            var animator = mother.GetComponentInChildren<Animator>();
            var idle = animator.runtimeAnimatorController.animationClips.First(c => c.name == "Mother_Idle");
            var controller = animator.runtimeAnimatorController;
            animator.runtimeAnimatorController = null;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var transforms = mother.GetComponentsInChildren<Transform>(true);
            Vector3[] positions = null,scales = null;
            Quaternion[] rotations = null;
            var graph = PlayableGraph.Create("Mother room idle preview");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            try
            {
                var player = AnimationClipPlayable.Create(graph,idle);
                player.SetSpeed(0);
                var output = AnimationPlayableOutput.Create(graph,"Pose",animator);
                output.SetSourcePlayable(player);
                graph.Play(); graph.Evaluate(0);
                positions = transforms.Select(t => t.localPosition).ToArray();
                rotations = transforms.Select(t => t.localRotation).ToArray();
                scales = transforms.Select(t => t.localScale).ToArray();
            }
            finally
            {
                graph.Destroy();
                animator.runtimeAnimatorController = controller;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                for (int i = 0; i < transforms.Length; i++)
                {
                    if (positions != null)
                    {
                        transforms[i].localPosition = positions[i];
                        transforms[i].localRotation = rotations[i];
                        transforms[i].localScale = scales[i];
                    }
                    PrefabUtility.RecordPrefabInstancePropertyModifications(transforms[i]);
                }
                PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
            }
        }

        private static void CreateLook(Transform root)
        {
            string path = MaterialFolder+"/HomeLook.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null) { profile = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(profile,path); }
            if (!profile.TryGet<Tonemapping>(out var tone)) { tone = profile.Add<Tonemapping>(); AssetDatabase.AddObjectToAsset(tone,profile); }
            tone.mode.Override(TonemappingMode.ACES);
            if (!profile.TryGet<ColorAdjustments>(out var color)) { color = profile.Add<ColorAdjustments>(); AssetDatabase.AddObjectToAsset(color,profile); }
            color.postExposure.Override(.3f); color.contrast.Override(3f); color.saturation.Override(-6f);
            if (!profile.TryGet<Bloom>(out var bloom)) { bloom = profile.Add<Bloom>(); AssetDatabase.AddObjectToAsset(bloom,profile); }
            bloom.intensity.Override(.12f); bloom.threshold.Override(1.2f);
            var item = new GameObject("Home global volume"); item.transform.SetParent(root,false);
            var volume = item.AddComponent<Volume>(); volume.isGlobal = true; volume.sharedProfile = profile;
            EditorUtility.SetDirty(profile);
        }

        private static void Validate(Scene scene,GameObject root,PlayerMovement player,GameObject mother,Camera camera,Layout layout,Func<Vector3,Vector3> convert,int batches)
        {
            if (batches != layout.renderBatches || batches > 16)
                throw new InvalidOperationException("Interior batch count incorrect.");
            int mask = LayerMask.GetMask("ArenaObstacle");
            foreach (var point in layout.route)
            {
                Vector3 floor = convert(point);
                if (!Physics.RaycastAll(floor+Vector3.up*2,Vector3.down,3,1,QueryTriggerInteraction.Ignore)
                    .Any(h => h.collider.name == "Continuous floor"))
                    throw new InvalidOperationException("Room route has no floor: " + point);
                if (Physics.CheckCapsule(floor+Vector3.up*.52f,floor+Vector3.up*1.52f,.49f,mask,QueryTriggerInteraction.Ignore))
                    throw new InvalidOperationException("Room route is blocked for the player: " + point);
            }
            foreach (var character in new[] {player.transform,mother.transform})
            {
                Vector3 point = character == player.transform ? character.position-Vector3.up*1.03f : character.position;
                if (Physics.CheckCapsule(point+Vector3.up*.5f,point+Vector3.up*1.4f,.30f,mask,QueryTriggerInteraction.Ignore))
                    throw new InvalidOperationException("Character intersects furniture: " + character.name);
                Vector3 viewport = camera.WorldToViewportPoint(character.position+Vector3.up*.5f);
                if (viewport.z <= 0 || viewport.x < .03f || viewport.x > .97f || viewport.y < .03f || viewport.y > .97f)
                    throw new InvalidOperationException("Character is outside the room camera: " + character.name);
            }
            if (FindInScene<EnemySpawner>(scene) != null || FindInScene<EnemyChaser>(scene) != null)
                throw new InvalidOperationException("Interior must not contain enemies or spawning.");
            if (FindInScene<GameOverUI>(scene) == null || player.transform.Find("VeilWalkerVisual") == null)
                throw new InvalidOperationException("Player/retry UI missing.");
            Debug.Log("HOME EDIT VALIDATION PASS: 8 full-capsule route samples, furniture clearance, player/mother framing, continuous floor, no enemies and preserved player/retry UI.");
        }

        public static void RenderPreview(Camera camera)
        {
            var old = RenderTexture.active;
            var previousTarget = camera.targetTexture;
            var target = new RenderTexture(1440,900,24);
            var image = new Texture2D(1440,900,TextureFormat.RGB24,false);
            try
            {
                camera.targetTexture = target;
                camera.aspect = 1.6f;
                camera.GetComponent<RoomCamera>().RefreshFraming();
                camera.Render(); camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0,0,1440,900),0,0); image.Apply();
                File.WriteAllBytes("ArtSource/Previews/HomeInterior_Unity01.png",image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                camera.ResetAspect(); camera.GetComponent<RoomCamera>().RefreshFraming();
                RenderTexture.active = old; target.Release();
                UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(image);
            }
        }

        private static T FindInScene<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<T>(true)).FirstOrDefault();
        private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x),Mathf.Abs(v.y),Mathf.Abs(v.z));
        private static string Hash(string path)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
                return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path)));
        }
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int split = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0,split));
            AssetDatabase.CreateFolder(path.Substring(0,split),path.Substring(split+1));
        }
    }
}
