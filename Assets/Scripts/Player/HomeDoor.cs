using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Survivor.Player
{
    /// <summary>Deliberate, one-way entrance. The destination scene owns its player/spawn.</summary>
    public sealed class HomeDoor : MonoBehaviour
    {
        [SerializeField] private string destination = "Assets/Scenes/Level01_HomeInterior.unity";
        [SerializeField, Min(.5f)] private float interactionRadius = 1.9f;
        private InputAction interact;
        private PlayerMovement player;
        private GUIStyle promptStyle;
        public static bool IsTransitioning => DoorFade.Active;
        public bool CanEnter
        {
            get
            {
                if (player == null || !player.isActiveAndEnabled || IsTransitioning || Time.timeScale <= 0) return false;
                var health = player.GetComponent<PlayerHealth>();
                Vector3 delta = player.transform.position - transform.position;
                return health != null && !health.IsDead && Mathf.Abs(delta.y) < 2.5f &&
                    new Vector2(delta.x, delta.z).sqrMagnitude <= interactionRadius * interactionRadius;
            }
        }

        private void Awake()
        {
            interact = new InputAction("Enter home", InputActionType.Button);
            interact.AddBinding("<Keyboard>/e");
            interact.AddBinding("<Gamepad>/buttonNorth");
            player = FindAnyObjectByType<PlayerMovement>();
        }
        private void OnEnable() => interact.Enable();
        private void OnDisable() => interact.Disable();
        private void OnDestroy() => interact.Dispose();
        private void Update()
        {
            if (interact.WasPressedThisFrame()) TryEnter();
        }
        public bool TryEnter()
        {
            if (!CanEnter) return false;
            if (!Application.CanStreamedLevelBeLoaded(destination))
            {
                Debug.LogError("Home scene is missing or disabled in Build Settings: " + destination, this);
                return false;
            }
            DoorFade.Begin(destination, player);
            return true;
        }
        private void OnGUI()
        {
            if (!CanEnter) return;
            if (promptStyle == null) promptStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 20,
                normal = { textColor = new Color(1f, .9f, .75f) }
            };
            string key = Gamepad.current != null && Keyboard.current == null ? "Y / Triangle" : "E";
            GUI.Box(new Rect(Screen.width * .5f - 145, Screen.height - 100, 290, 48), key + "  —  Enter home", promptStyle);
        }
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1, .65f, .2f);
            Gizmos.DrawWireSphere(transform.position, interactionRadius);
        }
    }

    /// <summary>Survives only the fade, not the player. Unscaled time works while gameplay is paused.</summary>
    public sealed class DoorFade : MonoBehaviour
    {
        public static bool Active { get; private set; }
        private Image curtain;
        private float previousTimeScale;
        private Behaviour[] paused;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState() => Active = false;

        public static void Begin(string destination, PlayerMovement player)
        {
            if (Active) return;
            Active = true;
            var root = new GameObject("Home door fade");
            DontDestroyOnLoad(root);
            root.AddComponent<DoorFade>().StartCoroutine(root.GetComponent<DoorFade>().Travel(destination, player));
        }
        private IEnumerator Travel(string destination, PlayerMovement player)
        {
            previousTimeScale = Time.timeScale;
            paused = System.Array.FindAll(player.GetComponents<Behaviour>(), b => b.enabled &&
                (b is PlayerMovement || b is PlayerAim || b is Survivor.Combat.PlayerSwordAttack));
            foreach (var item in paused) item.enabled = false;
            player.GetComponent<Rigidbody>().linearVelocity = Vector3.zero;
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;
            var sheet = new GameObject("Black curtain", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            sheet.transform.SetParent(transform, false);
            curtain = sheet.GetComponent<Image>();
            curtain.color = Color.clear;
            curtain.raycastTarget = true;
            var rect = (RectTransform)sheet.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            Time.timeScale = 0;
            yield return Fade(0, 1, .3f);
            AsyncOperation load = null;
            try { load = SceneManager.LoadSceneAsync(destination, LoadSceneMode.Single); }
            catch (System.Exception error) { Debug.LogException(error); }
            if (load == null)
            {
                foreach (var item in paused) if (item != null) item.enabled = true;
                Time.timeScale = previousTimeScale;
            }
            else
            {
                while (!load.isDone) yield return null;
                Time.timeScale = 1;
                yield return null; // Let the destination's player/UI initialize under black.
            }
            yield return Fade(1, 0, .4f);
            Destroy(gameObject);
        }
        private IEnumerator Fade(float from, float to, float duration)
        {
            for (float elapsed = 0; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                curtain.color = new Color(0, 0, 0, Mathf.Lerp(from, to, Mathf.SmoothStep(0, 1, elapsed / duration)));
                yield return null;
            }
            curtain.color = new Color(0, 0, 0, to);
        }
        private void OnDestroy()
        {
            // Also recover cleanly if stopped/cancelled mid-fade in the Editor.
            if (Time.timeScale == 0) Time.timeScale = previousTimeScale > 0 ? previousTimeScale : 1;
            Active = false;
        }
    }
}
