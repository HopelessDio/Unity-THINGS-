using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Survivor.EditorTools
{
    /// <summary>Editor-only viewing preferences; does not rewrite the user's window layout file.</summary>
    [InitializeOnLoad]
    public static class CrispGameViewSetup
    {
        private const string Pending = "Assets/Editor/CrispGameViewSetup.pending";
        static CrispGameViewSetup() { EditorApplication.update += Tick; }
        private static void Tick()
        {
            if (!File.Exists(Pending) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            try { Apply(); }
            catch (Exception error) { Debug.LogException(error); }
            finally { AssetDatabase.DeleteAsset(Pending); }
        }
        [MenuItem("Survivor/Presentation/Crisp Game View")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Save scene edits and stop Play before refreshing the presentation settings.");
            // Reload only if the open level has not yet picked up its on-disk AA setting.
            Scene scene = SceneManager.GetActiveScene();
            var camera = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Camera>(true)).FirstOrDefault(c => c.CompareTag("MainCamera"));
            if (camera != null && camera.TryGetComponent<UniversalAdditionalCameraData>(out var data) &&
                data.antialiasing != AntialiasingMode.SubpixelMorphologicalAntiAliasing &&
                (scene.path == HomeDoorSetup.Outside || scene.path == HomeInteriorSetup.ScenePath))
                EditorSceneManager.OpenScene(scene.path, OpenSceneMode.Single);
            var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            if (type == null) throw new InvalidOperationException("Unity Game view type not found.");
            var view = EditorWindow.GetWindow(type);
            view.Show(); view.Focus(); view.maximized = true;
            // Wait until the maximized viewport has its final dimensions before resetting zoom.
            EditorApplication.delayCall += () => EditorApplication.delayCall += () => ConfigureView(view);
        }
        private static void ConfigureView(EditorWindow view)
        {
            try
            {
                if (view == null) return;
                var serialized = new SerializedObject(view);
                serialized.Update();
                var scale = serialized.FindProperty("m_ZoomArea.m_Scale");
                var translation = serialized.FindProperty("m_ZoomArea.m_Translation");
                if (scale == null || translation == null) throw new InvalidOperationException("Game view zoom properties changed in this Unity version.");
                scale.vector2Value = Vector2.one;
                translation.vector2Value = Vector2.zero;
                var lowResolution = serialized.FindProperty("m_LowResolutionForAspectRatios");
                if (lowResolution != null && lowResolution.isArray)
                    for (int i = 0; i < lowResolution.arraySize; i++) lowResolution.GetArrayElementAtIndex(i).boolValue = false;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                view.Repaint();
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                EditorApplication.delayCall += () => Verify(view);
            }
            catch (Exception error) { Debug.LogException(error); }
        }
        private static void Verify(EditorWindow view)
        {
            var serialized = new SerializedObject(view);
            serialized.Update();
            Vector2 scale = serialized.FindProperty("m_ZoomArea.m_Scale").vector2Value;
            if ((scale - Vector2.one).sqrMagnitude > .0001f || !view.maximized) throw new InvalidOperationException("Game view did not retain 1x/maximized settings.");
            foreach (string path in new[] { HomeDoorSetup.Outside, HomeInteriorSetup.ScenePath })
            {
                string file = File.ReadAllText(path);
                if (!file.Contains("m_Antialiasing: 2") || !file.Contains("m_RenderPostProcessing: 1"))
                    throw new InvalidOperationException("SMAA/post processing not saved in " + path);
            }
            var camera = Camera.main;
            if (camera != null)
            {
                var data = camera.GetUniversalAdditionalCameraData();
                if (data.antialiasing != AntialiasingMode.SubpixelMorphologicalAntiAliasing || !data.renderPostProcessing)
                    throw new InvalidOperationException("Open camera did not pick up SMAA.");
            }
            Debug.Log($"CRISP VIEW PASS: Game view maximized, zoom={scale}, low-resolution aspect mode disabled; both saved level cameras use High SMAA with post processing; PC render scale stays 1. No geometry, lighting, colour or gameplay changes.");
        }
    }
}
