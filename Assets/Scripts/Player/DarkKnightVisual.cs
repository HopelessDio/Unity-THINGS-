using UnityEngine;
using UnityEngine.Rendering;

namespace Survivor.Player
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class DarkKnightVisual : MonoBehaviour
    {
        [SerializeField] private Material armorMaterial;
        [SerializeField] private Material trimMaterial;
        [SerializeField] private Material capeMaterial;
        [SerializeField] private Material visorMaterial;
        [SerializeField] private bool hideOriginalRenderer = true;

        private Rigidbody body;
        private Transform cape;
        private Transform[] coatTails;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();

            if (hideOriginalRenderer && TryGetComponent(out MeshRenderer originalRenderer))
            {
                originalRenderer.enabled = false;
            }

            BuildKnight();
        }

        private void LateUpdate()
        {
            if (cape == null || body == null)
            {
                return;
            }

            float speed = new Vector2(body.linearVelocity.x, body.linearVelocity.z).magnitude;
            float flutter = Mathf.Sin(Time.time * 9f) * Mathf.Clamp01(speed / 6f) * 5f;
            Quaternion target = Quaternion.Euler(9f + speed * 1.6f + flutter, 0f, 0f);
            cape.localRotation = Quaternion.Slerp(cape.localRotation, target, Time.deltaTime * 8f);

            if (coatTails == null)
            {
                return;
            }

            for (int index = 0; index < coatTails.Length; index++)
            {
                float offsetFlutter = Mathf.Sin(Time.time * 10f + index * 1.7f) * Mathf.Clamp01(speed / 5f) * 7f;
                Quaternion tailTarget = Quaternion.Euler(8f + speed * 2f + offsetFlutter, 0f, (index - 1) * 5f);
                coatTails[index].localRotation = Quaternion.Slerp(
                    coatTails[index].localRotation,
                    tailTarget,
                    Time.deltaTime * 9f);
            }
        }

        private void BuildKnight()
        {
            if (transform.Find("DarkKnightModel") != null)
            {
                return;
            }

            Transform model = new GameObject("DarkKnightModel").transform;
            model.SetParent(transform, false);

            cape = CreateTatteredPanel("WeatheredMantle", model,
                new Vector3(0f, 0.08f, -0.3f), new Vector2(0.86f, 0.92f),
                new Vector3(9f, 0f, 0f), capeMaterial);
            coatTails = new[]
            {
                CreateTatteredPanel("CoatTailLeft", model,
                    new Vector3(-0.25f, -0.55f, -0.25f), new Vector2(0.34f, 0.92f),
                    new Vector3(8f, 0f, -5f), capeMaterial),
                CreateTatteredPanel("CoatTailCenter", model,
                    new Vector3(0f, -0.58f, -0.28f), new Vector2(0.3f, 1.02f),
                    new Vector3(10f, 0f, 0f), armorMaterial),
                CreateTatteredPanel("CoatTailRight", model,
                    new Vector3(0.25f, -0.55f, -0.25f), new Vector2(0.34f, 0.88f),
                    new Vector3(8f, 0f, 5f), capeMaterial)
            };

            CreatePart("LeanCuirass", PrimitiveType.Cube, model,
                new Vector3(0f, 0.13f, 0f), new Vector3(0.68f, 0.78f, 0.38f),
                new Vector3(-5f, 0f, 0f), armorMaterial);
            CreatePart("LayeredBreastplate", PrimitiveType.Cube, model,
                new Vector3(0f, 0.18f, 0.215f), new Vector3(0.5f, 0.52f, 0.07f),
                new Vector3(-8f, 0f, 0f), trimMaterial);
            CreatePart("LeatherBelt", PrimitiveType.Cube, model,
                new Vector3(0f, -0.27f, 0.02f), new Vector3(0.72f, 0.12f, 0.43f),
                Vector3.zero, capeMaterial);

            CreatePart("MantleLeft", PrimitiveType.Sphere, model,
                new Vector3(-0.46f, 0.35f, -0.03f), new Vector3(0.48f, 0.25f, 0.46f),
                new Vector3(0f, 0f, -14f), capeMaterial);
            CreatePart("SteelPauldron", PrimitiveType.Sphere, model,
                new Vector3(0.44f, 0.34f, 0.02f), new Vector3(0.38f, 0.24f, 0.43f),
                new Vector3(0f, 0f, 12f), trimMaterial);
            CreatePart("LeftBracer", PrimitiveType.Capsule, model,
                new Vector3(-0.46f, -0.06f, 0.06f), new Vector3(0.16f, 0.36f, 0.16f),
                new Vector3(0f, 0f, -7f), armorMaterial);
            CreatePart("RightBracer", PrimitiveType.Capsule, model,
                new Vector3(0.46f, -0.04f, 0.16f), new Vector3(0.16f, 0.36f, 0.16f),
                new Vector3(20f, 0f, 6f), trimMaterial);

            CreatePart("LeftLeg", PrimitiveType.Cube, model,
                new Vector3(-0.18f, -0.62f, 0.03f), new Vector3(0.23f, 0.56f, 0.27f),
                new Vector3(0f, 0f, -2f), armorMaterial);
            CreatePart("RightLeg", PrimitiveType.Cube, model,
                new Vector3(0.18f, -0.62f, 0.03f), new Vector3(0.23f, 0.56f, 0.27f),
                new Vector3(0f, 0f, 2f), armorMaterial);
            CreatePart("LeftBoot", PrimitiveType.Cube, model,
                new Vector3(-0.18f, -0.91f, 0.14f), new Vector3(0.26f, 0.18f, 0.48f),
                Vector3.zero, trimMaterial);
            CreatePart("RightBoot", PrimitiveType.Cube, model,
                new Vector3(0.18f, -0.91f, 0.14f), new Vector3(0.26f, 0.18f, 0.48f),
                Vector3.zero, trimMaterial);

            CreatePart("ShadowedHead", PrimitiveType.Sphere, model,
                new Vector3(0f, 0.77f, 0.015f), new Vector3(0.55f, 0.57f, 0.55f),
                Vector3.zero, armorMaterial);
            CreatePart("BeakedMask", PrimitiveType.Cube, model,
                new Vector3(0f, 0.74f, 0.315f), new Vector3(0.42f, 0.26f, 0.19f),
                new Vector3(-12f, 0f, 0f), trimMaterial);
            CreatePart("NarrowCrimsonEyes", PrimitiveType.Cube, model,
                new Vector3(0f, 0.83f, 0.42f), new Vector3(0.36f, 0.055f, 0.03f),
                Vector3.zero, visorMaterial);
            CreatePart("HelmBrim", PrimitiveType.Cylinder, model,
                new Vector3(0f, 0.98f, 0f), new Vector3(0.55f, 0.045f, 0.5f),
                new Vector3(0f, 7f, 0f), trimMaterial);
            CreateCone("PointedHunterHelm", model,
                new Vector3(0f, 0.99f, -0.03f), 0.38f, 0.7f, 8,
                new Vector3(-5f, 0f, -9f), armorMaterial);

            CreatePart("GreatswordBlade", PrimitiveType.Cube, model,
                new Vector3(-0.3f, 0.15f, -0.39f), new Vector3(0.13f, 1.65f, 0.055f),
                new Vector3(0f, 0f, -31f), trimMaterial);
            CreatePart("GreatswordGuard", PrimitiveType.Cube, model,
                new Vector3(0.12f, -0.52f, -0.4f), new Vector3(0.58f, 0.09f, 0.09f),
                new Vector3(0f, 0f, -31f), armorMaterial);
            CreatePart("GreatswordGrip", PrimitiveType.Cylinder, model,
                new Vector3(0.25f, -0.69f, -0.4f), new Vector3(0.07f, 0.25f, 0.07f),
                new Vector3(0f, 0f, -31f), capeMaterial);

            CreatePart("HunterPistol", PrimitiveType.Cube, model,
                new Vector3(0.43f, -0.01f, 0.6f), new Vector3(0.2f, 0.2f, 0.72f),
                new Vector3(5f, 0f, 0f), trimMaterial);
            CreatePart("PistolMuzzle", PrimitiveType.Cylinder, model,
                new Vector3(0.43f, 0.02f, 0.98f), new Vector3(0.14f, 0.07f, 0.14f),
                new Vector3(90f, 0f, 0f), armorMaterial);
            CreatePart("PistolEmber", PrimitiveType.Sphere, model,
                new Vector3(0.43f, 0.02f, 1.06f), new Vector3(0.105f, 0.105f, 0.105f),
                Vector3.zero, visorMaterial);

            CreatePart("LeftHipDagger", PrimitiveType.Cube, model,
                new Vector3(-0.46f, -0.35f, 0.07f), new Vector3(0.08f, 0.62f, 0.08f),
                new Vector3(0f, 0f, -18f), trimMaterial);
        }

        private Transform CreateTatteredPanel(
            string partName,
            Transform parent,
            Vector3 localPosition,
            Vector2 size,
            Vector3 localEulerAngles,
            Material material)
        {
            GameObject panel = new(partName);
            panel.layer = gameObject.layer;
            panel.transform.SetParent(parent, false);
            panel.transform.localPosition = localPosition;
            panel.transform.localEulerAngles = localEulerAngles;

            float halfWidth = size.x * 0.5f;
            float halfHeight = size.y * 0.5f;
            Mesh mesh = new() { name = $"{partName}Mesh" };
            mesh.vertices = new[]
            {
                new Vector3(-halfWidth, halfHeight, 0f),
                new Vector3(halfWidth, halfHeight, 0f),
                new Vector3(halfWidth * 0.72f, -halfHeight, 0f),
                new Vector3(0f, -halfHeight + size.y * 0.17f, 0f),
                new Vector3(-halfWidth * 0.72f, -halfHeight, 0f)
            };
            mesh.triangles = new[]
            {
                0, 1, 2, 0, 2, 3, 0, 3, 4,
                2, 1, 0, 3, 2, 0, 4, 3, 0
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            panel.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = panel.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            return panel.transform;
        }

        private void CreateCone(
            string partName,
            Transform parent,
            Vector3 localPosition,
            float radius,
            float height,
            int sides,
            Vector3 localEulerAngles,
            Material material)
        {
            GameObject cone = new(partName);
            cone.layer = gameObject.layer;
            cone.transform.SetParent(parent, false);
            cone.transform.localPosition = localPosition;
            cone.transform.localEulerAngles = localEulerAngles;

            Vector3[] vertices = new Vector3[sides + 2];
            int[] triangles = new int[sides * 6];
            vertices[0] = Vector3.zero;
            vertices[1] = new Vector3(0f, height, 0f);

            for (int index = 0; index < sides; index++)
            {
                float angle = index * Mathf.PI * 2f / sides;
                vertices[index + 2] = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);

                int next = (index + 1) % sides;
                int triangle = index * 6;
                triangles[triangle] = 1;
                triangles[triangle + 1] = index + 2;
                triangles[triangle + 2] = next + 2;
                triangles[triangle + 3] = 0;
                triangles[triangle + 4] = next + 2;
                triangles[triangle + 5] = index + 2;
            }

            Mesh mesh = new() { name = $"{partName}Mesh" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            cone.AddComponent<MeshFilter>().sharedMesh = mesh;

            MeshRenderer renderer = cone.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }

        private Transform CreatePart(
            string partName,
            PrimitiveType primitiveType,
            Transform parent,
            Vector3 localPosition,
            Vector3 localScale,
            Vector3 localEulerAngles,
            Material material)
        {
            GameObject part = GameObject.CreatePrimitive(primitiveType);
            part.name = partName;
            part.layer = gameObject.layer;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            part.transform.localEulerAngles = localEulerAngles;

            Collider partCollider = part.GetComponent<Collider>();
            if (partCollider != null)
            {
                partCollider.enabled = false;
                Destroy(partCollider);
            }

            Renderer renderer = part.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            return part.transform;
        }
    }
}
