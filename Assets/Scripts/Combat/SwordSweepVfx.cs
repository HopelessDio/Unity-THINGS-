using UnityEngine;
using UnityEngine.Rendering;

namespace Survivor.Combat
{
    /// <summary>Pooled, world-space slash crescents. Visual only: no colliders or damage.</summary>
    public sealed class SwordSweepVfx : MonoBehaviour
    {
        const int PoolSize=4, Segments=64;
        const float FadeTime=.22f;
        static readonly int Reveal=Shader.PropertyToID("_Reveal"), Fade=Shader.PropertyToID("_Fade");
        sealed class Sweep
        {
            public GameObject obj;
            public MeshFilter filter;
            public MeshRenderer renderer;
            public bool driving;
            public float progress, age;
        }
        private GameObject root;
        private Mesh rightMesh, leftMesh;
        private Sweep[] pool;
        private ParticleSystem mist;
        private MaterialPropertyBlock properties;
        private int next;
        public bool IsReady => root!=null;
        public int ActiveSweeps
        {
            get { int count=0; if(pool!=null) foreach(var s in pool) if(s.obj.activeSelf) count++; return count; }
        }
        public void Initialize()
        {
            if(IsReady) return;
            var material=Resources.Load<Material>("SwordSweep");
            if(material==null) { Debug.LogError("SwordSweep material missing. Run Survivor/Prepare Sword Sweep Effects.",this); return; }
            if(properties==null) properties=new MaterialPropertyBlock();
            root=new GameObject("SwordSweepEffects");
            if(!Application.isPlaying) root.hideFlags=HideFlags.DontSaveInEditor|HideFlags.DontSaveInBuild;
            root.layer=gameObject.layer;
            rightMesh=CreateMesh(false); leftMesh=CreateMesh(true);
            pool=new Sweep[PoolSize];
            for(int i=0;i<pool.Length;i++)
            {
                var obj=new GameObject("CloudySwordArc_"+i); obj.transform.SetParent(root.transform,false); obj.layer=gameObject.layer;
                var filter=obj.AddComponent<MeshFilter>(); filter.sharedMesh=rightMesh;
                var renderer=obj.AddComponent<MeshRenderer>(); renderer.sharedMaterial=material;
                renderer.shadowCastingMode=ShadowCastingMode.Off; renderer.receiveShadows=false;
                pool[i]=new Sweep {obj=obj,filter=filter,renderer=renderer}; obj.SetActive(false);
            }
            var ash=Resources.Load<Material>("DashAsh");
            if(ash==null) return;
            var particles=new GameObject("SwordCloudWisps"); particles.transform.SetParent(root.transform,false); particles.layer=gameObject.layer;
            mist=particles.AddComponent<ParticleSystem>(); mist.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=mist.main; main.playOnAwake=false; main.loop=false; main.simulationSpace=ParticleSystemSimulationSpace.World;
            main.maxParticles=160; main.startLifetime=.27f; main.startSize=.24f; main.startSpeed=0;
            var emission=mist.emission; emission.enabled=false;
            var shape=mist.shape; shape.enabled=false;
            var fade=mist.colorOverLifetime; fade.enabled=true;
            var gradient=new Gradient(); gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(new Color(.78f,.81f,1),1)},
                new[]{new GradientAlphaKey(.45f,0),new GradientAlphaKey(0,1)}); fade.color=gradient;
            var size=mist.sizeOverLifetime; size.enabled=true; size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,.7f,1,1.7f));
            var rendererParticles=mist.GetComponent<ParticleSystemRenderer>(); rendererParticles.sharedMaterial=ash;
            rendererParticles.shadowCastingMode=ShadowCastingMode.Off; rendererParticles.receiveShadows=false;
        }
        static Mesh CreateMesh(bool reverse)
        {
            var vertices=new Vector3[(Segments+1)*2]; var uv=new Vector2[vertices.Length]; var triangles=new int[Segments*6];
            for(int i=0;i<=Segments;i++)
            {
                float u=i/(float)Segments, angle=Mathf.Lerp(-65,65,reverse?1-u:u)*Mathf.Deg2Rad;
                var direction=new Vector3(Mathf.Sin(angle),0,Mathf.Cos(angle));
                vertices[i*2]=direction*.36f; vertices[i*2+1]=direction;
                uv[i*2]=new Vector2(u,0); uv[i*2+1]=new Vector2(u,1);
                if(i==Segments) continue;
                int t=i*6,v=i*2;
                triangles[t]=v; triangles[t+1]=v+1; triangles[t+2]=v+2;
                triangles[t+3]=v+1; triangles[t+4]=v+3; triangles[t+5]=v+2;
            }
            var mesh=new Mesh {name=reverse?"Left cloudy crescent":"Right cloudy crescent"};
            mesh.vertices=vertices; mesh.uv=uv; mesh.triangles=triangles; mesh.RecalculateBounds();
            return mesh;
        }
        public int BeginSweep(int strike,Vector3 origin,Vector3 direction,float radius)
        {
            Initialize(); if(!IsReady) return -1;
            int index=next; next=(next+1)%PoolSize; var s=pool[index];
            direction.y=0; if(direction.sqrMagnitude<.001f) direction=Vector3.forward;
            s.obj.transform.SetPositionAndRotation(origin+Vector3.up*.08f,Quaternion.LookRotation(direction)*Quaternion.Euler(0,0,strike==0?-7:7));
            s.obj.transform.localScale=Vector3.one*radius;
            s.filter.sharedMesh=strike==0?rightMesh:leftMesh;
            s.progress=0; s.age=0; s.driving=true; s.obj.SetActive(true); Apply(s);
            return index;
        }
        public void SetProgress(int index,float progress)
        {
            if(!IsReady || index<0 || index>=PoolSize) return;
            var s=pool[index]; float old=s.progress; s.progress=Mathf.Clamp01(progress);
            if(mist!=null && s.progress>old)
            {
                mist.Play();
                int count=Mathf.Clamp(Mathf.CeilToInt((s.progress-old)*22),1,8);
                for(int i=0;i<count;i++)
                {
                    float u=Mathf.Lerp(old,s.progress,(i+.5f)/count);
                    bool reverse=s.filter.sharedMesh==leftMesh;
                    float angle=Mathf.Lerp(-65,65,reverse?1-u:u)*Mathf.Deg2Rad;
                    Vector3 radial=new Vector3(Mathf.Sin(angle),0,Mathf.Cos(angle));
                    var emit=new ParticleSystem.EmitParams {
                        position=s.obj.transform.TransformPoint(radial*Random.Range(.68f,.96f)),
                        velocity=s.obj.transform.TransformDirection(radial)*Random.Range(.25f,.7f)+Vector3.up*.22f,
                        startSize=Random.Range(.18f,.37f),startLifetime=Random.Range(.18f,.32f),
                        startColor=new Color(.95f,.97f,1,.42f) };
                    mist.Emit(emit,1);
                }
            }
            Apply(s);
        }
        public void Release(int index,bool cancelled=false)
        {
            if(!IsReady || index<0 || index>=PoolSize) return;
            var s=pool[index]; s.driving=false;
            if(cancelled) s.age=FadeTime*.65f;
        }
        private void Update() => Advance(Time.deltaTime);
        public void Advance(float dt)
        {
            if(!IsReady) return;
            foreach(var s in pool)
            {
                if(!s.obj.activeSelf) continue;
                if(!s.driving) s.age+=dt;
                if(s.age>=FadeTime) s.obj.SetActive(false);
                else Apply(s);
            }
        }
        void Apply(Sweep s)
        {
            properties.SetFloat(Reveal,s.progress);
            properties.SetFloat(Fade,1-Mathf.SmoothStep(0,1,s.age/FadeTime));
            s.renderer.SetPropertyBlock(properties);
        }
        private void OnDisable() => ClearEffects();
        private void OnDestroy() => ClearEffects();
        public void ClearEffects()
        {
            if(root!=null) { root.SetActive(false); Dispose(root); }
            if(rightMesh!=null) Dispose(rightMesh); if(leftMesh!=null) Dispose(leftMesh);
            root=null; pool=null; mist=null;
            rightMesh=null; leftMesh=null;
        }
        static void Dispose(Object obj) { if(Application.isPlaying) Destroy(obj); else DestroyImmediate(obj); }
    }
}
