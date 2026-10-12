using System.Collections;
using UnityEngine;

namespace Survivor.Enemies
{
    public sealed class EnemyHitFlash : MonoBehaviour
    {
        [SerializeField] private Color flashColor = Color.white;
        [SerializeField, Min(0.01f)] private float flashDuration = 0.08f;

        private Renderer targetRenderer;
        private Material runtimeMaterial;
        private Color normalColor;
        private Coroutine flashRoutine;

        private void Awake()
        {
            targetRenderer = GetComponentInChildren<Renderer>();

            if (targetRenderer == null)
            {
                Debug.LogError("Enemy Hit Flash needs a Renderer.", this);
                enabled = false;
                return;
            }

            runtimeMaterial = targetRenderer.material;
            normalColor = runtimeMaterial.color;
        }

        public void Play()
        {
            if (!enabled)
            {
                return;
            }

            if (flashRoutine != null)
            {
                StopCoroutine(flashRoutine);
            }

            flashRoutine = StartCoroutine(Flash());
        }

        private IEnumerator Flash()
        {
            runtimeMaterial.color = flashColor;
            yield return new WaitForSeconds(flashDuration);
            runtimeMaterial.color = normalColor;
            flashRoutine = null;
        }

        private void OnDisable()
        {
            if (runtimeMaterial != null)
            {
                runtimeMaterial.color = normalColor;
            }
        }

        private void OnDestroy()
        {
            if (runtimeMaterial != null)
            {
                Destroy(runtimeMaterial);
            }
        }
    }
}
