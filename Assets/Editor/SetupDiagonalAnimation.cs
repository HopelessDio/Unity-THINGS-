#pragma warning disable 0618
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

[InitializeOnLoad]
public static class SetupDiagonalAnimation
{
    private const string TexturePath = "Assets/Art/Characters/PlayerDiagonalSheet.png";
    private const string ClipPath = "Assets/Animations/PlayerDiagonal.anim";
    private const string ControllerPath = "Assets/PlayerVisual.controller";
    private const string StateName = "PlayerDiagonal";
    private const int FrameCount = 6;
    private const int FrameWidth = 362;
    private const int FrameHeight = 724;
    private const float SamplesPerSecond = 12f;

    static SetupDiagonalAnimation()
    {
        EditorApplication.delayCall += Setup;
    }

    [MenuItem("Tools/2D Survivor/Setup Diagonal Animation")]
    public static void Setup()
    {
        TextureImporter importer = AssetImporter.GetAtPath(TexturePath) as TextureImporter;
        if (importer == null)
        {
            Debug.LogWarning("Diagonal animation setup is waiting for PlayerDiagonalSheet.png to import.");
            return;
        }

        SpriteMetaData[] frameData = BuildFrameData();
        if (NeedsTextureSetup(importer, frameData))
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 768f;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spritesheet = frameData;
            importer.SaveAndReimport();
        }

        Sprite[] frames = AssetDatabase.LoadAllAssetsAtPath(TexturePath)
            .OfType<Sprite>()
            .OrderBy(sprite => sprite.name)
            .ToArray();

        if (frames.Length != FrameCount)
        {
            Debug.LogError($"Diagonal animation needs {FrameCount} sprites, but Unity imported {frames.Length}.");
            return;
        }

        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
        if (clip == null)
        {
            clip = new AnimationClip { name = StateName };
            AssetDatabase.CreateAsset(clip, ClipPath);
        }

        clip.frameRate = SamplesPerSecond;
        EditorCurveBinding spriteBinding = new EditorCurveBinding
        {
            path = string.Empty,
            type = typeof(SpriteRenderer),
            propertyName = "m_Sprite"
        };

        ObjectReferenceKeyframe[] keys = new ObjectReferenceKeyframe[FrameCount];
        for (int i = 0; i < FrameCount; i++)
        {
            keys[i] = new ObjectReferenceKeyframe
            {
                time = i / SamplesPerSecond,
                value = frames[i]
            };
        }

        AnimationUtility.SetObjectReferenceCurve(clip, spriteBinding, keys);
        AnimationClipSettings clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
        clipSettings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, clipSettings);
        EditorUtility.SetDirty(clip);

        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            Debug.LogError("Could not find Assets/PlayerVisual.controller.");
            return;
        }

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        AnimatorState state = stateMachine.states
            .Select(child => child.state)
            .FirstOrDefault(candidate => candidate.name == StateName);

        if (state == null)
            state = stateMachine.AddState(StateName, new Vector3(270f, 200f));

        state.motion = clip;
        state.speed = 1f;
        EditorUtility.SetDirty(state);
        EditorUtility.SetDirty(stateMachine);
        EditorUtility.SetDirty(controller);

        AssetDatabase.SaveAssets();
        Debug.Log("Diagonal player animation is ready: 6 frames, 12 samples, looping enabled.");
    }

    private static SpriteMetaData[] BuildFrameData()
    {
        SpriteMetaData[] frames = new SpriteMetaData[FrameCount];
        for (int i = 0; i < FrameCount; i++)
        {
            frames[i] = new SpriteMetaData
            {
                name = $"PlayerDiagonalSheet_{i}",
                rect = new Rect(i * FrameWidth, 0f, FrameWidth, FrameHeight),
                alignment = (int)SpriteAlignment.Custom,
                pivot = new Vector2(0.5f, 0.5f)
            };
        }

        return frames;
    }

    private static bool NeedsTextureSetup(TextureImporter importer, SpriteMetaData[] expected)
    {
        if (importer.textureType != TextureImporterType.Sprite ||
            importer.spriteImportMode != SpriteImportMode.Multiple ||
            !Mathf.Approximately(importer.spritePixelsPerUnit, 768f) ||
            importer.spritesheet.Length != expected.Length)
        {
            return true;
        }

        for (int i = 0; i < expected.Length; i++)
        {
            SpriteMetaData current = importer.spritesheet[i];
            if (current.name != expected[i].name || current.rect != expected[i].rect)
                return true;
        }

        return false;
    }
}
#pragma warning restore 0618
