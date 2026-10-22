using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Survivor.CameraSystem;
using Survivor.Combat;
using Survivor.Enemies;
using Survivor.Environment;
using Survivor.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Survivor.EditorTools
{
    [InitializeOnLoad]
    public static class LevelOneSceneInstaller
    {
        const string Pending="Assets/Editor/LevelOneInstall.pending";
        const string Folder="Assets/Models/Levels/Level01/";
        const string Model=Folder+"Level01_CrimsonVillage.fbx";
        const string MaterialFolder="Assets/Materials/Level01";
        const string RootName="Level01_CrimsonVillage";
        static double nextStatus;
        [Serializable] public class Surface { public string id,materialId,source; public Color color,emission; public float strength; public bool walkable; }
        [Serializable] public class Box { public string name; public Vector3 center,size; }
        [Serializable] public class Lamp { public Vector3 position; public Color color; public float power; }
        [Serializable] public class Layout
        {
            public Surface[] materials; public Box[] floors,obstacles; public Lamp[] lights;
            public Vector3 cameraPosition,cameraTarget,cameraUp,spawn; public float referenceWidth; public Vector3[] route;
        }
        static LevelOneSceneInstaller() { EditorApplication.update+=Tick; }
        static void Tick()
        {
            if(!File.Exists(Pending)) return;
            if(EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                if(EditorApplication.timeSinceStartup>nextStatus)
                {
                    nextStatus=EditorApplication.timeSinceStartup+20;
                    Debug.Log($"LEVEL01 WAITING: compiling={EditorApplication.isCompiling}, updating={EditorApplication.isUpdating}, playing={EditorApplication.isPlaying}");
                }
                return;
            }
            try { Install(); }
            catch(Exception exception) { Debug.LogException(exception); }
            finally { AssetDatabase.DeleteAsset(Pending); if(File.Exists(Pending)) File.Delete(Pending); }
        }
        [MenuItem("Survivor/Install Level 1 Crimson Village")]
        public static void Install()
        {
            Scene scene=SceneManager.GetActiveScene();
            if(scene.path!="Assets/Scenes/Game.unity") throw new InvalidOperationException("Open Game scene to install Level 1.");
            if(EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before installing Level 1.");
            var player=UnityEngine.Object.FindAnyObjectByType<PlayerMovement>();
            var camera=Camera.main;
            if(player==null || camera==null) throw new InvalidOperationException("Player/Main Camera missing.");
            if(!AssetDatabase.IsValidFolder("Assets/Scenes/Backups")) AssetDatabase.CreateFolder("Assets/Scenes","Backups");
            string backup="Assets/Scenes/Backups/Game_Before_Level01_20261015.unity";
            if(!File.Exists(backup)) EditorSceneManager.SaveScene(scene,backup,true);
            AssetDatabase.ImportAsset(Model,ImportAssetOptions.ForceSynchronousImport);
            var importer=(ModelImporter)AssetImporter.GetAtPath(Model);
            importer.materialImportMode=ModelImporterMaterialImportMode.None; importer.importAnimation=false; importer.importLights=false; importer.importCameras=false;
            importer.isReadable=true; importer.meshCompression=ModelImporterMeshCompression.Off;
            importer.SaveAndReimport();
            var layout=JsonUtility.FromJson<Layout>(File.ReadAllText(Folder+"Level01_Layout.json"));
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Model);
            if(asset==null || layout==null) throw new InvalidOperationException("Level 1 export is missing.");
            GameObject previous=scene.GetRootGameObjects().FirstOrDefault(obj=>obj.name==RootName);
            if(previous!=null) UnityEngine.Object.DestroyImmediate(previous);
            var root=new GameObject(RootName); SceneManager.MoveGameObjectToScene(root,scene);
            var model=(GameObject)PrefabUtility.InstantiatePrefab(asset,scene);
            PrefabUtility.UnpackPrefabInstance(model,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            model.transform.SetParent(root.transform,false); model.name="Village_RenderGeometry";
            Transform origin=Find(model,"L1_Origin"), bx=Find(model,"L1_BasisX"), by=Find(model,"L1_BasisY"), bz=Find(model,"L1_BasisZ");
            Vector3 ax=bx.position-origin.position,ay=by.position-origin.position,az=bz.position-origin.position;
            if(Mathf.Abs(ax.magnitude-1)>.01f || Mathf.Abs(ay.magnitude-1)>.01f || Mathf.Abs(az.magnitude-1)>.01f)
                throw new InvalidOperationException("FBX metre scale is incorrect.");
            Vector3 Convert(Vector3 v)=>origin.position+ax*v.x+ay*v.y+az*v.z;
            Vector3 Direction(Vector3 v)=>ax*v.x+ay*v.y+az*v.z;
            Debug.Log($"LEVEL01 FBX BASIS: X={ax}, Y={ay}, Z={az}");
            if(!AssetDatabase.IsValidFolder(MaterialFolder)) AssetDatabase.CreateFolder("Assets/Materials","Level01");
            var surfaces=layout.materials.ToDictionary(s=>s.id);
            int rendererCount=0;
            foreach(var renderer in model.GetComponentsInChildren<MeshRenderer>())
            {
                if(!surfaces.TryGetValue(renderer.name,out var surface)) throw new InvalidOperationException("Unknown render batch: "+renderer.name);
                string path=MaterialFolder+"/"+(string.IsNullOrEmpty(surface.materialId)?surface.id:surface.materialId)+".mat";
                Material material=AssetDatabase.LoadAssetAtPath<Material>(path);
                bool foliage=surface.source.StartsWith("Crimson foliage",StringComparison.Ordinal);
                bool selfLit=foliage || surface.strength>0;
                Shader shader=Shader.Find(selfLit?"Universal Render Pipeline/Unlit":"Universal Render Pipeline/Lit");
                if(material==null) { material=new Material(shader); AssetDatabase.CreateAsset(material,path); }
                else material.shader=shader;
                // Blender node colours are linear; Unity's colour properties
                // are authored in sRGB and converted for the lighting shader.
                material.SetColor("_BaseColor",surface.strength>0?surface.emission*2:
                    foliage?surface.color.gamma*.65f:surface.color.gamma);
                if(material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness",.12f);
                material.SetFloat("_Cull",0); material.doubleSidedGI=true;
                if(material.HasProperty("_EmissionColor") && surface.strength>0)
                {
                    material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor",surface.emission.gamma*surface.strength);
                }
                else if(material.HasProperty("_EmissionColor")) { material.DisableKeyword("_EMISSION"); material.SetColor("_EmissionColor",Color.black); }
                renderer.sharedMaterial=material; EditorUtility.SetDirty(material);
                if(surface.walkable)
                    renderer.gameObject.AddComponent<MeshCollider>().sharedMesh=renderer.GetComponent<MeshFilter>().sharedMesh;
                rendererCount++;
            }
            var collisions=new GameObject("Gameplay_Collisions").transform; collisions.SetParent(root.transform,false);
            int obstacleLayer=LayerMask.NameToLayer("ArenaObstacle");
            foreach(var floor in layout.floors) MakeBox(floor,collisions,0,Convert,Direction);
            foreach(var obstacle in layout.obstacles) MakeBox(obstacle,collisions,obstacleLayer,Convert,Direction);
            var kill=new GameObject("Level01_VoidKillZone"); kill.transform.SetParent(root.transform,false);
            kill.transform.position=Convert(new Vector3(5,10,-14));
            var killCollider=kill.AddComponent<BoxCollider>(); killCollider.size=new Vector3(240,5,280); killCollider.isTrigger=true;
            kill.AddComponent<VoidKillZone>();

            // Preserve, rather than delete, the old arena and its random spawning.
            foreach(var old in scene.GetRootGameObjects())
                if(new[]{"Environment","ArenaSpawnArea","ArenaBoundaries","ArenaGeometry","EnemySpawner","AshenCourtyard","Directional Light","Global Volume"}.Contains(old.name)) old.SetActive(false);
            foreach(var spawner in UnityEngine.Object.FindObjectsByType<EnemySpawner>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                if(spawner.gameObject.scene==scene) spawner.enabled=false;
            var body=player.GetComponent<Rigidbody>();
            body.position=Convert(layout.spawn); body.rotation=Quaternion.LookRotation(Direction(new Vector3(0,1,0)),Vector3.up);
            player.transform.SetPositionAndRotation(body.position,body.rotation);
            player.GetComponent<PlayerModelVisual>().RebuildVisual();
            CreateLighting(root.transform,layout,Convert,Direction);
            var oldFollow=camera.GetComponent<CameraFollow>(); if(oldFollow!=null) oldFollow.enabled=false;
            camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.025f,.002f,.008f);
            camera.nearClipPlane=.1f; camera.farClipPlane=1000; camera.allowHDR=true;
            var extra=camera.GetUniversalAdditionalCameraData(); extra.renderPostProcessing=true;
            extra.renderShadows=true;
            extra.antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            extra.antialiasingQuality=AntialiasingQuality.High;
            var backdrop=CreateBackdrop(camera);
            var follow=camera.GetComponent<LevelOneCamera>(); if(follow==null) follow=camera.gameObject.AddComponent<LevelOneCamera>();
            follow.enabled=true;
            follow.Configure(player.transform,backdrop,Convert(layout.cameraPosition),
                Quaternion.LookRotation(Direction(layout.cameraTarget-layout.cameraPosition),Direction(layout.cameraUp)),layout.referenceWidth);
            player.ConfigureMovementView(camera.transform);
            var home=new GameObject("Level01_HomeEntrance"); home.transform.SetParent(root.transform,false);
            home.transform.position=Convert(new Vector3(20,53,0));
            Physics.SyncTransforms();
            Validate(root,player,camera,layout,Convert,rendererCount);
            EditorUtility.SetDirty(player); EditorUtility.SetDirty(camera); EditorUtility.SetDirty(follow);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Debug.Log($"LEVEL01 INSTALLED: {rendererCount} material batches; {layout.floors.Length} floor boxes; {layout.obstacles.Length} obstacles; shifted-right follow camera; existing Veil Walker, sword, dash and UI retained; old arena backed up and disabled.");
            RenderPreview(camera);
        }
        static Transform Find(GameObject obj,string name)=>obj.GetComponentsInChildren<Transform>().First(t=>t.name==name);
        static void MakeBox(Box box,Transform parent,int layer,Func<Vector3,Vector3> convert,Func<Vector3,Vector3> direction)
        {
            var obj=new GameObject(box.name); obj.transform.SetParent(parent,false); obj.layer=layer;
            obj.transform.position=convert(box.center);
            Vector3 x=direction(new Vector3(box.size.x,0,0)),y=direction(new Vector3(0,box.size.y,0)),z=direction(new Vector3(0,0,box.size.z));
            var collider=obj.AddComponent<BoxCollider>(); collider.size=Abs(x)+Abs(y)+Abs(z);
        }
        static Vector3 Abs(Vector3 v)=>new Vector3(Mathf.Abs(v.x),Mathf.Abs(v.y),Mathf.Abs(v.z));
        static Transform CreateBackdrop(Camera camera)
        {
            Transform old=camera.transform.Find("Level01_PictureBackdrop"); if(old!=null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var obj=GameObject.CreatePrimitive(PrimitiveType.Quad); obj.name="Level01_PictureBackdrop";
            obj.transform.SetParent(camera.transform,false); obj.layer=2; UnityEngine.Object.DestroyImmediate(obj.GetComponent<Collider>());
            string path=MaterialFolder+"/VillageBackdrop.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null) { material=new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(material,path); }
            material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Level01/CrimsonVillage_Backdrop.png"));
            material.SetColor("_BaseColor",Color.white); material.SetFloat("_Cull",0);
            var renderer=obj.GetComponent<MeshRenderer>(); renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off; renderer.receiveShadows=false; EditorUtility.SetDirty(material);
            return obj.transform;
        }
        static void CreateLighting(Transform root,Layout layout,Func<Vector3,Vector3> convert,Func<Vector3,Vector3> direction)
        {
            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.3f,.33f,.42f);
            RenderSettings.ambientEquatorColor=new Color(.2f,.2f,.25f);
            RenderSettings.ambientGroundColor=new Color(.08f,.035f,.04f); RenderSettings.ambientIntensity=1.35f;
            RenderSettings.fog=false; RenderSettings.skybox=null;
            var obj=new GameObject("Level01_CoolSkyLight"); obj.transform.SetParent(root,false);
            obj.transform.rotation=Quaternion.LookRotation(direction(new Vector3(29,16,-65)),Vector3.up);
            var light=obj.AddComponent<Light>(); light.type=LightType.Directional; light.color=new Color(.8f,.82f,.9f);
            light.intensity=1.3f; light.shadows=LightShadows.Soft; light.shadowStrength=.75f; RenderSettings.sun=light;
            foreach(var lamp in layout.lights)
            {
                var point=new GameObject("Level01_AmberLantern"); point.transform.SetParent(root,false); point.transform.position=convert(lamp.position);
                var component=point.AddComponent<Light>(); component.type=LightType.Point; component.color=lamp.color;
                component.intensity=5; component.range=12; component.shadows=LightShadows.None;
            }
            var volumeObj=new GameObject("Level01_PostProcessing"); volumeObj.transform.SetParent(root,false);
            var volume=volumeObj.AddComponent<Volume>(); volume.isGlobal=true; volume.priority=20;
            string profilePath=MaterialFolder+"/Level01Look.asset";
            var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if(profile==null) { profile=ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(profile,profilePath); }
            if(!profile.TryGet<Tonemapping>(out var tone)) { tone=profile.Add<Tonemapping>(); AssetDatabase.AddObjectToAsset(tone,profile); }
            tone.mode.Override(TonemappingMode.ACES);
            if(!profile.TryGet<ColorAdjustments>(out var color)) { color=profile.Add<ColorAdjustments>(); AssetDatabase.AddObjectToAsset(color,profile); }
            color.postExposure.Override(.45f); color.contrast.Override(7);
            if(!profile.TryGet<Bloom>(out var bloom)) { bloom=profile.Add<Bloom>(); AssetDatabase.AddObjectToAsset(bloom,profile); }
            bloom.intensity.Override(.18f); bloom.threshold.Override(1.2f);
            volume.sharedProfile=profile; EditorUtility.SetDirty(profile);
        }
        static void Validate(GameObject root,PlayerMovement player,Camera camera,Layout layout,Func<Vector3,Vector3> convert,int rendererCount)
        {
            if(rendererCount<20) throw new InvalidOperationException("Level render batches missing.");
            foreach(var point in layout.route)
            {
                Vector3 position=convert(point);
                if(!Physics.RaycastAll(position+Vector3.up*4,Vector3.down,5,1,QueryTriggerInteraction.Ignore)
                    .Any(hit=>hit.collider.transform.IsChildOf(root.transform) && hit.normal.y>.9f))
                    throw new InvalidOperationException("Level route has missing floor at "+position);
            }
            int obstacles=LayerMask.GetMask("ArenaObstacle"); Vector3 spawn=player.transform.position;
            if(Physics.CheckCapsule(spawn+Vector3.down*.48f,spawn+Vector3.up*.48f,.46f,obstacles,QueryTriggerInteraction.Ignore))
                throw new InvalidOperationException("Player spawn overlaps an obstacle.");
            var visual=player.transform.Find("VeilWalkerVisual");
            if(visual==null || visual.GetComponent<GreatSwordLocomotion>()?.IsReady!=true || visual.GetComponent<PlayerSwordAttack>()==null || visual.GetComponent<PlayerDashAfterimages>()==null)
                throw new InvalidOperationException("Existing player combat/animation/dash missing.");
            if(camera.GetComponent<LevelOneCamera>()==null || camera.transform.Find("Level01_PictureBackdrop")==null)
                throw new InvalidOperationException("Shifted level camera/background missing.");
            Debug.Log($"LEVEL01 VALIDATION PASS: 11 route floor samples, clear human-scale spawn, {rendererCount} local render batches, live Humanoid movement/combat/dash, camera and background.");
        }
        public static void RenderPreview(Camera camera)
        {
            var target=new RenderTexture(1280,720,24); var pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);
            var oldTarget=camera.targetTexture; var oldActive=RenderTexture.active;
            try
            {
                camera.targetTexture=target; camera.aspect=1280f/720; camera.GetComponent<LevelOneCamera>()?.RefreshFraming(); camera.Render(); RenderTexture.active=target;
                pixels.ReadPixels(new Rect(0,0,1280,720),0,0); pixels.Apply();
                File.WriteAllBytes("ArtSource/Previews/Level01_Unity_Installed.png",pixels.EncodeToPNG());
                Debug.Log("LEVEL01 UNITY PREVIEW SAVED");
            }
            finally
            {
                camera.targetTexture=oldTarget; camera.ResetAspect(); camera.GetComponent<LevelOneCamera>()?.RefreshFraming(); RenderTexture.active=oldActive; target.Release();
                UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(pixels);
            }
        }
    }
}
