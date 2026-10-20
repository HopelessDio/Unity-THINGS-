using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Survivor.Enemies
{
    public sealed class EnemyHitFlash : MonoBehaviour
    {
        [SerializeField] private Color flashColor = Color.white;
        [SerializeField, Min(0.01f)] private float flashDuration = 0.08f;

        private sealed class Slot
        {
            public Renderer renderer;
            public int index;
            public MaterialPropertyBlock before;
            public MaterialPropertyBlock flash;
        }

        private readonly List<Slot> slots = new List<Slot>();
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorProperty = Shader.PropertyToID("_Color");
        private Coroutine flashRoutine;

        private void Awake()
        {
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] == null) continue;
                    slots.Add(new Slot { renderer = renderer, index = i,
                        before = new MaterialPropertyBlock(), flash = new MaterialPropertyBlock() });
                }
            }
            if (slots.Count == 0)
            {
                Debug.LogError("Enemy Hit Flash needs a Renderer.", this);
                enabled = false;
                return;
            }

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
                Restore();
            }

            flashRoutine = StartCoroutine(Flash());
        }

        private IEnumerator Flash()
        {
            // Each skin submaterial flashes; shared assets are never cloned or recolored.
            foreach (Slot slot in slots)
            {
                if (slot.renderer == null) continue;
                slot.renderer.GetPropertyBlock(slot.before, slot.index);
                slot.renderer.GetPropertyBlock(slot.flash, slot.index);
                slot.flash.SetColor(BaseColor, flashColor);
                slot.flash.SetColor(ColorProperty, flashColor);
                slot.renderer.SetPropertyBlock(slot.flash, slot.index);
            }
            yield return new WaitForSeconds(flashDuration);
            Restore();
            flashRoutine = null;
        }

        private void OnDisable()
        {
            if (flashRoutine != null)
            {
                StopCoroutine(flashRoutine);
                Restore();
                flashRoutine = null;
            }
        }

        private void Restore()
        {
            foreach (Slot slot in slots)
                if (slot.renderer != null) slot.renderer.SetPropertyBlock(slot.before, slot.index);
        }
    }
}
