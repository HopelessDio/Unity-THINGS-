using System;
using System.IO;
using System.Linq;
using Survivor.Enemies;
using Survivor.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

namespace Survivor.EditorTools
{
    [InitializeOnLoad]
    public static class HomeDiscoverySetup
    {
        private const string Pending = "Assets/Editor/HomeDiscoverySetup.pending";
        private const string CorpseMesh = "Assets/Models/Characters/Mother/MotherRestingPose.asset";
        private const string CorpsePrefab = "Assets/Prefab/Characters/Mother/MotherResting.prefab";
        static HomeDiscoverySetup() { EditorApplication.update += Tick; }
        private static void Tick()
        {
            if (!File.Exists(Pending) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            try { Install(); }
            catch (Exception error) { Debug.LogException(error); }
            finally { AssetDatabase.DeleteAsset(Pending); }
        }
        [MenuItem("Survivor/Levels/Stage Home Discovery")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Save scene edits and stop Play mode first.");
            string outdoorHash = Hash(HomeDoorSetup.Outside);
            string backup = "Assets/Scenes/Backups/HomeInterior_BeforeDiscovery_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unity";
            if (!AssetDatabase.CopyAsset(HomeInteriorSetup.ScenePath, backup)) throw new IOException("Interior backup failed.");
            Scene scene = SceneManager.GetActiveScene().path == HomeInteriorSetup.ScenePath ? SceneManager.GetActiveScene() :
                EditorSceneManager.OpenScene(HomeInteriorSetup.ScenePath, OpenSceneMode.Single);
            var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
            Vector3 origin = all.Single(t => t.name == "H1_Origin").position;
            Vector3 ax = all.Single(t => t.name == "H1_BasisX").position - origin;
            Vector3 ay = all.Single(t => t.name == "H1_BasisY").position - origin;
            Vector3 az = all.Single(t => t.name == "H1_BasisZ").position - origin;
            Vector3 C(float x, float y, float z) => origin + ax * x + ay * y + az * z;
            // Generate a separate low-cost static pose; the living rig/prefab are untouched.
            BuildMotherPose();
            var previous = scene.GetRootGameObjects().FirstOrDefault(r => r.name == "Home_Discovery");
            if (previous != null) UnityEngine.Object.DestroyImmediate(previous);
            var living = scene.GetRootGameObjects().FirstOrDefault(r => r.name == "Mother");
            if (living != null) UnityEngine.Object.DestroyImmediate(living);
            var root = new GameObject("Home_Discovery");
            var mother = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(CorpsePrefab), scene);
            mother.name = "Mother";
            mother.transform.SetParent(root.transform, false);
            // Baked head points along -X; align that direction with the room's authored -X.
            mother.transform.rotation = Quaternion.AngleAxis(Vector3.SignedAngle(Vector3.left, -ax.normalized, Vector3.up), Vector3.up);
            mother.transform.position = C(-2.1f, .025f, 2.65f);

            var zombie = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(AshboundZombieSetup.PrefabPath), scene);
            PrefabUtility.UnpackPrefabInstance(zombie, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            zombie.name = "FeedingZombie";
            zombie.transform.SetParent(root.transform, false);
            zombie.transform.SetPositionAndRotation(C(-3.15f, 1.08f, 1.35f), Quaternion.LookRotation(az.normalized, ay.normalized));
            SampleIdle(zombie);
            var animator = zombie.GetComponentInChildren<Animator>();
            var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            var lf = animator.GetBoneTransform(HumanBodyBones.LeftFoot).position;
            var rf = animator.GetBoneTransform(HumanBodyBones.RightFoot).position;
            animator.enabled = false;
            hips.position -= Vector3.up * .62f;
            RotateWorld(animator.GetBoneTransform(HumanBodyBones.Spine), zombie.transform.right, 42);
            RotateWorld(animator.GetBoneTransform(HumanBodyBones.Chest), zombie.transform.right, 15);
            RotateWorld(animator.GetBoneTransform(HumanBodyBones.Neck), zombie.transform.right, -10);
            Solve(animator, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, lf, zombie.transform.forward);
            Solve(animator, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, rf, zombie.transform.forward);
            Vector3 floor = C(-3.15f, 0, 1.35f);
            Solve(animator, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
                floor + zombie.transform.forward * .65f + Vector3.up * .35f - zombie.transform.right * .22f, -zombie.transform.right);
            Solve(animator, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
                floor + zombie.transform.forward * .65f + Vector3.up * .35f + zombie.transform.right * .22f, zombie.transform.right);
            // Visual-only staging: no pursuit, damage, score event, solid capsule or gravity.
            UnityEngine.Object.DestroyImmediate(zombie.GetComponent<AshboundZombie>());
            foreach (var behaviour in zombie.GetComponents<MonoBehaviour>()) UnityEngine.Object.DestroyImmediate(behaviour);
            foreach (var collider in zombie.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(collider);
            foreach (var body in zombie.GetComponentsInChildren<Rigidbody>()) UnityEngine.Object.DestroyImmediate(body);
            zombie.AddComponent<FeedingZombiePose>().Configure(animator.GetBoneTransform(HumanBodyBones.Head),
                animator.GetBoneTransform(HumanBodyBones.Neck), animator.GetBoneTransform(HumanBodyBones.LeftUpperArm),
                animator.GetBoneTransform(HumanBodyBones.RightUpperArm));
            // Remove the stage copy's Animator so it cannot rebind the saved crouch on load.
            // The normal reusable combat prefab retains its Humanoid Animator unchanged.
            UnityEngine.Object.DestroyImmediate(animator);
            // A restrained flat stain reads from the gameplay camera; no detailed gore.
            string stainPath = "Assets/Materials/HomeInterior/DiscoveryBlood.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(stainPath);
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, stainPath); }
            material.SetColor("_BaseColor", new Color(.13f, .006f, .012f));
            material.SetFloat("_Smoothness", .35f);
            var stain = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stain.name = "Restrained blood pool"; stain.transform.SetParent(root.transform, false);
            stain.transform.position = C(-3.15f, .008f, 2.65f);
            stain.transform.localScale = new Vector3(1.35f, .004f, .8f);
            UnityEngine.Object.DestroyImmediate(stain.GetComponent<Collider>());
            stain.GetComponent<Renderer>().sharedMaterial = material;
            Physics.SyncTransforms();
            Validate(scene, mother, zombie);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (Hash(HomeDoorSetup.Outside) != outdoorHash) throw new InvalidOperationException("Outdoor scene unexpectedly changed.");
            Selection.activeGameObject = root;
            Debug.Log("HOME DISCOVERY SETUP PASS: separate static mother pose, crouched visual-only feeding zombie, restrained stain; living mother/enemy prefabs preserved; interior backed up; outdoor unchanged. Reveal trigger/combat not implemented yet.");
        }
        private static void BuildMotherPose()
        {
            var mother = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(VillageMotherSetup.PrefabPath));
            var result = new GameObject("MotherResting");
            try
            {
                mother.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                SampleIdle(mother);
                var animator = mother.GetComponentInChildren<Animator>();
                animator.enabled = false;
                RotateWorld(animator.GetBoneTransform(HumanBodyBones.LeftUpperArm), Vector3.forward, -12);
                RotateWorld(animator.GetBoneTransform(HumanBodyBones.RightLowerArm), Vector3.right, 18);
                RotateWorld(animator.GetBoneTransform(HumanBodyBones.Head), Vector3.up, -18);
                mother.transform.rotation = Quaternion.Euler(0, 90, 0) * Quaternion.Euler(-90, 0, 0);
                var skin = mother.GetComponentInChildren<SkinnedMeshRenderer>();
                var mesh = new Mesh { name = "MotherRestingPose" };
                skin.BakeMesh(mesh, true);
                var points = mesh.vertices;
                for (int i = 0; i < points.Length; i++) points[i] = skin.transform.TransformPoint(points[i]);
                float bottom = points.Min(p => p.y);
                for (int i = 0; i < points.Length; i++) points[i].y -= bottom;
                mesh.vertices = points;
                // FBX may carry a mirrored basis. Once that transform is baked into
                // vertices, reverse the winding too or the upward faces get culled.
                if (skin.transform.localToWorldMatrix.determinant < 0)
                {
                    var indices = mesh.triangles;
                    for (int i = 0; i < indices.Length; i += 3)
                    { int swap = indices[i + 1]; indices[i + 1] = indices[i + 2]; indices[i + 2] = swap; }
                    mesh.triangles = indices;
                }
                // BakeMesh normals are local; transformed vertices need regenerated normals.
                mesh.RecalculateNormals(); mesh.RecalculateBounds();
                var previous = AssetDatabase.LoadAssetAtPath<Mesh>(CorpseMesh);
                if (previous == null) AssetDatabase.CreateAsset(mesh, CorpseMesh);
                else { EditorUtility.CopySerialized(mesh, previous); UnityEngine.Object.DestroyImmediate(mesh); mesh = previous; }
                result.AddComponent<MeshFilter>().sharedMesh = mesh;
                result.AddComponent<MeshRenderer>().sharedMaterials = skin.sharedMaterials;
                if (mesh.bounds.size.y > .6f || mesh.bounds.size.x < 1.4f || mesh.vertexCount > 4000)
                    throw new InvalidOperationException("Mother resting mesh shape/scale is incorrect: " + mesh.bounds);
                PrefabUtility.SaveAsPrefabAsset(result, CorpsePrefab);
            }
            finally { UnityEngine.Object.DestroyImmediate(mother); UnityEngine.Object.DestroyImmediate(result); }
        }
        private static void SampleIdle(GameObject root)
        {
            var animator = root.GetComponentInChildren<Animator>();
            var clip = animator.runtimeAnimatorController.animationClips.First(c => c.name.EndsWith("Idle"));
            var graph = PlayableGraph.Create("Discovery pose sample");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var controller = animator.runtimeAnimatorController;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.runtimeAnimatorController = null;
            try
            {
                var playable = AnimationClipPlayable.Create(graph, clip);
                AnimationPlayableOutput.Create(graph, "Pose", animator).SetSourcePlayable(playable);
                graph.Play(); graph.Evaluate(0);
                var transforms = root.GetComponentsInChildren<Transform>(true);
                var positions = transforms.Select(t => t.localPosition).ToArray();
                var rotations = transforms.Select(t => t.localRotation).ToArray();
                var scales = transforms.Select(t => t.localScale).ToArray();
                graph.Destroy(); animator.runtimeAnimatorController = controller;
                for (int i = 0; i < transforms.Length; i++)
                { transforms[i].localPosition = positions[i]; transforms[i].localRotation = rotations[i]; transforms[i].localScale = scales[i]; }
            }
            finally { if (graph.IsValid()) graph.Destroy(); }
        }
        private static void RotateWorld(Transform bone, Vector3 axis, float degrees)
        { bone.rotation = Quaternion.AngleAxis(degrees, axis) * bone.rotation; }
        private static void Solve(Animator animator, HumanBodyBones upper, HumanBodyBones lower, HumanBodyBones end, Vector3 target, Vector3 pole)
        {
            var a = animator.GetBoneTransform(upper); var b = animator.GetBoneTransform(lower); var c = animator.GetBoneTransform(end);
            float l1 = Vector3.Distance(a.position, b.position), l2 = Vector3.Distance(b.position, c.position);
            Vector3 d = target - a.position; float distance = Mathf.Clamp(d.magnitude, Mathf.Abs(l1 - l2) + .002f, l1 + l2 - .002f);
            Vector3 direction = d.normalized;
            Vector3 bend = Vector3.ProjectOnPlane(pole, direction).normalized;
            float along = (l1 * l1 - l2 * l2 + distance * distance) / (2 * distance);
            Vector3 knee = a.position + direction * along + bend * Mathf.Sqrt(Mathf.Max(0, l1 * l1 - along * along));
            Vector3 final = a.position + direction * distance;
            Quaternion endRotation = c.rotation;
            a.rotation = Quaternion.FromToRotation(b.position - a.position, knee - a.position) * a.rotation;
            b.rotation = Quaternion.FromToRotation(c.position - b.position, final - b.position) * b.rotation;
            c.rotation = endRotation;
        }
        public static void Validate(Scene scene, GameObject mother, GameObject zombie)
        {
            if (mother.GetComponentInChildren<Animator>() != null || mother.GetComponentInChildren<MeshFilter>().sharedMesh.vertexCount > 4000 ||
                zombie.GetComponent<EnemyChaser>() != null || zombie.GetComponent<EnemyContactDamage>() != null ||
                zombie.GetComponent<EnemyHealth>() != null || zombie.GetComponentInChildren<Rigidbody>() != null ||
                zombie.GetComponentInChildren<Animator>() != null || zombie.GetComponent<FeedingZombiePose>() == null)
                throw new InvalidOperationException("Discovery must be a static mother and visual-only zombie, not active combat.");
            if (mother.GetComponentInChildren<Renderer>().bounds.min.y < -.01f || Vector3.Dot(mother.transform.up, Vector3.up) < .99f)
                throw new InvalidOperationException("Mother's resting pose must stay above the floor, without a flipped up axis.");
            var player = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PlayerMovement>(true)).Single();
            if (Physics.CheckCapsule(player.transform.position - Vector3.up * .5f, player.transform.position + Vector3.up * .5f, .49f,
                LayerMask.GetMask("ArenaObstacle"), QueryTriggerInteraction.Ignore)) throw new InvalidOperationException("Home entry obstructed.");
        }
        private static string Hash(string path)
        { using (var sha = System.Security.Cryptography.SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path))); }
    }
}
