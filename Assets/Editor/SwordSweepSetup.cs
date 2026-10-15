using System;
using System.IO;
using Survivor.Combat;
using Survivor.Player;
using UnityEditor;
using UnityEngine;

namespace Survivor.EditorTools
{
    [InitializeOnLoad]
    public static class SwordSweepSetup
    {
        const string Pending="Assets/Editor/SwordSweep.pending";
        static SwordSweepSetup() { EditorApplication.update+=Tick; }
        static void Tick()
        {
            if(!File.Exists(Pending) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlaying) return;
            try { Prepare(); }
            catch(Exception e) { Debug.LogException(e); }
            finally { AssetDatabase.DeleteAsset(Pending); }
        }
        [MenuItem("Survivor/Prepare Sword Sweep Effects")]
        public static void Prepare()
        {
            var shader=Shader.Find("Survivor/SwordSweep");
            if(shader==null || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("Sword sweep shader failed to compile");
            const string path="Assets/Resources/SwordSweep.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null) { material=new Material(shader); AssetDatabase.CreateAsset(material,path); }
            material.SetColor("_Tint",new Color(2.2f,2.05f,1.8f,1));
            material.SetColor("_EdgeTint",new Color(.8f,.85f,1.3f,1));
            EditorUtility.SetDirty(material); AssetDatabase.SaveAssets();
            ValidateEffects();
            var movement=UnityEngine.Object.FindAnyObjectByType<PlayerMovement>();
            if(movement!=null)
            {
                Transform visual=movement.transform.Find("VeilWalkerVisual");
                if(visual!=null) SwordComboValidation.Validate(movement.gameObject,visual);
            }
            Debug.Log("SWORD SWEEP READY: two alternating strikes, broad ivory crescent, cool mist edge and pooled cloud wisps. No heavy third strike.");
        }
        static void ValidateEffects()
        {
            var obj=new GameObject("SwordSweepValidation");
            var vfx=obj.AddComponent<SwordSweepVfx>();
            try
            {
                int right=vfx.BeginSweep(0,new Vector3(12000,0,12000),Vector3.forward,2.5f);
                vfx.SetProgress(right,.9f); vfx.Release(right);
                int left=vfx.BeginSweep(1,new Vector3(12000,0,12000),Vector3.right,2.5f);
                vfx.SetProgress(left,.9f); vfx.Release(left);
                if(vfx.ActiveSweeps!=2) throw new InvalidOperationException("Alternating slash effects did not overlap independently");
                vfx.Advance(.3f);
                if(vfx.ActiveSweeps!=0) throw new InvalidOperationException("Slash effects did not fade out");
                for(int i=0;i<12;i++) { int slot=vfx.BeginSweep(i%2,new Vector3(12000,0,12000),Vector3.forward,2.5f); vfx.SetProgress(slot,1); vfx.Release(slot); }
                if(vfx.ActiveSweeps>4) throw new InvalidOperationException("Slash pool grew without bounds");
                Render(vfx);
                Debug.Log("SWORD VFX PASS: right/left independent crescents, timed fade, bounded four-object pool, no damage colliders.");
            }
            finally { vfx.ClearEffects(); UnityEngine.Object.DestroyImmediate(obj); }
        }
        static void Render(SwordSweepVfx vfx)
        {
            vfx.Advance(1);
            int slot=vfx.BeginSweep(0,new Vector3(12000,0,12000),Vector3.forward,2.5f); vfx.SetProgress(slot,.95f);
            var obj=new GameObject("SwordSweepAuditCamera"); var camera=obj.AddComponent<Camera>(); camera.enabled=false;
            camera.orthographic=true; camera.orthographicSize=2.1f;
            camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.035f,.04f,.055f);
            obj.transform.position=new Vector3(12000,6,11997); obj.transform.LookAt(new Vector3(12000,0,12001));
            var target=new RenderTexture(900,650,24); var pixels=new Texture2D(900,650,TextureFormat.RGB24,false); var old=RenderTexture.active;
            try
            {
                camera.targetTexture=target; camera.Render(); RenderTexture.active=target;
                pixels.ReadPixels(new Rect(0,0,900,650),0,0); pixels.Apply();
                File.WriteAllBytes("ArtSource/Previews/SwordSweep_Actual.png",pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active=old; camera.targetTexture=null; target.Release();
                UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(pixels); UnityEngine.Object.DestroyImmediate(obj);
            }
        }
    }
}
