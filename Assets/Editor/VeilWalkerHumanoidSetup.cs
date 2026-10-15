using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Survivor.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Survivor.EditorTools
{
    [InitializeOnLoad]
    public static class VeilWalkerHumanoidSetup
    {
        const string Pending="Assets/Editor/VeilWalkerHumanoid.pending";
        const string Model="Assets/Models/Player/Mixamo/VeilWalker_Mixamo.fbx";
        const string Folder="Assets/Prefab/Player/VeilWalker/Mixamo";
        static VeilWalkerHumanoidSetup() { EditorApplication.update+=Tick; }
        static void Tick()
        {
            if(!File.Exists(Pending) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlaying) return;
            try { Prepare(); }
            catch(Exception e) { Debug.LogException(e); }
            finally { AssetDatabase.DeleteAsset(Pending); }
        }
        [MenuItem("Survivor/Prepare Mixamo Humanoid Rig")]
        public static void Prepare()
        {
            var map=new Dictionary<string,string> {
                ["Hips"]="Hips",["Spine"]="Spine",["Chest"]="Spine1",["UpperChest"]="Spine2",["Neck"]="Neck",["Head"]="Head" };
            foreach(string side in new[]{"Left","Right"})
                foreach(var pair in new[]{("Shoulder","Shoulder"),("UpperArm","Arm"),("LowerArm","ForeArm"),("Hand","Hand"),("UpperLeg","UpLeg"),("LowerLeg","Leg"),("Foot","Foot"),("Toes","ToeBase")})
                    map[side+pair.Item1]=side+pair.Item2;
            var avatar=Configure(Model,map.ToDictionary(p=>p.Key,p=>"mixamorig:"+p.Value),false);
            var sourceMap=new Dictionary<string,string> { ["Hips"]="Hips",["Spine"]="Spine",["Chest"]="Chest",["Neck"]="Neck",["Head"]="Head" };
            // Old files use opposite anatomical side labels. Map spatial sides,
            // not matching strings, into the same Humanoid definition.
            foreach(string side in new[]{"Left","Right"})
                foreach(string part in new[]{"Shoulder","UpperArm","LowerArm","Hand","UpperLeg","LowerLeg","Foot"})
                    sourceMap[side+part]=(side=="Left"?"Right":"Left")+part;
            foreach(string path in Directory.GetFiles("Assets/MaximoAnimations","*.fbx")) Configure(path.Replace('\\','/'),sourceMap,true);
            if(!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Prefab/Player/VeilWalker","Mixamo");
            var instance=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Model));
            try
            {
                instance.name="VeilWalker_Mixamo";
                var animator=instance.GetComponent<Animator>();
                if(animator==null) animator=instance.AddComponent<Animator>();
                animator.avatar=avatar; animator.applyRootMotion=false;
                animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                foreach(var skin in instance.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    skin.updateWhenOffscreen=true;
                    var names=new Dictionary<string,string> { ["Veil_Slate"]="Slate",["Veil_Joints"]="DarkFaceAndCloth",["Veil_Burgundy"]="BurgundyCape",["Veil_IvoryMask"]="IvoryMask",["Veil_AgedBronze"]="AgedBronze",["Veil_EyeVoid"]="EyeVoid" };
                    skin.sharedMaterials=skin.sharedMaterials.Select(m=>m!=null && names.TryGetValue(m.name,out string target)?AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/VeilWalker/"+target+".mat"):m).ToArray();
                }
                var all=instance.GetComponentsInChildren<Transform>(true);
                var gear=instance.AddComponent<PlayerWeaponEquipment>();
                var right=all.First(t=>t.name=="WeaponSocket_Right"); var left=all.First(t=>t.name=="WeaponSocket_Left");
                gear.ConfigureSockets(right,left);
                var grip=new GameObject("SwordOffhandGrip").transform; grip.SetParent(right,false); grip.localPosition=Vector3.back*.00102f;
                gear.ConfigureSwordSupport(grip);
                PrefabUtility.SaveAsPrefabAsset(instance,Folder+"/VeilWalker_Mixamo.prefab");
                string runPath="Assets/MaximoAnimations/VeilWalker_Model01@Great Sword Run.fbx";
                var run=AssetDatabase.LoadAllAssetsAtPath(runPath).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview"));
                if(!run.isHumanMotion) throw new InvalidOperationException("Run clip was not converted to Humanoid motion");
                string controllerPath=Folder+"/MixamoRunPreview.controller";
                var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
                if(controller==null) controller=AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                var machine=controller.layers[0].stateMachine;
                var state=machine.states.Select(s=>s.state).FirstOrDefault(s=>s.name=="Run");
                if(state==null) state=machine.AddState("Run");
                state.motion=run; machine.defaultState=state;
                animator.runtimeAnimatorController=controller;
                PrefabUtility.SaveAsPrefabAsset(instance,Folder+"/VeilWalker_Mixamo_RunPreview.prefab");
                ValidateRetarget(instance,animator,run);
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
            AssetDatabase.SaveAssets();
            Debug.Log("MIXAMO HUMANOID READY: new T-pose model and Avatar, eight Humanoid source clips, separate preview prefabs. Game scene and original procedural player untouched.");
        }
        static Avatar Configure(string path,Dictionary<string,string> map,bool animation)
        {
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(model==null) throw new InvalidOperationException("Missing model "+path);
            var transforms=model.GetComponentsInChildren<Transform>(true);
            var reference=animation?AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Player/Mixamo/VeilWalker_AnimationReference.fbx"):null;
            if(animation && reference==null) throw new InvalidOperationException("Generate the true source bind reference before configuring animation Avatars");
            var referenceBones=reference!=null?reference.GetComponentsInChildren<Transform>(true).ToDictionary(t=>t.name,t=>t):null;
            foreach(string bone in map.Values) if(!transforms.Any(t=>t.name==bone)) throw new InvalidOperationException("Missing mapped bone "+bone+" in "+path);
            var description=new HumanDescription {
                human=map.Select(p=>new HumanBone {humanName=p.Key,boneName=p.Value,limit=new HumanLimit {useDefaultValues=true}}).ToArray(),
                skeleton=transforms.Select(t=> {
                    // Animation-only FBXs expose frame zero as the model pose.
                    // That running/swinging pose must NOT be the Avatar's T-pose.
                    Transform rest=t;
                    if(referenceBones!=null)
                    {
                        if(t==model.transform) rest=reference.transform;
                        else if(referenceBones.TryGetValue(t.name,out var matched)) rest=matched;
                    }
                    return new SkeletonBone {name=t.name,position=rest.localPosition,rotation=rest.localRotation,scale=rest.localScale};
                }).ToArray(),
                upperArmTwist=.5f,lowerArmTwist=.5f,upperLegTwist=.5f,lowerLegTwist=.5f,armStretch=.05f,legStretch=.05f,feetSpacing=0,hasTranslationDoF=false };
            var importer=(ModelImporter)AssetImporter.GetAtPath(path);
            importer.animationType=ModelImporterAnimationType.Human;
            importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
            importer.humanDescription=description;
            importer.importAnimation=animation;
            importer.importCameras=false; importer.importLights=false; importer.optimizeGameObjects=false;
            if(animation)
            {
                var clips=importer.clipAnimations.Length>0?importer.clipAnimations:importer.defaultClipAnimations;
                bool loop=path.Contains("Run") || path.Contains("Strafe");
                foreach(var clip in clips) { clip.loopTime=loop; clip.loopPose=loop; }
                importer.clipAnimations=clips;
            }
            importer.SaveAndReimport();
            var avatar=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
            if(avatar==null || !avatar.isValid || !avatar.isHuman) throw new InvalidOperationException("Invalid Humanoid Avatar: "+path);
            Debug.Log("HUMANOID AVATAR PASS "+path+" valid="+avatar.isValid+" human="+avatar.isHuman);
            return avatar;
        }
        static void ValidateRetarget(GameObject model,Animator animator,AnimationClip run)
        {
            animator.runtimeAnimatorController=null;
            var graph=PlayableGraph.Create("Humanoid retarget verification"); graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            try
            {
                var clip=AnimationClipPlayable.Create(graph,run); clip.SetSpeed(0); clip.SetApplyFootIK(false);
                var output=AnimationPlayableOutput.Create(graph,"Retarget",animator); output.SetSourcePlayable(clip); graph.Play();
                var foot=animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                Vector3 initial=Vector3.zero; float travel=0;
                for(int i=0;i<=30;i++)
                {
                    clip.SetTime(run.length*i/30f); graph.Evaluate(0);
                    if(i==0) initial=foot.position;
                    travel=Mathf.Max(travel,Vector3.Distance(initial,foot.position));
                    foreach(var t in model.GetComponentsInChildren<Transform>()) if(!float.IsFinite(t.position.sqrMagnitude)) throw new InvalidOperationException("Non-finite Humanoid pose");
                }
                if(travel<.1f) throw new InvalidOperationException("Humanoid retarget produced no leg motion");
                Debug.Log($"HUMANOID RETARGET PASS: Mixamo run evaluated across full cycle on the new Avatar; foot travel {travel:F3}m.");
                RenderRetarget(model,animator,graph,clip,run);
            }
            finally { graph.Destroy(); }
        }
        static void RenderRetarget(GameObject model,Animator animator,PlayableGraph graph,AnimationClipPlayable player,AnimationClip run)
        {
            Vector3 previous=model.transform.position;
            var skin=model.GetComponentInChildren<SkinnedMeshRenderer>();
            var baked=new Mesh();
            var body=new GameObject("HumanoidAuditMesh"); body.transform.SetParent(skin.transform,false);
            body.AddComponent<MeshFilter>().sharedMesh=baked;
            body.AddComponent<MeshRenderer>().sharedMaterials=skin.sharedMaterials;
            skin.enabled=false;
            var cameraObject=new GameObject("HumanoidAuditCamera"); var camera=cameraObject.AddComponent<Camera>();
            camera.enabled=false; camera.orthographic=true; camera.orthographicSize=1.4f;
            camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.23f,.25f,.28f);
            var lightObject=new GameObject("HumanoidAuditLight"); var light=lightObject.AddComponent<Light>();
            light.type=LightType.Directional; light.intensity=2; lightObject.transform.rotation=Quaternion.Euler(35,-25,0);
            var target=new RenderTexture(420,520,24); var atlas=new Texture2D(420*4,520,TextureFormat.RGB24,false);
            var old=RenderTexture.active;
            try
            {
                model.transform.position=new Vector3(10000,0,0);
                camera.transform.position=model.transform.position+new Vector3(2.2f,2,4);
                camera.transform.LookAt(model.transform.position+Vector3.up);
                camera.targetTexture=target;
                for(int i=0;i<4;i++)
                {
                    player.SetTime(run.length*i/4f); graph.Evaluate(0); skin.BakeMesh(baked,true);
                    camera.Render(); RenderTexture.active=target;
                    atlas.ReadPixels(new Rect(0,0,420,520),i*420,0);
                }
                atlas.Apply(); File.WriteAllBytes("ArtSource/Previews/VeilWalker_Humanoid_Run.png",atlas.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active=old; camera.targetTexture=null; target.Release();
                model.transform.position=previous; skin.enabled=true;
                UnityEngine.Object.DestroyImmediate(body); UnityEngine.Object.DestroyImmediate(baked);
                UnityEngine.Object.DestroyImmediate(cameraObject); UnityEngine.Object.DestroyImmediate(lightObject);
                UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(atlas);
            }
        }
    }
}
