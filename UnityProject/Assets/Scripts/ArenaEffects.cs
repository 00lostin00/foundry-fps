using UnityEngine;

namespace Foundry
{
    public static class ArenaEffects
    {
        static AudioClip rifle,pistol;
        static AudioClip[] metalImpacts,genericImpacts,footsteps;
        static Material tracer,sparkMaterial,muzzleMaterial,smokeMaterial;
        static Texture2D ParticleTexture(string name)=>Resources.Load<Texture2D>("Foundry/Particles/"+name);
        static Material ParticleMaterial(string name,bool additive=false)
        {
            var shader=Shader.Find(additive?"Legacy Shaders/Particles/Additive":"Sprites/Default");
            if(shader==null) shader=Shader.Find("Sprites/Default");
            var material=new Material(shader);
            material.mainTexture=ParticleTexture(name);
            return material;
        }
        public static void Shot(Vector3 origin,Vector3 end,int weapon)
        {
            if(Application.isBatchMode)return;
            if(tracer==null) {
                tracer=Resources.Load<Material>("Foundry/FX/Tracer");
                if(tracer==null)tracer=ParticleMaterial("trace_01");
                tracer.color=new Color(1,.70f,.26f);
                sparkMaterial=Resources.Load<Material>("Foundry/FX/Spark");
                if(sparkMaterial==null)sparkMaterial=ParticleMaterial("spark_01",true);
                muzzleMaterial=Resources.Load<Material>("Foundry/FX/Muzzle");
                if(muzzleMaterial==null)muzzleMaterial=ParticleMaterial("muzzle_01",true);
                smokeMaterial=Resources.Load<Material>("Foundry/FX/Smoke");
                if(smokeMaterial==null)smokeMaterial=ParticleMaterial("smoke_01");
            }
            var beam=new GameObject("Shot tracer");var line=beam.AddComponent<LineRenderer>();
            line.sharedMaterial=tracer;line.positionCount=2;line.SetPosition(0,origin);line.SetPosition(1,end);
            line.startWidth=.025f;line.endWidth=.006f;line.textureMode=LineTextureMode.Stretch;
            Object.Destroy(beam,.08f);
            Burst(end,"spark_01",sparkMaterial,.055f,.22f,9,new Color(1,.62f,.2f),3.5f,1.5f);
            Burst(origin,"muzzle_01",muzzleMaterial,.28f,.07f,1,new Color(1,.72f,.3f),.2f,0);
            Burst(origin,"smoke_01",smokeMaterial,.30f,.55f,3,new Color(.55f,.57f,.55f,.32f),.45f,-.1f);
            var flash=new GameObject("Muzzle flash");flash.transform.position=origin;
            var light=flash.AddComponent<Light>();light.color=new Color(1,.60f,.22f);light.intensity=2.4f;light.range=3.5f;Object.Destroy(flash,.06f);
            if(rifle==null){rifle=Synthesize(false);pistol=Synthesize(true);}
            var audio=new GameObject("Spatial gunshot");audio.transform.position=origin;
            var source=audio.AddComponent<AudioSource>();source.spatialBlend=.9f;source.minDistance=2;source.maxDistance=80;
            source.volume=ArenaApp.Settings.Volume;source.clip=weapon==0?rifle:pistol;source.Play();Object.Destroy(audio,1);
        }
        static void Burst(Vector3 position,string texture,Material material,float size,float life,int count,Color color,float speed,float gravity)
        {
            if(material==null)return;
            var go=new GameObject("FX "+texture);go.transform.position=position;
            var particles=go.AddComponent<ParticleSystem>();
            particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=particles.main;main.duration=life;main.startLifetime=life;main.startSpeed=speed;main.startSize=size;
            main.startColor=color;main.loop=false;main.playOnAwake=false;main.gravityModifier=gravity;
            var emission=particles.emission;emission.rateOverTime=0;emission.SetBursts(new[]{new ParticleSystem.Burst(0,(short)count)});
            var shape=particles.shape;shape.enabled=false;
            var renderer=go.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;renderer.renderMode=ParticleSystemRenderMode.Billboard;
            particles.Play();Object.Destroy(go,life+1);
        }
        public static void Impact(Vector3 position,bool flesh)
        {
            if(Application.isBatchMode)return;
            if(metalImpacts==null) {
                metalImpacts=LoadClips("impactMetal_heavy",5);
                genericImpacts=LoadClips("impactGeneric_light",5);
                footsteps=LoadClips("footstep_concrete",5);
            }
            var clips=flesh?genericImpacts:metalImpacts;
            if(clips!=null&&clips.Length>0) Play(clips[Random.Range(0,clips.Length)],position,flesh?.22f:.30f);
        }
        public static void Footstep(Vector3 position)
        {
            if(Application.isBatchMode)return;
            if(footsteps==null) {
                metalImpacts=LoadClips("impactMetal_heavy",5);
                genericImpacts=LoadClips("impactGeneric_light",5);
                footsteps=LoadClips("footstep_concrete",5);
            }
            if(footsteps!=null&&footsteps.Length>0) Play(footsteps[Random.Range(0,footsteps.Length)],position,.20f);
        }
        static AudioClip[] LoadClips(string prefix,int count)
        {
            var clips=new AudioClip[count];
            for(int i=0;i<count;i++) clips[i]=Resources.Load<AudioClip>("Foundry/Audio/"+prefix+"_"+i.ToString("000"));
            return clips;
        }
        static void Play(AudioClip clip,Vector3 position,float volume)
        {
            if(clip==null)return;
            var go=new GameObject("Spatial one shot");go.transform.position=position;
            var source=go.AddComponent<AudioSource>();source.clip=clip;source.volume=volume*Mathf.Clamp01(ArenaApp.Settings.Volume*1.6f);
            source.spatialBlend=.9f;source.minDistance=2;source.maxDistance=45;source.Play();
            Object.Destroy(go,clip.length+.1f);
        }
        static AudioClip Synthesize(bool heavy)
        {
            const int rate=22050;int count=(int)(rate*(heavy?.38f:.26f));var data=new float[count];
            var random=new System.Random(heavy?937:153);
            for(int i=0;i<count;i++) {
                float t=i/(float)rate;float noise=(float)(random.NextDouble()*2-1);
                float body=Mathf.Sin(2*Mathf.PI*(heavy?68:128)*t)*.38f;
                float crack=Mathf.Sin(2*Mathf.PI*(heavy?220:420)*t)*.10f;
                data[i]=(noise*.62f+body+crack)*Mathf.Exp(-t*(heavy?15:23))*.72f;
            }
            var clip=AudioClip.Create(heavy?"Original pistol report":"Original rifle report",count,1,rate,false);clip.SetData(data,0);return clip;
        }
    }
}
