using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Survivor.EditorTools
{
    /// <summary>Builds an optimized, noncombatant NPC prefab without saving a scene.</summary>
    [InitializeOnLoad]
    public static class VillageMotherSetup
    {
        private const string Model = "Assets/Models/Characters/Mother/VillageMother.fbx";
        private const string Motions = "Assets/Models/Characters/Mother/VillageMother_Motions.fbx";
        private const string Palette = "Assets/Textures/Characters/Mother/MotherPalette.png";
        private const string Folder = "Assets/Prefab/Characters/Mother";
        private const string Pending = "Assets/Editor/VillageMotherSetup.pending";
        public const string PrefabPath = Folder + "/VillageMother.prefab";

        static VillageMotherSetup() { EditorApplication.update += Tick; }

        private static void Tick()
        {
            if (!File.Exists(Pending) || EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode) return;
            try { Install(); }
            catch (Exception e) { Debug.LogException(e); }
            finally { AssetDatabase.DeleteAsset(Pending); }
        }

        [MenuItem("Survivor/Characters/Build Mother Prefab")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before installing the mother prefab.");
            EnsureFolder(Folder);
            EnsureFolder("Assets/Materials/Characters/Mother");
            AssetDatabase.ImportAsset(Palette, ImportAssetOptions.ForceSynchronousImport);
            var textureImporter = (TextureImporter)AssetImporter.GetAtPath(Palette);
            textureImporter.textureType = TextureImporterType.Default;
            textureImporter.sRGBTexture = true;
            textureImporter.mipmapEnabled = false;
            textureImporter.filterMode = FilterMode.Point;
            textureImporter.wrapMode = TextureWrapMode.Clamp;
            textureImporter.npotScale = TextureImporterNPOTScale.None;
            textureImporter.textureCompression = TextureImporterCompression.Uncompressed;
            textureImporter.alphaSource = TextureImporterAlphaSource.None;
            textureImporter.isReadable = false;
            textureImporter.SaveAndReimport();
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Palette);
            if (texture.width != 8 || texture.height != 1)
                throw new InvalidOperationException("Mother palette should contain exactly eight pixels.");

            string matPath = "Assets/Materials/Characters/Mother/MotherPalette.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "MotherPalette" };
                AssetDatabase.CreateAsset(material, matPath);
            }
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", .12f);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Cull", 2f);
            EditorUtility.SetDirty(material);

            Avatar avatar = ConfigureHumanoid(Model, false);
            ConfigureHumanoid(Motions, true);
            var motionImporter = (ModelImporter)AssetImporter.GetAtPath(Motions);
            var clips = motionImporter.defaultClipAnimations;
            if (clips.Length != 2) throw new InvalidOperationException("Expected mother idle and walk takes.");
            foreach (var clip in clips)
            {
                clip.name = clip.takeName.Contains("Walk") ? "Mother_Walk" : "Mother_Idle";
                clip.loopTime = true;
                clip.loopPose = false;
                clip.lockRootRotation = true;
                clip.lockRootHeightY = true;
                clip.lockRootPositionXZ = true;
                clip.keepOriginalOrientation = true;
                clip.keepOriginalPositionXZ = true;
                clip.keepOriginalPositionY = true;
            }
            motionImporter.clipAnimations = clips;
            motionImporter.SaveAndReimport();
            var animations = AssetDatabase.LoadAllAssetsAtPath(Motions).OfType<AnimationClip>().ToArray();
            var idle = animations.Single(c => c.name == "Mother_Idle");
            var walk = animations.Single(c => c.name == "Mother_Walk");
            string controllerPath = Folder + "/VillageMother.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            foreach (var layer in controller.layers)
                foreach (var state in layer.stateMachine.states) layer.stateMachine.RemoveState(state.state);
            foreach (var parameter in controller.parameters) controller.RemoveParameter(parameter);
            foreach (var old in AssetDatabase.LoadAllAssetsAtPath(controllerPath).OfType<BlendTree>())
                UnityEngine.Object.DestroyImmediate(old, true);
            controller.AddParameter("MoveSpeed", AnimatorControllerParameterType.Float);
            var tree = new BlendTree { name = "Quiet idle and gentle walk", blendType = BlendTreeType.Simple1D,
                blendParameter = "MoveSpeed", useAutomaticThresholds = false };
            tree.AddChild(idle, 0f);
            tree.AddChild(walk, .53f);
            AssetDatabase.AddObjectToAsset(tree, controller);
            var stateMachine = controller.layers[0].stateMachine;
            var locomotion = stateMachine.AddState("Locomotion");
            locomotion.motion = tree;
            stateMachine.defaultState = locomotion;
            EditorUtility.SetDirty(controller);

            var root = new GameObject("VillageMother");
            try
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(Model);
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(source);
                visual.name = "MotherVisual";
                visual.transform.SetParent(root.transform, false);
                PrefabUtility.UnpackPrefabInstance(visual, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                var transforms = visual.GetComponentsInChildren<Transform>(true);
                var foot = transforms.Single(t => t.name == "mixamorig:LeftFoot");
                var toe = transforms.Single(t => t.name == "mixamorig:LeftToeBase");
                Vector3 forward = Vector3.ProjectOnPlane(toe.position-foot.position,Vector3.up).normalized;
                visual.transform.localRotation = Quaternion.FromToRotation(forward,Vector3.forward);
                var skin = visual.GetComponentInChildren<SkinnedMeshRenderer>();
                if (skin == null || visual.GetComponentsInChildren<Renderer>().Length != 1 || skin.sharedMesh.subMeshCount != 1)
                    throw new InvalidOperationException("Mother must have one skinned renderer and one submesh.");
                skin.sharedMaterials = new[] { material };
                skin.quality = SkinQuality.Bone2;
                skin.updateWhenOffscreen = false;
                Bounds bounds = WorldBounds(skin);
                visual.transform.localScale *= 1.70f / bounds.size.y;
                bounds = WorldBounds(skin);
                visual.transform.localPosition -= Vector3.up*bounds.min.y;
                var animator = visual.GetComponent<Animator>();
                if (animator == null) animator = visual.AddComponent<Animator>();
                animator.avatar = avatar;
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                var capsule = root.AddComponent<CapsuleCollider>();
                capsule.height = 1.70f;
                capsule.radius = .23f;
                capsule.center = Vector3.up*.85f;
                bounds = WorldBounds(skin);
                if (Mathf.Abs(bounds.size.y-1.70f) > .02f || Mathf.Abs(bounds.min.y) > .015f)
                    throw new InvalidOperationException("Mother height or foot placement is incorrect: " + bounds);
                long triangles = (long)skin.sharedMesh.GetIndexCount(0)/3;
                if (skin.sharedMesh.vertexCount > 5000 || triangles > 6000)
                    throw new InvalidOperationException("Mother exceeded the imported geometry budget.");
                var external = AssetDatabase.LoadAllAssetsAtPath("Assets/Great Sword Pack/great sword run (2).fbx")
                    .OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview") && c.isHumanMotion);
                if (external == null) throw new InvalidOperationException("Missing configured Mixamo compatibility clip.");
                ValidateMotion(visual, new[] { idle, walk, external });
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                RenderPreview(visual, idle, walk);
                AssetDatabase.SaveAssets();
                var saved = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                var savedSkin = saved.GetComponentInChildren<SkinnedMeshRenderer>();
                if (savedSkin.sharedMaterials.Length != 1 || saved.GetComponentInChildren<Animator>().avatar != avatar)
                    throw new InvalidOperationException("Saved mother prefab failed its round-trip check.");
                Debug.Log($"MOTHER PREFAB PASS: {PrefabPath}; 1.70m, {skin.sharedMesh.vertexCount} imported vertices, {triangles} triangles, one renderer/material, 8x1 palette, Bone2 skin, no Read/Write CPU copy, offscreen culling, valid 52-bone Humanoid Avatar and idle/walk. Noncombatant; no scene changes saved.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static Avatar ConfigureHumanoid(string path, bool animation)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (model == null) throw new InvalidOperationException("Build the mother in Blender first: " + path);
            var reference = AssetDatabase.LoadAssetAtPath<GameObject>(Model);
            var rest = reference.GetComponentsInChildren<Transform>(true).ToDictionary(t => t.name);
            var transforms = model.GetComponentsInChildren<Transform>(true);
            var map = new Dictionary<string,string> { ["Hips"]="Hips", ["Spine"]="Spine", ["Chest"]="Spine1",
                ["UpperChest"]="Spine2", ["Neck"]="Neck", ["Head"]="Head" };
            foreach (string side in new[] { "Left", "Right" })
            {
                foreach (var pair in new[] { ("Shoulder","Shoulder"), ("UpperArm","Arm"), ("LowerArm","ForeArm"),
                    ("Hand","Hand"), ("UpperLeg","UpLeg"), ("LowerLeg","Leg"), ("Foot","Foot"), ("Toes","ToeBase") })
                    map[side+pair.Item1] = side+pair.Item2;
                foreach (string finger in new[] { "Thumb", "Index", "Middle", "Ring", "Little" })
                {
                    string source = finger == "Little" ? "Pinky" : finger;
                    map[side+finger+"Proximal"] = side+"Hand"+source+"1";
                    map[side+finger+"Intermediate"] = side+"Hand"+source+"2";
                    map[side+finger+"Distal"] = side+"Hand"+source+"3";
                }
            }
            foreach (string bone in map.Values)
                if (!transforms.Any(t => t.name == "mixamorig:"+bone)) throw new InvalidOperationException("Missing bone: " + bone);
            var description = new HumanDescription {
                human = map.Select(p => new HumanBone { humanName=HumanTrait.BoneName[(int)Enum.Parse(typeof(HumanBodyBones),p.Key)],
                    boneName="mixamorig:"+p.Value, limit=new HumanLimit { useDefaultValues=true } }).ToArray(),
                skeleton = transforms.Select(t => {
                    Transform bind = animation ? (t==model.transform ? reference.transform : rest.TryGetValue(t.name,out var r) ? r : t) : t;
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
            importer.importBlendShapes = false;
            importer.optimizeGameObjects = false;
            importer.isReadable = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.useFileScale = true;
            importer.importNormals = ModelImporterNormals.Import;
            importer.skinWeights = ModelImporterSkinWeights.Custom;
            importer.maxBonesPerVertex = 2;
            importer.minBoneWeight = .0001f;
            importer.optimizeMeshPolygons = true;
            importer.optimizeMeshVertices = true;
            if (!animation) importer.clipAnimations = Array.Empty<ModelImporterClipAnimation>();
            importer.SaveAndReimport();
            var avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
            if (avatar == null || !avatar.isValid || !avatar.isHuman)
                throw new InvalidOperationException("Invalid mother Humanoid Avatar: " + path);
            Debug.Log("MOTHER HUMANOID PASS: " + path + "; valid T-pose body, toes and fingers.");
            return avatar;
        }

        private static void ValidateMotion(GameObject visual, AnimationClip[] clips)
        {
            var animator = visual.GetComponent<Animator>();
            var skin = visual.GetComponentInChildren<SkinnedMeshRenderer>();
            var transforms = visual.GetComponentsInChildren<Transform>(true);
            var positions = transforms.Select(t => t.localPosition).ToArray();
            var rotations = transforms.Select(t => t.localRotation).ToArray();
            var scales = transforms.Select(t => t.localScale).ToArray();
            var controller = animator.runtimeAnimatorController;
            animator.runtimeAnimatorController = null;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var graph = PlayableGraph.Create("Mother Humanoid audit");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var output = AnimationPlayableOutput.Create(graph,"Pose",animator);
            var baked = new Mesh();
            Bounds localBounds = skin.localBounds;
            try
            {
                foreach (var clip in clips)
                {
                    if (!clip.isHumanMotion) throw new InvalidOperationException("Mother clip is not Humanoid: " + clip.name);
                    var player = AnimationClipPlayable.Create(graph,clip);
                    player.SetSpeed(0);
                    player.SetApplyFootIK(false);
                    output.SetSourcePlayable(player);
                    graph.Play();
                    Vector3 firstFoot = default;
                    float travel = 0f;
                    for (int sample = 0; sample <= 24; sample++)
                    {
                        player.SetTime(clip.length*sample/24f);
                        graph.Evaluate(0);
                        Vector3 foot = animator.GetBoneTransform(HumanBodyBones.LeftFoot).position;
                        if (sample == 0) firstFoot = foot;
                        travel = Mathf.Max(travel,Vector3.Distance(firstFoot,foot));
                        skin.BakeMesh(baked,true);
                        Bounds bounds = BakedBounds(skin,baked);
                        if (bounds.size.y < 1.2f || bounds.size.y > 2.1f || bounds.size.x > 1.8f ||
                            bounds.min.y < -.20f || bounds.max.y > 2.05f)
                            throw new InvalidOperationException($"Mother {clip.name} pose {sample} has invalid bounds: {bounds}");
                        if (clip != clips[2]) localBounds.Encapsulate(baked.bounds);
                    }
                    if (clip != clips[0] && travel < .08f)
                        throw new InvalidOperationException("No retargeted foot motion in " + clip.name);
                    Debug.Log($"MOTHER RETARGET PASS: {clip.name}, 25 finite skin poses; foot travel {travel:F3}m.");
                    graph.DestroyPlayable(player);
                }
                // Keep a conservative animation bound without updating offscreen every frame.
                localBounds.Expand(.001f);
                skin.localBounds = localBounds;
            }
            finally
            {
                graph.Destroy();
                animator.runtimeAnimatorController = controller;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                for (int i = 0; i < transforms.Length; i++)
                {
                    transforms[i].localPosition = positions[i];
                    transforms[i].localRotation = rotations[i];
                    transforms[i].localScale = scales[i];
                }
                UnityEngine.Object.DestroyImmediate(baked);
            }
        }

        private static void RenderPreview(GameObject visual, AnimationClip idle, AnimationClip walk)
        {
            var animator = visual.GetComponent<Animator>();
            var skin = visual.GetComponentInChildren<SkinnedMeshRenderer>();
            var controller = animator.runtimeAnimatorController;
            animator.runtimeAnimatorController = null;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            Transform root = visual.transform.root;
            Vector3 original = root.position;
            root.position = Vector3.right*12000;
            var baked = new Mesh();
            var posed = new GameObject("MotherAuditMesh");
            posed.transform.SetParent(skin.transform,false);
            posed.AddComponent<MeshFilter>().sharedMesh = baked;
            posed.AddComponent<MeshRenderer>().sharedMaterials = skin.sharedMaterials;
            skin.enabled = false;
            var cameraObject = new GameObject("MotherAuditCamera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 1.02f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.22f,.22f,.24f);
            var lightObject = new GameObject("MotherAuditLight");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.6f;
            lightObject.transform.rotation = Quaternion.Euler(35,-25,0);
            var target = new RenderTexture(420,560,24);
            var atlas = new Texture2D(1260,560,TextureFormat.RGB24,false);
            var previous = RenderTexture.active;
            var graph = PlayableGraph.Create("Mother render proof");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var output = AnimationPlayableOutput.Create(graph,"Pose",animator);
            try
            {
                camera.targetTexture = target;
                for (int view = 0; view < 3; view++)
                {
                    AnimationClip clip = view == 2 ? walk : idle;
                    var player = AnimationClipPlayable.Create(graph,clip);
                    player.SetSpeed(0);
                    player.SetApplyFootIK(false);
                    output.SetSourcePlayable(player);
                    graph.Play();
                    player.SetTime(clip.length*(view==2 ? .3f : 0f));
                    graph.Evaluate(0);
                    skin.BakeMesh(baked,true);
                    camera.transform.position = root.position + (view==1 ? new Vector3(-1.4f,1.4f,-3f) : new Vector3(1.4f,1.4f,3f));
                    camera.transform.LookAt(root.position+Vector3.up*.85f);
                    // Illuminate the viewed side, rather than inheriting the level's
                    // dark atmospheric presentation in this model-inspection image.
                    lightObject.transform.rotation = Quaternion.LookRotation(
                        root.position+Vector3.up*.85f-camera.transform.position);
                    camera.Render();
                    camera.Render();
                    RenderTexture.active = target;
                    atlas.ReadPixels(new Rect(0,0,420,560),view*420,0);
                    graph.DestroyPlayable(player);
                }
                atlas.Apply();
                File.WriteAllBytes("ArtSource/Previews/VillageMother_Unity01.png",atlas.EncodeToPNG());
            }
            finally
            {
                graph.Destroy();
                animator.runtimeAnimatorController = controller;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                RenderTexture.active = previous;
                camera.targetTexture = null;
                target.Release();
                root.position = original;
                skin.enabled = true;
                foreach (var obj in new UnityEngine.Object[] {posed,baked,cameraObject,lightObject,target,atlas})
                    UnityEngine.Object.DestroyImmediate(obj);
            }
        }

        private static Bounds WorldBounds(SkinnedMeshRenderer skin)
        {
            var baked = new Mesh();
            try { skin.BakeMesh(baked,true); return BakedBounds(skin,baked); }
            finally { UnityEngine.Object.DestroyImmediate(baked); }
        }

        private static Bounds BakedBounds(SkinnedMeshRenderer skin, Mesh baked)
        {
            Vector3[] vertices = baked.vertices;
            if (vertices.Length == 0) throw new InvalidOperationException("Mother skin is empty.");
            Bounds bounds = new Bounds(skin.transform.TransformPoint(vertices[0]),Vector3.zero);
            foreach (Vector3 vertex in vertices)
            {
                Vector3 point = skin.transform.TransformPoint(vertex);
                if (!float.IsFinite(point.sqrMagnitude)) throw new InvalidOperationException("Mother skin is non-finite.");
                bounds.Encapsulate(point);
            }
            return bounds;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int split = path.LastIndexOf('/');
            string parent = path.Substring(0,split);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent,path.Substring(split+1));
        }
    }
}
