using System;
using System.IO;
using Survivor.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Survivor.EditorTools
{
    public static class VeilWalkerSceneInstaller
    {
        private const string ScenePath = "Assets/Scenes/Game.unity";
        private const string PendingPath = "Assets/Editor/VeilWalkerSceneInstall.pending";
        private const string PrefabsPath = "Assets/Prefab/Player/VeilWalker/";

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
                Install();
                AssetDatabase.DeleteAsset(PendingPath);
                if (File.Exists(PendingPath)) File.Delete(PendingPath);
                AssetDatabase.Refresh();
            }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        [MenuItem("Survivor/Install Veil Walker in Game Scene")]
        public static void Install()
        {
            EnsureAfterimageMaterial();
            EnsureDashAccentMaterials();
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabsPath + "VeilWalker.prefab");
            GameObject sword = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabsPath + "VeilWalker_Sword.prefab");
            if (model == null || sword == null)
            {
                VeilWalkerAssetBuilder.Prepare();
                model = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabsPath + "VeilWalker.prefab");
                sword = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabsPath + "VeilWalker_Sword.prefab");
            }
            if (model == null || sword == null) throw new InvalidOperationException("Veil Walker prefabs are missing.");

            Scene active = SceneManager.GetActiveScene();
            bool alreadyOpen = active.path == ScenePath;
            Scene scene = alreadyOpen ? active : EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            GameObject player = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                    if (child.name == "Player3d") player = child.gameObject;
            }
            if (player == null) throw new InvalidOperationException("Player3d was not found in Game.unity.");
            if (player.GetComponent<Rigidbody>() == null || player.GetComponent<CapsuleCollider>() == null)
                throw new InvalidOperationException("Existing player physics components are missing.");

            PlayerModelVisual visual = player.GetComponent<PlayerModelVisual>();
            if (visual == null) visual = player.AddComponent<PlayerModelVisual>();
            visual.Configure(model, sword);
            foreach (MonoBehaviour behaviour in player.GetComponents<MonoBehaviour>())
            {
                if (behaviour == null) continue;
                string type = behaviour.GetType().FullName;
                if (type == "Survivor.Player.DarkKnightVisual" || type == "Survivor.Combat.PlayerAutoShooter")
                    behaviour.enabled = false;
            }

            Transform instance = player.transform.Find("VeilWalkerVisual");
            if (instance == null) throw new InvalidOperationException("Visual model did not spawn.");
            PlayerWeaponEquipment equipment = instance.GetComponent<PlayerWeaponEquipment>();
            if (equipment == null || equipment.EquippedSword == null || equipment.EquippedGun != null)
                throw new InvalidOperationException("Scene player must equip exactly the sword-only loadout.");
            int count = 0;
            foreach (Transform child in player.transform)
                if (child.name == "VeilWalkerVisual") count++;
            if (count != 1) throw new InvalidOperationException("Duplicate player visuals detected.");
            SkinnedMeshRenderer skin = instance.GetComponentInChildren<SkinnedMeshRenderer>();
            CapsuleCollider capsule = player.GetComponent<CapsuleCollider>();
            float targetHeight = capsule.height * Mathf.Abs(player.transform.lossyScale.y);
            if (skin == null || Mathf.Abs(skin.bounds.size.y - targetHeight) > .1f)
                throw new InvalidOperationException("Player visual height does not match the collision capsule.");
            ValidateWalk(instance, player.transform, equipment);
            ValidateKeyboardFacing();
            ValidateSmoothTurns();
            ValidateKeyboardRun(instance, player.transform);
            ValidateDashAnimation(instance, player.transform, equipment);
            ValidateAfterimages(instance, player.transform);
            ValidateDashAccents(player);
            SwordComboValidation.Validate(player, instance);

            EditorUtility.SetDirty(visual);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"Veil Walker installed in Game.unity: two-handed sword, no gun, visual scale {instance.localScale.x:F3}, foot offset {instance.localPosition.y:F3}, visible height {skin.bounds.size.y:F3}. Movement, aiming, health and physics preserved; old visuals and projectile shooter disabled.");
            if (!alreadyOpen) EditorSceneManager.CloseScene(scene, true);
        }

        private static void ValidateWalk(Transform visual, Transform player, PlayerWeaponEquipment equipment)
        {
            VeilWalkerLocomotion walk = visual.GetComponent<VeilWalkerLocomotion>();
            if (walk == null || !walk.IsReady) throw new InvalidOperationException("Walk rig is not ready.");
            Transform leftHand = null;
            Transform foot = null;
            Transform rightFoot = null;
            Transform leftKnee = null, rightKnee = null;
            Transform chest = null;
            foreach (Transform bone in visual.GetComponentsInChildren<Transform>(true))
            {
                if (bone.name == "LeftHand") leftHand = bone;
                if (bone.name == "LeftFoot") foot = bone;
                if (bone.name == "RightFoot") rightFoot = bone;
                if (bone.name == "LeftLowerLeg") leftKnee = bone;
                if (bone.name == "RightLowerLeg") rightKnee = bone;
                if (bone.name == "Chest") chest = bone;
            }
            if (leftHand == null || foot == null || rightFoot == null) throw new InvalidOperationException("Walk bones missing.");
            float gripDistance = Vector3.Distance(leftHand.position, equipment.SwordOffhandGrip.position);
            Vector3 playerPosition = player.position;
            Quaternion playerRotation = player.rotation;
            Vector3 restFoot = foot.position;
            float greatestStep = 0f;
            if (chest == null) throw new InvalidOperationException("Chest animation bone missing.");
            Quaternion restChest = chest.localRotation;
            float greatestShoulderMotion = 0f;
            try
            {
                for (int heading = 0; heading < 16; heading++)
                {
                    Vector3 direction = Quaternion.AngleAxis(heading * 22.5f, Vector3.up) * player.forward;
                    float directionalStep = 0f;
                    for (int gait = 0; gait < 2; gait++)
                    for (int sample = 0; sample < 64; sample++)
                    {
                        if (gait == 0) walk.SamplePose(sample / 64f, 1f, direction);
                        else walk.SampleRunPose(sample / 64f, 1f, direction);
                        greatestShoulderMotion = Mathf.Max(greatestShoulderMotion,
                            Quaternion.Angle(restChest, chest.localRotation));
                        greatestStep = Mathf.Max(greatestStep, Vector3.Distance(restFoot, foot.position));
                        directionalStep = Mathf.Max(directionalStep, Mathf.Abs(Vector3.Dot(foot.position - restFoot, direction)));
                        if (float.IsNaN(foot.position.x) || float.IsNaN(foot.rotation.x))
                            throw new InvalidOperationException("Invalid walk pose.");
                        if (Mathf.Abs(Vector3.Distance(leftHand.position, equipment.SwordOffhandGrip.position) - gripDistance) > .001f)
                            throw new InvalidOperationException("Walk animation broke the two-handed grip.");
                        if (player.position != playerPosition || player.rotation != playerRotation)
                            throw new InvalidOperationException("Walk animation moved the physics root.");
                    }
                    if (directionalStep < .03f)
                        throw new InvalidOperationException($"Missing directional stride at {heading * 22.5f} degrees.");
                    // Advance the gameplay root between stance poses. The planted
                    // ankle must stay in the same world-space ground location.
                    walk.SampleRunPose(.08f, 1f, direction, true);
                    Vector3 plantedFoot = foot.position;
                    player.position += direction * .03f;
                    try
                    {
                        walk.SampleRunPose(.1f, 1f, direction, true);
                        if (Vector3.Distance(plantedFoot, foot.position) > .015f)
                            throw new InvalidOperationException($"Run foot slid during stance at {heading * 22.5f} degrees.");
                    }
                    finally
                    {
                        player.position = playerPosition;
                        walk.SampleRunPose(.7f, 0f, direction);
                    }
                }
            }
            finally { walk.ResetPose(); }
            if (greatestStep < .05f || Vector3.Distance(foot.position, restFoot) > .001f)
                throw new InvalidOperationException("Walk did not step or restore the idle pose.");
            if (!walk.RunByDefault) throw new InvalidOperationException("Running should be the default gait.");
            if (greatestShoulderMotion < 2f || Quaternion.Angle(restChest, chest.localRotation) > .01f)
                throw new InvalidOperationException("Upper body did not animate or reset to idle.");
            Debug.Log("Approved big-stride run restored: 2048 pose checks + 16 foot-plant tests passed; two-handed sword guard and original world-direction WASD preserved. Later directional animation experiments removed.");
        }


        private static void ValidateKeyboardFacing()
        {
            for (int heading = 0; heading < 16; heading++)
            {
                Vector3 direction = Quaternion.Euler(0f, heading * 22.5f, 0f) * Vector3.forward;
                if (!PlayerAim.TryGetFacingRotation(direction, out Quaternion facing) ||
                    Vector3.Distance(facing * Vector3.forward, direction) > .0001f)
                    throw new InvalidOperationException("Keyboard-facing rotation does not match movement.");
            }
            if (PlayerAim.TryGetFacingRotation(Vector3.zero, out _))
                throw new InvalidOperationException("Idle must keep the last facing direction.");
            Debug.Log("Keyboard facing validated: 16 directions and idle. Mouse no longer rotates Player3d; world-direction WASD and approved run retained.");
        }

        private static void ValidateKeyboardRun(Transform visual, Transform player)
        {
            VeilWalkerLocomotion walk = visual.GetComponent<VeilWalkerLocomotion>();
            Transform left = null, right = null, leftKnee = null, rightKnee = null;
            foreach (Transform bone in visual.GetComponentsInChildren<Transform>(true))
            {
                if (bone.name == "LeftFoot") left = bone;
                if (bone.name == "RightFoot") right = bone;
                if (bone.name == "LeftLowerLeg") leftKnee = bone;
                if (bone.name == "RightLowerLeg") rightKnee = bone;
            }
            Quaternion original = player.rotation;
            walk.ResetPose();
            float ankleOrder = Mathf.Sign(Vector3.Dot(right.position - left.position, player.right));
            float kneeOrder = Mathf.Sign(Vector3.Dot(rightKnee.position - leftKnee.position, player.right));
            float smallestAnkleGap = float.MaxValue;
            float smallestKneeGap = float.MaxValue;
            try
            {
                for (int sample = 0; sample < 32; sample++)
                {
                    float cycle = sample / 32f;
                    walk.SampleRunPose(cycle, 1f, player.forward);
                    Vector3 expectedLeft = player.InverseTransformPoint(left.position);
                    Vector3 expectedRight = player.InverseTransformPoint(right.position);
                    // Jump between headings with foot locking enabled to test
                    // that old world-space anchors are released on each turn.
                    for (int heading = 0; heading < 8; heading++)
                    {
                        player.rotation = original * Quaternion.Euler(0f, heading * 45f, 0f);
                        walk.SampleKeyboardRun(cycle, 1f);
                        if (Vector3.Distance(expectedLeft, player.InverseTransformPoint(left.position)) > .015f ||
                            Vector3.Distance(expectedRight, player.InverseTransformPoint(right.position)) > .015f)
                            throw new InvalidOperationException("Keyboard diagonal must use the same local forward-run pose.");
                        float ankleGap = ankleOrder * Vector3.Dot(right.position - left.position, player.right);
                        float kneeGap = kneeOrder * Vector3.Dot(rightKnee.position - leftKnee.position, player.right);
                        smallestAnkleGap = Mathf.Min(smallestAnkleGap, ankleGap);
                        smallestKneeGap = Mathf.Min(smallestKneeGap, kneeGap);
                        if (ankleGap < .12f || kneeGap < .02f)
                            throw new InvalidOperationException("Widened run stance must keep ankles and knees on their resting sides.");
                    }
                }
            }
            finally
            {
                player.rotation = original;
                walk.SampleKeyboardRun(0f, 0f);
            }
            Debug.Log($"Slightly open run stance validated: 256 turn/pose checks; same forward gait in all directions; minimum ankle gap {smallestAnkleGap:F3}m, knee gap {smallestKneeGap:F3}m; stride, timing and guard preserved.");
        }

        private static void ValidateSmoothTurns()
        {
            foreach (float start in new[] { 0f, 350f, 10f, 180f })
            foreach (float turn in new[] { 45f, -45f, 90f, -90f, 180f })
            foreach (float dt in new[] { .02f, .01f })
            {
                Quaternion current = Quaternion.Euler(0f, start, 0f);
                Quaternion target = Quaternion.Euler(0f, start + turn, 0f);
                float velocity = 0f;
                Quaternion first = PlayerAim.SmoothFacing(current, target, ref velocity, .09f, 1080f, dt);
                if (Quaternion.Angle(current, first) < .01f || Quaternion.Angle(first, target) < 1f)
                    throw new InvalidOperationException("Turn must ease toward its target rather than snap or stay still.");
                current = first;
                for (int frame = 0; frame < Mathf.CeilToInt(.6f / dt); frame++)
                {
                    float before = Quaternion.Angle(current, target);
                    current = PlayerAim.SmoothFacing(current, target, ref velocity, .09f, 1080f, dt);
                    if (Quaternion.Angle(current, target) > before + .01f)
                        throw new InvalidOperationException("Smooth turn overshot or took the long rotation path.");
                }
                if (Quaternion.Angle(current, target) > .2f)
                    throw new InvalidOperationException("Smooth turn failed to settle.");
            }
            Debug.Log("Smooth keyboard turns validated: 40 cases covering diagonals, reversals and 360-degree wrap at two physics rates; no snap or overshoot.");
        }

        private static void ValidateDashAnimation(Transform visual, Transform player, PlayerWeaponEquipment equipment)
        {
            VeilWalkerLocomotion walk = visual.GetComponent<VeilWalkerLocomotion>();
            Transform leftHand = null, leftFoot = null, rightFoot = null, hips = null;
            foreach (Transform bone in visual.GetComponentsInChildren<Transform>(true))
            {
                if (bone.name == "LeftHand") leftHand = bone;
                if (bone.name == "LeftFoot") leftFoot = bone;
                if (bone.name == "RightFoot") rightFoot = bone;
                if (bone.name == "Hips") hips = bone;
            }
            walk.ResetPose();
            float grip = Vector3.Distance(leftHand.position, equipment.SwordOffhandGrip.position);
            float footOrder = Mathf.Sign(Vector3.Dot(rightFoot.position - leftFoot.position, player.right));
            Quaternion originalRotation = player.rotation;
            Vector3 originalPosition = player.position;
            float greatestLean = 0f;
            try
            {
                for (int heading = 0; heading < 8; heading++)
                {
                    player.rotation = originalRotation * Quaternion.Euler(0f, heading * 45f, 0f);
                    foreach (float cycle in new[] { 0f, .5f })
                    {
                        walk.SampleRunPose(cycle, 1f, player.forward);
                        Quaternion runHips = hips.localRotation;
                        Vector3 runFoot = leftFoot.position;
                        for (int progress = 0; progress <= 8; progress++)
                        for (int blend = 0; blend <= 4; blend++)
                        {
                            walk.SampleDashPose(cycle, blend / 4f, progress / 8f);
                            greatestLean = Mathf.Max(greatestLean, Quaternion.Angle(runHips, hips.localRotation));
                            if (Mathf.Abs(Vector3.Distance(leftHand.position, equipment.SwordOffhandGrip.position) - grip) > .002f)
                                throw new InvalidOperationException("Dash blending loosened the supporting sword hand.");
                            if (footOrder * Vector3.Dot(rightFoot.position - leftFoot.position, player.right) < .1f)
                                throw new InvalidOperationException("Dash blend crossed the feet.");
                            if (float.IsNaN(hips.localRotation.x) || player.position != originalPosition)
                                throw new InvalidOperationException("Dash animation must remain finite and visual-only.");
                        }
                        walk.SampleDashPose(cycle, 0f, 1f);
                        if (Vector3.Distance(leftFoot.position, runFoot) > .001f)
                            throw new InvalidOperationException("Dash fade-out must restore the original run pose.");
                    }
                }
            }
            finally
            {
                player.rotation = originalRotation;
                walk.SampleKeyboardRun(0f, 0f);
            }
            if (greatestLean < 10f) throw new InvalidOperationException("Dash must have a distinct braced pose.");
            Debug.Log("Sword dash animation validated: 720 blended poses across 8 headings and both lead legs; grip and foot ordering preserved; run restored; no physics changes.");
        }

        private static void EnsureAfterimageMaterial()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            const string path = "Assets/Resources/DashAfterimage.mat";
            Shader shader = Shader.Find("Survivor/DashAfterimage");
            if (shader == null || ShaderUtil.ShaderHasError(shader))
                throw new InvalidOperationException("Dash afterimage shader is missing or failed compilation.");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = "DashAfterimage" };
                material.SetColor("_Tint", new Color(2.1f, .32f, .4f, 1f));
                material.SetFloat("_Opacity", .48f);
                AssetDatabase.CreateAsset(material, path);
            }
        }

        private static void ValidateAfterimages(Transform visual, Transform player)
        {
            PlayerDashAfterimages effect = visual.GetComponent<PlayerDashAfterimages>();
            effect.Initialize();
            if (!effect.IsReady) throw new InvalidOperationException("Dash afterimage pool did not initialize.");
            VeilWalkerLocomotion walk = visual.GetComponent<VeilWalkerLocomotion>();
            Vector3 original = player.position;
            try
            {
                walk.SampleDashPose(0f, 1f, .5f);
                effect.CaptureSnapshot();
                Bounds snapshot = effect.LatestBodyBounds();
                Bounds rendered = visual.GetComponentInChildren<SkinnedMeshRenderer>().bounds;
                float heightRatio = snapshot.size.y / rendered.size.y;
                if (heightRatio < .7f || heightRatio > 1.3f ||
                    Vector3.Distance(snapshot.center, rendered.center) > .4f)
                    throw new InvalidOperationException($"Afterimage scale/position mismatch: height {snapshot.size.y:F3}m vs body {rendered.size.y:F3}m, ratio {heightRatio:F3}.");
                player.position += Vector3.right * 2f;
                Bounds frozen = effect.LatestBodyBounds();
                if (Vector3.Distance(frozen.center, snapshot.center) > .0001f)
                    throw new InvalidOperationException("Dash afterimage followed the live player instead of staying behind.");
                for (int i = 0; i < 10; i++) effect.CaptureSnapshot();
                if (effect.ActiveSnapshotCount > 6)
                    throw new InvalidOperationException("Afterimage snapshots must use a bounded pool.");
                effect.ClearSnapshots();
                if (effect.ActiveSnapshotCount != 0)
                    throw new InvalidOperationException("Afterimage cleanup failed.");
                Debug.Log($"Dash afterimages validated: pose captured at {snapshot.size.y:F2}m height; world-space snapshots stay behind; pool limited to 6; cleanup passed; crimson-white URP shader compiled.");
            }
            finally
            {
                player.position = original;
                effect.ClearSnapshots();
                walk.SampleKeyboardRun(0f, 0f);
            }
        }

        private static void EnsureDashAccentMaterials()
        {
            Shader shader = Shader.Find("Survivor/DashWisp");
            if (shader == null || ShaderUtil.ShaderHasError(shader))
                throw new InvalidOperationException("Dash wisp shader failed compilation.");
            foreach (string name in new[] { "DashIvoryStreak", "DashAsh" })
            {
                string path = $"Assets/Resources/{name}.mat";
                if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) continue;
                Material material = new Material(shader) { name = name };
                bool ash = name == "DashAsh";
                material.SetColor("_Tint", ash ? new Color(.9f,.87f,.8f,1f) : new Color(1.4f,1.3f,1.1f,1f));
                material.SetFloat("_ParticleMode", ash ? 1f : 0f);
                AssetDatabase.CreateAsset(material, path);
            }
        }

        private static void ValidateDashAccents(GameObject player)
        {
            PlayerDashTrail effect = player.GetComponent<PlayerDashTrail>();
            if (effect == null) throw new InvalidOperationException("Existing dash trail component missing.");
            effect.ConfigureVisuals();
            TrailRenderer streak = effect.DashTrail;
            if (streak.widthMultiplier > .07f || streak.time > .08f || streak.sharedMaterial == null ||
                streak.sharedMaterial.name != "DashIvoryStreak")
                throw new InvalidOperationException("Thick colored trail was not replaced by the short ivory streak.");
            try
            {
                effect.EmitAshPuff(false);
                effect.EmitAshPuff(true);
                ParticleSystem ash = effect.AshParticles;
                if (ash.main.simulationSpace != ParticleSystemSimulationSpace.World || ash.main.maxParticles > 40 ||
                    ash.particleCount != 20 || ash.GetComponent<Collider>() != null)
                    throw new InvalidOperationException("Ash puff count, world-space simulation or no-collider check failed.");
                ash.Simulate(.1f, true, false, true);
                var particles = new ParticleSystem.Particle[40];
                int alive = ash.GetParticles(particles);
                if (alive < 16 || particles[0].GetCurrentSize(ash) < .2f || particles[0].GetCurrentColor(ash).a < 60)
                    throw new InvalidOperationException("Ash puff size, opacity or lifetime is too low to read.");
                Camera camera = Camera.main;
                if (camera == null) throw new InvalidOperationException("A camera is needed to check ash billboard geometry.");
                Mesh geometry = new Mesh();
                try
                {
                    ash.GetComponent<ParticleSystemRenderer>().BakeMesh(geometry, camera, ParticleSystemBakeMeshOptions.Default);
                    if (geometry.vertexCount < 40)
                        throw new InvalidOperationException("Ash particles did not generate renderable billboard geometry.");
                }
                finally { UnityEngine.Object.DestroyImmediate(geometry); }
                effect.ClearAsh();
                if (ash.particleCount != 0) throw new InvalidOperationException("Ash cleanup failed.");
                Debug.Log($"Visible ash validated: {alive} particles alive at 100ms; billboard mesh generated; larger 30-50cm puffs lifted above floor, stronger opacity and 320-500ms fade; ivory streak and afterimages unchanged.");
            }
            finally { effect.ClearAsh(); }
        }
    }
}
