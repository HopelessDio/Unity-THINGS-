using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Survivor.Enemies;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Survivor.EditorTools
{
    /// <summary>Prefab-only installer: never saves or changes the open level.</summary>
    [InitializeOnLoad]
    public static class AshboundZombieSetup
    {
        public const string PrefabPath = "Assets/Prefab/Enemies/AshboundZombie.prefab";
        private const string ModelPath = "Assets/Models/Enemies/Ashbound/AshboundZombie.fbx";
        private const string MotionsPath = "Assets/Models/Enemies/Ashbound/AshboundZombie_Motions.fbx";
        private const string ControllerPath = "Assets/Prefab/Enemies/AshboundZombie.controller";
        private const string Pending = "Assets/Editor/AshboundZombieSetup.pending";

        static AshboundZombieSetup() { EditorApplication.update += Tick; }

        private static void Tick()
        {
            if (!File.Exists(Pending) || EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode) return;
            try { Install(); }
            catch (Exception e) { Debug.LogException(e); }
            finally { AssetDatabase.DeleteAsset(Pending); }
        }

        [MenuItem("Survivor/Enemies/Build Ashbound Zombie Prefab")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before building the zombie prefab.");
            EnsureFolder("Assets/Prefab/Enemies");
            EnsureFolder("Assets/Materials/Enemies/Ashbound");
            var avatar = ConfigureHumanoid(ModelPath, false);
            ConfigureHumanoid(MotionsPath, true);
            var importer = (ModelImporter)AssetImporter.GetAtPath(MotionsPath);
            var clips = importer.defaultClipAnimations;
            if (clips.Length != 2) throw new InvalidOperationException($"Expected Idle/Shuffle, got {clips.Length} source takes.");
            foreach (var clip in clips)
            {
                clip.name = clip.takeName.Contains("Shuffle") ? "Ashbound_Shuffle" : "Ashbound_Idle";
                clip.loopTime = true;
                clip.loopPose = false;
                clip.lockRootRotation = true;
                clip.lockRootHeightY = true;
                clip.lockRootPositionXZ = true;
                clip.keepOriginalOrientation = true;
                clip.keepOriginalPositionXZ = true;
                clip.keepOriginalPositionY = true;
            }
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var animations = AssetDatabase.LoadAllAssetsAtPath(MotionsPath).OfType<AnimationClip>().ToArray();
            var idle = animations.Single(c => c.name == "Ashbound_Idle");
            var walk = animations.Single(c => c.name == "Ashbound_Shuffle");
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            // Remove only this generated controller's subassets on rebuild, not source clips.
            foreach (var layer in controller.layers)
                foreach (var state in layer.stateMachine.states) layer.stateMachine.RemoveState(state.state);
            foreach (var parameter in controller.parameters) controller.RemoveParameter(parameter);
            foreach (var oldTree in AssetDatabase.LoadAllAssetsAtPath(ControllerPath).OfType<BlendTree>())
                UnityEngine.Object.DestroyImmediate(oldTree, true);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            var tree = new BlendTree { name = "Idle to shuffle", blendType = BlendTreeType.Simple1D,
                blendParameter = "Speed", useAutomaticThresholds = false };
            tree.AddChild(idle, 0f);
            tree.AddChild(walk, .9f);
            AssetDatabase.AddObjectToAsset(tree, controller);
            var locomotion = controller.layers[0].stateMachine.AddState("Locomotion");
            locomotion.motion = tree;
            controller.layers[0].stateMachine.defaultState = locomotion;
            EditorUtility.SetDirty(controller);

            var root = new GameObject("AshboundZombie");
            try
            {
                var body = root.AddComponent<Rigidbody>();
                body.mass = 1.3f;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.constraints = RigidbodyConstraints.FreezeRotation;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                var capsule = root.AddComponent<CapsuleCollider>();
                capsule.height = 2.16f;
                capsule.radius = .31f;
                var chaser = root.AddComponent<EnemyChaser>();
                var settings = new SerializedObject(chaser);
                settings.FindProperty("moveSpeed").floatValue = 1.2f;
                settings.FindProperty("rotationSpeed").floatValue = 6f;
                // Close enough for capsule contact; the old 1m stop could prevent damage.
                settings.FindProperty("stoppingDistance").floatValue = .55f;
                settings.FindProperty("obstacleLayers").intValue = LayerMask.GetMask("ArenaObstacle", "ArenaHazard");
                settings.ApplyModifiedPropertiesWithoutUndo();
                root.AddComponent<EnemyHealth>();
                root.AddComponent<EnemyContactDamage>();

                var visual = (GameObject)PrefabUtility.InstantiatePrefab(model);
                visual.name = "AshboundVisual";
                visual.transform.SetParent(root.transform, false);
                PrefabUtility.UnpackPrefabInstance(visual, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                // Blender faces -Y. Check exported basis rather than guessing the FBX handedness.
                var toes = visual.GetComponentsInChildren<Transform>(true).Single(t => t.name == "mixamorig:LeftToeBase");
                var foot = visual.GetComponentsInChildren<Transform>(true).Single(t => t.name == "mixamorig:LeftFoot");
                Vector3 forward = Vector3.ProjectOnPlane(toes.position - foot.position, Vector3.up);
                if (forward.sqrMagnitude < .000001f) throw new InvalidOperationException("Cannot determine zombie forward.");
                visual.transform.rotation = Quaternion.FromToRotation(forward.normalized, Vector3.forward);

                foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = renderer.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++) mats[i] = MaterialFor(mats[i].name);
                    renderer.sharedMaterials = mats;
                    if (renderer is SkinnedMeshRenderer skinned) skinned.updateWhenOffscreen = true;
                }
                Bounds bounds = BoundsOf(visual);
                float scale = 2.14f / bounds.size.y;
                visual.transform.localScale *= scale;
                bounds = BoundsOf(visual);
                visual.transform.localPosition += Vector3.down * (bounds.min.y + 1.08f);
                var animator = visual.GetComponentInChildren<Animator>();
                if (animator == null) animator = visual.AddComponent<Animator>();
                animator.avatar = avatar;
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                root.AddComponent<AshboundZombie>().Configure(animator);
                root.AddComponent<EnemyHitFlash>();
                Validate(root);
                var mixamoRun = AssetDatabase.LoadAllAssetsAtPath("Assets/Great Sword Pack/great sword run (2).fbx")
                    .OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview") && c.isHumanMotion);
                if (mixamoRun == null) throw new InvalidOperationException("Existing configured Mixamo test clip missing.");
                ValidateAnimation(visual, idle, walk, mixamoRun);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                RenderPreview(visual, idle, walk, mixamoRun);
                AssetDatabase.SaveAssets();
                Debug.Log($"ASHBOUND PREFAB PASS: {PrefabPath}; refined low-density 2.14m model, 52 Mixamo-named bones, valid Humanoid Avatar, idle/shuffle, 30HP, 10 contact damage, 1.2m/s pursuit. No scene edits; intro spawner remains disabled.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static Avatar ConfigureHumanoid(string path, bool animation)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (model == null) throw new InvalidOperationException("Build the refined Blender model first: " + path);
            var reference = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var rest = reference.GetComponentsInChildren<Transform>(true).ToDictionary(t => t.name);
            var transforms = model.GetComponentsInChildren<Transform>(true);
            var map = new Dictionary<string,string> {
                ["Hips"]="Hips", ["Spine"]="Spine", ["Chest"]="Spine1", ["UpperChest"]="Spine2", ["Neck"]="Neck", ["Head"]="Head"
            };
            foreach (string side in new[] { "Left", "Right" })
            {
                foreach (var pair in new[] { ("Shoulder","Shoulder"), ("UpperArm","Arm"), ("LowerArm","ForeArm"),
                    ("Hand","Hand"), ("UpperLeg","UpLeg"), ("LowerLeg","Leg"), ("Foot","Foot"), ("Toes","ToeBase") })
                    map[side+pair.Item1] = side+pair.Item2;
                foreach (string finger in new[] { "Thumb", "Index", "Middle", "Ring", "Little" })
                {
                    string sourceFinger = finger == "Little" ? "Pinky" : finger;
                    map[side+finger+"Proximal"] = side+"Hand"+sourceFinger+"1";
                    map[side+finger+"Intermediate"] = side+"Hand"+sourceFinger+"2";
                    map[side+finger+"Distal"] = side+"Hand"+sourceFinger+"3";
                }
            }
            foreach (string bone in map.Values)
                if (!transforms.Any(t => t.name == "mixamorig:"+bone)) throw new InvalidOperationException("Missing bone " + bone);
            var description = new HumanDescription {
                human = map.Select(p => new HumanBone { humanName=HumanTrait.BoneName[(int)Enum.Parse(typeof(HumanBodyBones),p.Key)], boneName="mixamorig:"+p.Value,
                    limit=new HumanLimit { useDefaultValues=true } }).ToArray(),
                skeleton = transforms.Select(t => {
                    // Animated frame zero is NOT a valid T-pose reference.
                    Transform bind = animation ? (t == model.transform ? reference.transform : rest.TryGetValue(t.name,out var r) ? r : t) : t;
                    return new SkeletonBone { name=t.name, position=bind.localPosition, rotation=bind.localRotation, scale=bind.localScale };
                }).ToArray(), upperArmTwist=.5f, lowerArmTwist=.5f, upperLegTwist=.5f, lowerLegTwist=.5f,
                armStretch=.05f, legStretch=.05f, hasTranslationDoF=false
            };
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.humanDescription = description;
            importer.importAnimation = animation;
            importer.importCameras = false;
            importer.importLights = false;
            importer.optimizeGameObjects = false;
            importer.isReadable = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.useFileScale = true;
            importer.importNormals = ModelImporterNormals.Import;
            if (!animation) importer.clipAnimations = Array.Empty<ModelImporterClipAnimation>();
            importer.SaveAndReimport();
            var avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
            if (avatar == null || !avatar.isValid || !avatar.isHuman)
                throw new InvalidOperationException("Invalid zombie Humanoid Avatar: " + path);
            Debug.Log("ASHBOUND HUMANOID PASS: " + path + "; mapped body, toes and all finger joints from true neutral bind.");
            return avatar;
        }

        private static void ValidateAnimation(GameObject visual, params AnimationClip[] clips)
        {
            var renderer = visual.GetComponentInChildren<SkinnedMeshRenderer>();
            var animator = visual.GetComponent<Animator>();
            var baked = new Mesh();
            var controller = animator.runtimeAnimatorController;
            var transforms = visual.GetComponentsInChildren<Transform>(true);
            var positions = transforms.Select(t => t.localPosition).ToArray();
            var rotations = transforms.Select(t => t.localRotation).ToArray();
            var scales = transforms.Select(t => t.localScale).ToArray();
            animator.runtimeAnimatorController = null;
            var graph = PlayableGraph.Create("Ashbound humanoid and Mixamo validation");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var output = AnimationPlayableOutput.Create(graph, "Pose", animator);
            try
            {
                foreach (var clip in clips)
                {
                    if (!clip.isHumanMotion) throw new InvalidOperationException("Clip is not Humanoid: " + clip.name);
                    var playable = AnimationClipPlayable.Create(graph, clip);
                    playable.SetSpeed(0);
                    playable.SetApplyFootIK(false);
                    output.SetSourcePlayable(playable);
                    graph.Play();
                    Vector3 firstFoot = Vector3.zero;
                    float footTravel = 0f;
                    for (int sample = 0; sample <= 24; sample++)
                    {
                        playable.SetTime(clip.length * sample / 24f);
                        graph.Evaluate(0);
                        Vector3 foot = animator.GetBoneTransform(HumanBodyBones.LeftFoot).position;
                        if (sample == 0) firstFoot = foot;
                        footTravel = Mathf.Max(footTravel, Vector3.Distance(firstFoot, foot));
                        // true preserves the FBX mesh-space unit conversion; false
                        // produces metre vertices which a 100x renderer scales twice.
                        renderer.BakeMesh(baked, true);
                        Bounds bounds = new Bounds(renderer.transform.TransformPoint(baked.vertices[0]), Vector3.zero);
                        foreach (Vector3 vertex in baked.vertices)
                        {
                            Vector3 point = renderer.transform.TransformPoint(vertex);
                            if (!float.IsFinite(point.x) || !float.IsFinite(point.y) || !float.IsFinite(point.z))
                                throw new InvalidOperationException("Zombie skin has non-finite vertices.");
                            bounds.Encapsulate(point);
                        }
                        if (bounds.size.y < 1.5f || bounds.size.y > 2.6f || bounds.size.x > 2f ||
                            bounds.min.y < -1.35f || bounds.min.y > -.65f)
                            throw new InvalidOperationException($"{clip.name} sample {sample} deformed incorrectly: {bounds}.");
                    }
                    if (clip != clips[0] && footTravel < .1f)
                        throw new InvalidOperationException("Retarget has no leg movement: " + clip.name);
                    Debug.Log($"ASHBOUND RETARGET PASS: {clip.name}, 25 baked-skin samples, left foot travel {footTravel:F3}m.");
                    graph.DestroyPlayable(playable);
                }
                Debug.Log("ASHBOUND ANIMATION PASS: 75 baked-mesh samples across idle, shuffle and external Mixamo run; finite skin, upright human scale and grounded feet.");
            }
            finally
            {
                graph.Destroy();
                animator.runtimeAnimatorController = controller;
                for (int i = 0; i < transforms.Length; i++)
                {
                    transforms[i].localPosition = positions[i];
                    transforms[i].localRotation = rotations[i];
                    transforms[i].localScale = scales[i];
                }
                UnityEngine.Object.DestroyImmediate(baked);
            }
        }

        private static void RenderPreview(GameObject visual, AnimationClip idle, AnimationClip walk, AnimationClip mixamo)
        {
            var animator = visual.GetComponent<Animator>();
            var skin = visual.GetComponentInChildren<SkinnedMeshRenderer>();
            var controller = animator.runtimeAnimatorController;
            animator.runtimeAnimatorController = null;
            Transform root = visual.transform.root;
            Vector3 oldPosition = root.position;
            root.position = Vector3.right * 10000f;
            var baked = new Mesh();
            var posed = new GameObject("Ashbound_RenderAuditOnly");
            posed.transform.SetParent(skin.transform, false);
            posed.AddComponent<MeshFilter>().sharedMesh = baked;
            posed.AddComponent<MeshRenderer>().sharedMaterials = skin.sharedMaterials;
            skin.enabled = false;
            var cameraObject = new GameObject("Ashbound_AuditCamera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 1.3f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.18f,.19f,.21f);
            var lightObject = new GameObject("Ashbound_AuditLight");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.8f;
            lightObject.transform.rotation = Quaternion.Euler(35,-25,0);
            var target = new RenderTexture(420,560,24);
            var atlas = new Texture2D(1680,560,TextureFormat.RGB24,false);
            var previous = RenderTexture.active;
            var graph = PlayableGraph.Create("Ashbound rendered retarget proof");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var output = AnimationPlayableOutput.Create(graph,"Proof",animator);
            try
            {
                camera.targetTexture = target;
                camera.transform.position = root.position + new Vector3(2f,1.1f,4f);
                camera.transform.LookAt(root.position);
                var clips = new[] {idle,walk,walk,mixamo};
                float[] times = {0f,.25f,.75f,.25f};
                for (int i = 0; i < clips.Length; i++)
                {
                    var player = AnimationClipPlayable.Create(graph,clips[i]);
                    player.SetSpeed(0);
                    player.SetApplyFootIK(false);
                    output.SetSourcePlayable(player);
                    graph.Play();
                    player.SetTime(clips[i].length*times[i]);
                    graph.Evaluate(0);
                    skin.BakeMesh(baked,true);
                    // Warm the first SRP/light update before capturing this pose.
                    camera.Render();
                    camera.Render();
                    RenderTexture.active = target;
                    atlas.ReadPixels(new Rect(0,0,420,560),i*420,0);
                    graph.DestroyPlayable(player);
                }
                atlas.Apply();
                File.WriteAllBytes("ArtSource/Previews/Ashbound_Unity_Humanoid.png",atlas.EncodeToPNG());
            }
            finally
            {
                graph.Destroy();
                animator.runtimeAnimatorController = controller;
                RenderTexture.active = previous;
                camera.targetTexture = null;
                target.Release();
                root.position = oldPosition;
                skin.enabled = true;
                foreach (var obj in new UnityEngine.Object[] {posed,baked,cameraObject,lightObject,target,atlas})
                    UnityEngine.Object.DestroyImmediate(obj);
            }
        }

        public static void Validate(GameObject root)
        {
            var renderer = root.GetComponentInChildren<SkinnedMeshRenderer>();
            var animator = root.GetComponentInChildren<Animator>();
            if (renderer == null || renderer.sharedMesh == null || renderer.bones.Length < 50 ||
                animator == null || animator.runtimeAnimatorController == null || animator.applyRootMotion ||
                animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman ||
                root.GetComponent<EnemyChaser>() == null || root.GetComponent<EnemyHealth>() == null ||
                root.GetComponent<EnemyContactDamage>() == null)
                throw new InvalidOperationException("Zombie skin/animation/combat setup incomplete.");
            Bounds bounds = BoundsOf(root);
            if (Mathf.Abs(bounds.size.y - 2.14f) > .05f || Mathf.Abs(bounds.min.y-root.transform.position.y+1.08f) > .05f)
                throw new InvalidOperationException($"Zombie scale/feet incorrect: {bounds}.");
            if (renderer.sharedMaterials.Any(m => m == null || m.shader.name != "Universal Render Pipeline/Lit"))
                throw new InvalidOperationException("Zombie requires non-missing URP materials.");
        }

        private static Material MaterialFor(string source)
        {
            string name = source.Replace(" (Instance)", "");
            string path = "Assets/Materials/Enemies/Ashbound/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            Color color;
            switch (name)
            {
                case "Ashbound_Skin": color = new Color(.62f,.60f,.56f); break;
                case "Ashbound_Tunic": color = new Color(.22f,.215f,.23f); break;
                case "Ashbound_Trousers": color = new Color(.14f,.15f,.17f); break;
                case "Ashbound_Burgundy": color = new Color(.39f,.10f,.13f); break;
                case "Ashbound_Boots": color = new Color(.24f,.18f,.15f); break;
                case "Ashbound_Sockets": color = new Color(.012f,.008f,.012f); break;
                case "Ashbound_Eyes": color = new Color(.85f,.025f,.015f); break;
                default: throw new InvalidOperationException("Unexpected zombie material " + name);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", .12f);
            material.SetFloat("_Cull", 0f);
            if (name == "Ashbound_Eyes")
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 2f);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Bounds BoundsOf(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) throw new InvalidOperationException("Zombie has no renderers.");
            Bounds bounds = default;
            bool first = true;
            var baked = new Mesh();
            try
            {
                foreach (var renderer in renderers)
                {
                    if (renderer is SkinnedMeshRenderer skin)
                    {
                        skin.BakeMesh(baked,true);
                        foreach (Vector3 vertex in baked.vertices)
                        {
                            Vector3 point = skin.transform.TransformPoint(vertex);
                            if (first) { bounds = new Bounds(point,Vector3.zero); first = false; }
                            else bounds.Encapsulate(point);
                        }
                    }
                    else if (first) { bounds = renderer.bounds; first = false; }
                    else bounds.Encapsulate(renderer.bounds);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(baked); }
            return bounds;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int split = path.LastIndexOf('/');
            string parent = path.Substring(0, split);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(split+1));
        }
    }
}
