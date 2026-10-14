using UnityEngine;
using UnityEngine.SceneManagement;

namespace Survivor.Player
{
    /// <summary>Fits the approved character to the existing gameplay capsule.</summary>
    [ExecuteAlways]
    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent]
    public sealed class PlayerModelVisual : MonoBehaviour
    {
        [SerializeField] private GameObject characterPrefab;
        [SerializeField] private GameObject swordPrefab;
        [SerializeField, Min(.1f)] private float fallbackHeight = 2f;
        private GameObject visualInstance;

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            BuildVisual();
        }

        private void Start()
        {
            // OnEnable can run before scene.isLoaded becomes true on Retry.
            // Start is a second safe entry point after scene activation.
            BuildVisual();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene == gameObject.scene) BuildVisual();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            ClearVisual();
        }

        public void Configure(GameObject model, GameObject sword)
        {
            characterPrefab = model;
            swordPrefab = sword;
            RebuildVisual();
        }

        [ContextMenu("Rebuild Veil Walker Visual")]
        public void RebuildVisual()
        {
            ClearVisual();
            BuildVisual();
        }

        private void BuildVisual()
        {
            if (visualInstance != null || characterPrefab == null) return;
            // Never create preview geometry on a prefab asset itself.
            if (!gameObject.scene.IsValid() || !gameObject.scene.isLoaded) return;

            if (TryGetComponent(out DarkKnightVisual legacy)) legacy.enabled = false;
            if (TryGetComponent(out MeshRenderer capsuleRenderer)) capsuleRenderer.enabled = false;
            Transform oldModel = transform.Find("DarkKnightModel");
            if (oldModel != null) oldModel.gameObject.SetActive(false);

            visualInstance = Instantiate(characterPrefab, transform, false);
            visualInstance.name = "VeilWalkerVisual";
            if (!Application.IsPlaying(gameObject))
                visualInstance.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;

            Transform visual = visualInstance.transform;
            visual.localPosition = Vector3.zero;
            visual.localRotation = Quaternion.identity;
            visual.localScale = Vector3.one;
            foreach (SkinnedMeshRenderer skin in visualInstance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                skin.updateWhenOffscreen = true;
            FitToCapsule(visual);

            PlayerWeaponEquipment equipment = visualInstance.GetComponent<PlayerWeaponEquipment>();
            if (equipment != null)
            {
                equipment.EquipGun(null);
                equipment.EquipSword(swordPrefab);
            }
            foreach (Transform child in visualInstance.GetComponentsInChildren<Transform>(true))
                child.gameObject.layer = gameObject.layer;
            foreach (SkinnedMeshRenderer skin in visualInstance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                skin.updateWhenOffscreen = true;
            // Attach after fitting: the imported rig's 100x unit conversion must
            // be accounted for before capturing the resting foot positions.
            VeilWalkerLocomotion locomotion = visualInstance.AddComponent<VeilWalkerLocomotion>();
            locomotion.Initialize();
            Survivor.Combat.PlayerSwordAttack swordAttack = visualInstance.AddComponent<Survivor.Combat.PlayerSwordAttack>();
            swordAttack.Initialize();
            PlayerDashAfterimages afterimages = visualInstance.AddComponent<PlayerDashAfterimages>();
            afterimages.Initialize();
        }

        private void FitToCapsule(Transform visual)
        {
            SkinnedMeshRenderer skin = visual.GetComponentInChildren<SkinnedMeshRenderer>();
            if (skin == null) return;
            Mesh baked = new Mesh { name = "TemporaryPlayerFit" };
            try
            {
                skin.BakeMesh(baked);
                Vector3[] vertices = baked.vertices;
                if (vertices.Length == 0) return;
                Matrix4x4 toVisual = visual.worldToLocalMatrix * skin.transform.localToWorldMatrix;
                // BakeMesh can include FBX rig scaling differently from the
                // renderer transform. Fit using the actual visible bounds.
                Bounds visible = skin.bounds;
                Bounds bounds = new Bounds(visual.InverseTransformPoint(visible.center), Vector3.zero);
                for (int x = -1; x <= 1; x += 2)
                    for (int y = -1; y <= 1; y += 2)
                        for (int z = -1; z <= 1; z += 2)
                            bounds.Encapsulate(visual.InverseTransformPoint(visible.center +
                                Vector3.Scale(visible.extents, new Vector3(x, y, z))));

                // Detect the imported front from the ivory mask, so mouse aiming
                // stays correct regardless of the FBX coordinate conversion.
                Material[] materials = skin.sharedMaterials;
                for (int slot = 0; slot < materials.Length && slot < baked.subMeshCount; slot++)
                {
                    if (materials[slot] == null || materials[slot].name != "IvoryMask") continue;
                    int[] indices = baked.GetIndices(slot);
                    if (indices.Length == 0) break;
                    Vector3 maskCenter = Vector3.zero;
                    foreach (int index in indices) maskCenter += toVisual.MultiplyPoint3x4(vertices[index]);
                    maskCenter /= indices.Length;
                    if (maskCenter.z < -.01f) visual.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    break;
                }

                float height = fallbackHeight;
                float foot = -height * .5f;
                if (TryGetComponent(out CapsuleCollider capsule) && capsule.direction == 1)
                {
                    height = capsule.height;
                    foot = capsule.center.y - capsule.height * .5f;
                }
                if (bounds.size.y < .01f) return;
                float scale = height / bounds.size.y;
                visual.localScale = Vector3.one * scale;
                visual.localPosition = new Vector3(0f, foot - bounds.min.y * scale, 0f);
            }
            finally
            {
                if (Application.IsPlaying(gameObject)) Destroy(baked);
                else DestroyImmediate(baked);
            }
        }

        private void ClearVisual()
        {
            if (visualInstance == null) return;
            visualInstance.SetActive(false);
            if (Application.IsPlaying(gameObject)) Destroy(visualInstance);
            else DestroyImmediate(visualInstance);
            visualInstance = null;
        }
    }
}
