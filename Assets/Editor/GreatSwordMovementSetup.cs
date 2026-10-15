using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Survivor.Player;
using Survivor.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Survivor.EditorTools
{
    [InitializeOnLoad]
    public static class GreatSwordMovementSetup
    {
        const string Pending="Assets/Editor/GreatSwordMovement.pending";
        const string Pack="Assets/Great Sword Pack/";
        const string Reference="Assets/Models/Player/Mixamo/GreatSword_Reference.fbx";
        static double nextStatus;
        static GreatSwordMovementSetup() { EditorApplication.update+=Tick; }
        static void Tick()
        {
            if(!File.Exists(Pending)) return;
            if(EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlaying)
            {
                if(EditorApplication.timeSinceStartup>nextStatus)
                {
                    nextStatus=EditorApplication.timeSinceStartup+15;
                    Debug.Log($"GREAT SWORD waiting: compiling={EditorApplication.isCompiling}, updating={EditorApplication.isUpdating}, playing={EditorApplication.isPlaying}");
                }
                return;
            }
            try { Install(); } catch(Exception e) { Debug.LogException(e); }
            finally { AssetDatabase.DeleteAsset(Pending); }
        }
        [MenuItem("Survivor/Install Great Sword Movement Only")]
        public static void Install()
        {
            AssetDatabase.ImportAsset(Reference,ImportAssetOptions.ForceSynchronousImport);
            var reference=AssetDatabase.LoadAssetAtPath<GameObject>(Reference);
            if(reference==null) throw new InvalidOperationException("Generate GreatSword_Reference.fbx with the locomotion audit script first");
            var bank=AssetDatabase.LoadAssetAtPath<GreatSwordMovementSet>("Assets/Resources/GreatSwordMovement.asset");
            if(bank==null) { bank=ScriptableObject.CreateInstance<GreatSwordMovementSet>(); AssetDatabase.CreateAsset(bank,"Assets/Resources/GreatSwordMovement.asset"); }
            bank.idle=Import("great sword idle.fbx",reference);
            bank.forward=Import("great sword run (2).fbx",reference);
            bank.backward=Import("great sword run.fbx",reference);
            bank.left=Import("great sword strafe (3).fbx",reference);
            bank.right=Import("great sword strafe (4).fbx",reference);
            bank.strideDistances=new Vector4(2.6034f,1.9208f,1.3494f,1.9855f);
            bank.stanceOffsets=new Vector4(.4333333f,.55f,.9f,.9833333f);
            EditorUtility.SetDirty(bank); AssetDatabase.SaveAssets();
            var movement=UnityEngine.Object.FindAnyObjectByType<PlayerMovement>();
            if(movement==null || movement.gameObject.scene.name!="Game") throw new InvalidOperationException("Open Game scene before installing on the player");
            var model=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Player/VeilWalker/Mixamo/VeilWalker_Mixamo.prefab");
            var sword=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Player/VeilWalker/VeilWalker_Sword.prefab");
            var visual=movement.GetComponent<PlayerModelVisual>();
            if(model==null || sword==null || visual==null) throw new InvalidOperationException("Player/model/sword setup missing");
            visual.Configure(model,sword);
            Transform instance=movement.transform.Find("VeilWalkerVisual");
            Validate(instance,movement.transform);
            SwordComboValidation.Validate(movement.gameObject,instance);
            RenderPreview(instance,movement.transform);
            instance.GetComponent<GreatSwordLocomotion>().Sample(0,0);
            EditorUtility.SetDirty(visual); EditorSceneManager.MarkSceneDirty(movement.gameObject.scene);
            EditorSceneManager.SaveScene(movement.gameObject.scene); AssetDatabase.SaveAssets();
            Debug.Log("GREAT SWORD MOVEMENT INSTALLED: T-rig player uses idle, forward, backward, left and right only. Mouse aim, Rigidbody movement, two-hit sword attack, slash VFX and dash retained. Other pack actions untouched.");
        }
        static AnimationClip Import(string name,GameObject reference)
        {
            string path=Pack+name; AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var map=new Dictionary<string,string> { ["Hips"]="Hips",["Spine"]="Spine",["Chest"]="Spine1",["UpperChest"]="Spine2",["Neck"]="Neck",["Head"]="Head" };
            foreach(string side in new[]{"Left","Right"})
                foreach(var pair in new[]{("Shoulder","Shoulder"),("UpperArm","Arm"),("LowerArm","ForeArm"),("Hand","Hand"),("UpperLeg","UpLeg"),("LowerLeg","Leg"),("Foot","Foot"),("Toes","ToeBase")})
                    map[side+pair.Item1]=side+pair.Item2;
            var rest=reference.GetComponentsInChildren<Transform>(true).ToDictionary(t=>t.name,t=>t);
            var transforms=source.GetComponentsInChildren<Transform>(true);
            Debug.Log($"GREAT SWORD REFERENCE {name}: source root {source.transform.localRotation.eulerAngles}/{source.transform.localScale}, reference {reference.transform.localRotation.eulerAngles}/{reference.transform.localScale}");
            var human=map.Select(p=>new HumanBone {humanName=p.Key,boneName="mixamorig:"+p.Value,limit=new HumanLimit {useDefaultValues=true}}).ToArray();
            foreach(var bone in human) if(!transforms.Any(t=>t.name==bone.boneName)) throw new InvalidOperationException("Missing Mixamo bone "+bone.boneName);
            var description=new HumanDescription {
                human=human,skeleton=transforms.Select(t=> {
                    Transform bind=t==source.transform?reference.transform:rest.TryGetValue(t.name,out var r)?r:t;
                    return new SkeletonBone {name=t.name,position=bind.localPosition,rotation=bind.localRotation,scale=bind.localScale};
                }).ToArray(), upperArmTwist=.5f,lowerArmTwist=.5f,upperLegTwist=.5f,lowerLegTwist=.5f,armStretch=.05f,legStretch=.05f };
            var importer=(ModelImporter)AssetImporter.GetAtPath(path);
            importer.animationType=ModelImporterAnimationType.Human; importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
            importer.humanDescription=description; importer.importAnimation=true; importer.optimizeGameObjects=false;
            importer.importCameras=false; importer.importLights=false;
            var clips=importer.clipAnimations.Length>0?importer.clipAnimations:importer.defaultClipAnimations;
            foreach(var clip in clips)
            {
                clip.loopTime=true; clip.loopPose=true;
                clip.lockRootPositionXZ=false; clip.lockRootHeightY=true; clip.lockRootRotation=true;
                clip.keepOriginalOrientation=true;
                clip.keepOriginalPositionY=false; clip.heightFromFeet=true;
            }
            importer.clipAnimations=clips; importer.SaveAndReimport();
            var avatar=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
            var motion=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview"));
            if(avatar==null || !avatar.isHuman || !avatar.isValid || !motion.isHumanMotion) throw new InvalidOperationException("Invalid movement Avatar/clip: "+name);
            Debug.Log("GREAT SWORD CLIP PASS: "+name+" Humanoid, looping, extracted root motion discarded by gameplay Animator.");
            return motion;
        }
        public static void Validate(Transform visual,Transform player)
        {
            var driver=visual.GetComponent<GreatSwordLocomotion>(); var overlay=visual.GetComponent<VeilWalkerLocomotion>();
            if(driver==null || !driver.IsReady || overlay==null || !overlay.IsReady) throw new InvalidOperationException("Movement/weapon overlay not initialized");
            var animator=visual.GetComponent<Animator>();
            Vector3 root=player.position; Quaternion rotation=player.rotation;
            for(int clip=0;clip<5;clip++)
            {
                Vector3 initial=Vector3.zero; float footTravel=0;
                for(int i=0;i<=24;i++)
                {
                    driver.Sample(clip,i/24f);
                    Vector3 foot=animator.GetBoneTransform(HumanBodyBones.LeftFoot).position;
                    Vector3 otherFoot=animator.GetBoneTransform(HumanBodyBones.RightFoot).position;
                    Vector3 head=animator.GetBoneTransform(HumanBodyBones.Head).position;
                    var drape=visual.GetComponent<VeilWalkerCapeDrape>();
                    drape?.ApplyPose();
                    if(Vector3.Distance(head,animator.GetBoneTransform(HumanBodyBones.Head).position)>.0001f ||
                        Vector3.Distance(foot,animator.GetBoneTransform(HumanBodyBones.LeftFoot).position)>.0001f)
                        throw new InvalidOperationException("Cape correction changed the body pose");
                    if(drape!=null)
                    {
                        var chain=visual.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="mixamorig:CapeUpper" || t.name=="mixamorig:CapeMiddle" || t.name=="mixamorig:CapeLower").ToArray();
                        Vector3 upper=(chain[1].position-chain[0].position).normalized;
                        Vector3 lower=(chain[2].position-chain[1].position).normalized;
                        if(Vector3.Dot(upper,lower)<.99f || Vector3.Dot(upper,Vector3.down)<.8f)
                            throw new InvalidOperationException("Cape is not a straight downward drape");
                    }
                    if(head.y-(foot.y+otherFoot.y)*.5f<.8f)
                        throw new InvalidOperationException($"Retargeted character is not upright: clip={clip}, frame={i}");
                    if(i==0) initial=foot; footTravel=Mathf.Max(footTravel,Vector3.Distance(initial,foot));
                    if(player.position!=root || Quaternion.Angle(player.rotation,rotation)>.01f) throw new InvalidOperationException("Animation moved the physics root");
                    Vector3 offset=animator.GetBoneTransform(HumanBodyBones.Hips).position-root;
                    if(offset.magnitude>3) throw new InvalidOperationException($"In-place clip escaped capsule clip={clip} frame={i} hipsOffset={offset} visualScale={visual.localScale} visualPosition={visual.localPosition} humanScale={animator.humanScale}");
                    offset.y=0;
                    if(offset.magnitude>.5f) throw new InvalidOperationException("Animation root travel was not removed");
                }
                if(clip>0 && footTravel<.1f) throw new InvalidOperationException("Movement clip has no leg motion");
                Debug.Log($"GREAT SWORD RETARGET PASS {clip}: 25 poses, foot travel {footTravel:F3}m, physics root unchanged.");
            }
            foreach(var direction in new[]{Vector3.forward,Vector3.back,Vector3.left,Vector3.right,new Vector3(1,0,1),new Vector3(-1,0,-1)})
            {
                Vector4 weights=GreatSwordLocomotion.DirectionalWeights(direction);
                if(Mathf.Abs(weights.x+weights.y+weights.z+weights.w-1)>.001f) throw new InvalidOperationException("Directional weights are not normalized");
            }
            if(visual.GetComponent<PlayerSwordAttack>()==null || visual.GetComponent<PlayerDashAfterimages>()==null || PlayerSwordAttack.ComboLength!=2)
                throw new InvalidOperationException("Existing combat or dash effects missing");
            driver.Sample(0,0);
        }

        static void RenderPreview(Transform visual,Transform player)
        {
            Vector3 position=player.position; Quaternion rotation=player.rotation;
            var cameraObject=new GameObject("Movement audit camera");
            var lampObject=new GameObject("Movement audit light");
            var camera=cameraObject.AddComponent<Camera>(); camera.enabled=false;
            camera.orthographic=true; camera.orthographicSize=1.5f;
            camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.12f,.14f,.18f);
            var lamp=lampObject.AddComponent<Light>(); lamp.type=LightType.Directional; lamp.intensity=2.5f;
            lampObject.transform.rotation=Quaternion.Euler(30,200,0);
            var target=new RenderTexture(360,420,24);
            var pixels=new Texture2D(1080,2100,TextureFormat.RGB24,false);
            var previous=RenderTexture.active;
            var skin=visual.GetComponentInChildren<SkinnedMeshRenderer>();
            var proxy=new GameObject("Baked audit skin"); proxy.transform.SetParent(skin.transform,false);
            var baked=new Mesh(); proxy.AddComponent<MeshFilter>().sharedMesh=baked;
            proxy.AddComponent<MeshRenderer>().sharedMaterials=skin.sharedMaterials;
            bool enabled=skin.enabled;
            try
            {
                player.SetPositionAndRotation(new Vector3(15000,0,15000),Quaternion.identity);
                cameraObject.transform.position=player.position+new Vector3(3,1.8f,5);
                cameraObject.transform.LookAt(player.position);
                camera.targetTexture=target; skin.enabled=false;
                for(int clip=0;clip<5;clip++) for(int frame=0;frame<3;frame++)
                {
                    visual.GetComponent<GreatSwordLocomotion>().Sample(clip,frame/3f);
                    visual.GetComponent<VeilWalkerLocomotion>().ApplySupportingGrip();
                    visual.GetComponent<VeilWalkerCapeDrape>()?.ApplyPose();
                    skin.BakeMesh(baked,true);
                    camera.Render(); RenderTexture.active=target;
                    pixels.ReadPixels(new Rect(0,0,360,420),frame*360,(4-clip)*420);
                }
                pixels.Apply(); File.WriteAllBytes("ArtSource/Previews/GreatSword_Movement_Actual.png",pixels.EncodeToPNG());
            }
            finally
            {
                player.SetPositionAndRotation(position,rotation); skin.enabled=enabled;
                RenderTexture.active=previous; camera.targetTexture=null; target.Release();
                foreach(var item in new UnityEngine.Object[]{cameraObject,lampObject,proxy,baked,target,pixels}) UnityEngine.Object.DestroyImmediate(item);
                visual.GetComponent<GreatSwordLocomotion>().Sample(0,0);
                visual.GetComponent<VeilWalkerLocomotion>().ApplySupportingGrip();
                visual.GetComponent<VeilWalkerCapeDrape>()?.ApplyPose();
            }
        }
    }
}
