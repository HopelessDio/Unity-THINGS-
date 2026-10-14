using UnityEngine;
using UnityEngine.Rendering;

namespace Survivor.Player
{
    /// <summary>Pooled mesh snapshots, not cloned players or gameplay objects.</summary>
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class PlayerDashAfterimages : MonoBehaviour
    {
        [SerializeField, Range(2, 10)] private int snapshotCount = 6;
        [SerializeField, Min(.015f)] private float snapshotInterval = .032f;
        [SerializeField, Min(.05f)] private float lifetime = .26f;
        private Material material;
        private PlayerMovement movement;
        private SkinnedMeshRenderer[] skins;
        private MeshFilter[] solids;
        private Snapshot[] pool;
        private MaterialPropertyBlock properties;
        private int nextSlot;
        private float nextCapture;
        private bool wasDashing;
        private static readonly int Age = Shader.PropertyToID("_Age");

        private sealed class Part
        {
            public Mesh mesh;
            public Matrix4x4 matrix;
            public bool owned;
        }
        private sealed class Snapshot
        {
            public Part[] parts;
            public float born;
            public bool active;
        }

        public bool IsReady => pool != null && material != null;
        public int ActiveSnapshotCount
        {
            get
            {
                int count = 0;
                if (pool != null) foreach (Snapshot shot in pool) if (shot.active) count++;
                return count;
            }
        }

        public void Initialize()
        {
            if (pool != null) return;
            material = Resources.Load<Material>("DashAfterimage");
            movement = GetComponentInParent<PlayerMovement>();
            if (material == null || movement == null) return;
            skins = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            solids = GetComponentsInChildren<MeshFilter>(true);
            properties = new MaterialPropertyBlock();
            pool = new Snapshot[snapshotCount];
            for (int i = 0; i < pool.Length; i++)
            {
                Snapshot shot = new Snapshot { parts = new Part[skins.Length + solids.Length] };
                for (int p = 0; p < shot.parts.Length; p++)
                {
                    bool owned = p < skins.Length;
                    Mesh mesh = owned ? new Mesh { name = "DashPoseSnapshot", hideFlags = HideFlags.HideAndDontSave }
                        : solids[p - skins.Length].sharedMesh;
                    if (owned) mesh.MarkDynamic();
                    shot.parts[p] = new Part { mesh = mesh, owned = owned };
                }
                pool[i] = shot;
            }
        }

        private void LateUpdate()
        {
            if (!IsReady) Initialize();
            if (!IsReady) return;
            bool dashing = movement.enabled && movement.IsDashing;
            if (dashing && !wasDashing) nextCapture = Time.time;
            if (dashing && Time.time >= nextCapture)
            {
                CaptureSnapshot();
                nextCapture = Time.time + snapshotInterval;
            }
            wasDashing = dashing;
            foreach (Snapshot shot in pool)
            {
                if (!shot.active) continue;
                float age = (Time.time - shot.born) / lifetime;
                if (age >= 1f) { shot.active = false; continue; }
                properties.SetFloat(Age, Mathf.Clamp01(age));
                foreach (Part part in shot.parts)
                {
                    if (part.mesh == null) continue;
                    for (int sub = 0; sub < part.mesh.subMeshCount; sub++)
                        Graphics.DrawMesh(part.mesh, part.matrix, material, gameObject.layer,
                            null, sub, properties, ShadowCastingMode.Off, false);
                }
            }
        }

        public void CaptureSnapshot()
        {
            if (!IsReady) Initialize();
            if (!IsReady) return;
            Snapshot shot = pool[nextSlot];
            nextSlot = (nextSlot + 1) % pool.Length;
            for (int i = 0; i < skins.Length; i++)
            {
                skins[i].BakeMesh(shot.parts[i].mesh, true);
                // Compensate the imported rig scale while baking, then retain
                // the complete renderer matrix (including the FBX unit scale).
                shot.parts[i].matrix = skins[i].transform.localToWorldMatrix;
            }
            for (int i = 0; i < solids.Length; i++)
                shot.parts[skins.Length + i].matrix = solids[i].transform.localToWorldMatrix;
            shot.born = Time.time;
            shot.active = true;
        }

        public Bounds LatestBodyBounds()
        {
            Snapshot shot = pool[(nextSlot + pool.Length - 1) % pool.Length];
            Part part = shot.parts[0];
            Bounds local = part.mesh.bounds;
            Bounds world = new Bounds(part.matrix.MultiplyPoint3x4(local.center), Vector3.zero);
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
                world.Encapsulate(part.matrix.MultiplyPoint3x4(local.center +
                    Vector3.Scale(local.extents, new Vector3(x,y,z))));
            return world;
        }

        public void ClearSnapshots()
        {
            if (pool != null) foreach (Snapshot shot in pool) shot.active = false;
            wasDashing = false;
        }

        private void OnDisable() => ClearSnapshots();

        private void OnDestroy()
        {
            if (pool == null) return;
            foreach (Snapshot shot in pool)
            foreach (Part part in shot.parts)
            {
                if (!part.owned || part.mesh == null) continue;
                if (Application.isPlaying) Destroy(part.mesh);
                else DestroyImmediate(part.mesh);
            }
        }
    }
}
