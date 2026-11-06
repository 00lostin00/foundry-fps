using UnityEngine;
using UnityEngine.Rendering;

namespace Foundry
{
    public static class ArenaWorld
    {
        public static readonly Vector3[] Spawns = {
            new Vector3(-25,1,-20),new Vector3(25,1,20),new Vector3(-25,1,20),new Vector3(25,1,-20),
            new Vector3(-18,1,0),new Vector3(18,1,0),new Vector3(0,1,-22),new Vector3(0,1,22)
        };
        static Material concrete, metal, wall, orange, teal, pale, dark;
        static Transform propRoot;

        public static Material Material(Color color)
        {
            var m=new Material(Shader.Find("Standard")); m.color=color; m.SetFloat("_Glossiness",.18f); return m;
        }
        public static GameObject Box(string name,Vector3 pos,Vector3 scale,Material material,Transform parent=null)
        {
            var obj=GameObject.CreatePrimitive(PrimitiveType.Cube); obj.name=name;
            obj.transform.SetParent(parent,false); obj.transform.localPosition=pos; obj.transform.localScale=scale;
            obj.GetComponent<Renderer>().sharedMaterial=material; return obj;
        }
        public static void Build()
        {
            concrete=Material(new Color(.45f,.47f,.45f)); metal=Material(new Color(.28f,.32f,.34f));
            wall=Material(new Color(.50f,.50f,.48f));
            orange=Material(new Color(.94f,.30f,.10f)); teal=Material(new Color(.10f,.48f,.50f));
            pale=Material(new Color(.80f,.80f,.72f)); dark=Material(new Color(.08f,.10f,.12f));
            var floorDiff=Resources.Load<Texture2D>("Foundry/Textures/Floor/concrete_floor_worn_001_diff_2k");
            if(floorDiff!=null) {
                ApplyPbr(concrete,"Floor","concrete_floor_worn_001",new Vector2(8,7),.05f,.24f);
            } else {
                concrete.mainTexture=GridTexture(new Color(.62f,.63f,.60f),new Color(.28f,.30f,.30f),16,false);
                concrete.mainTextureScale=new Vector2(8,7);
            }
            var wallDiff=Resources.Load<Texture2D>("Foundry/Textures/Wall/concrete_wall_003_diff_2k");
            if(wallDiff!=null) {
                ApplyPbr(wall,"Wall","concrete_wall_003",new Vector2(4,2),.05f,.28f);
            } else {
                wall.mainTexture=GridTexture(new Color(.55f,.55f,.52f),new Color(.25f,.26f,.25f),32,true);
                wall.mainTextureScale=new Vector2(4,2);
            }
            metal.mainTexture=GridTexture(new Color(.38f,.42f,.44f),new Color(.16f,.18f,.19f),32,true);
            metal.mainTextureScale=new Vector2(4,2);
            RenderSettings.ambientLight=new Color(.68f,.73f,.76f);
            RenderSettings.fog=true; RenderSettings.fogColor=new Color(.62f,.68f,.72f); RenderSettings.fogDensity=.0025f;
            var sun=new GameObject("Late afternoon").AddComponent<Light>(); sun.type=LightType.Directional;
            sun.transform.rotation=Quaternion.Euler(62,-40,0); sun.color=new Color(1,.95f,.86f); sun.intensity=1.6f;
            sun.shadows=LightShadows.Soft;
            Sky(sun);
            Box("Concrete foundation",new Vector3(0,-.5f,0),new Vector3(64,1,54),concrete);
            Box("North wall",new Vector3(0,4,27),new Vector3(64,8,1),wall);
            Box("South wall",new Vector3(0,4,-27),new Vector3(64,8,1),wall);
            Box("West wall",new Vector3(-32,4,0),new Vector3(1,8,54),wall);
            Box("East wall",new Vector3(32,4,0),new Vector3(1,8,54),wall);
            for(int side=-1;side<=1;side+=2) {
                Container(new Vector3(side*12,1.5f,side*10),side==1?orange:teal);
                Container(new Vector3(side*23,1.5f,-side*7),side==1?teal:orange);
                Box("Loading bay",new Vector3(side*21,3.5f,side*19),new Vector3(13,.5f,9),metal);
                for(int i=-1;i<=1;i+=2) Box("Column",new Vector3(side*21+i*5,1.6f,side*19),new Vector3(.5f,3.2f,.5f),pale);
                for(int z=-1;z<=1;z+=2) {
                    Box("Cover barrier",new Vector3(side*6,.8f,z*20),new Vector3(4,1.6f,1),pale);
                    for(int i=0;i<3;i++) Box("Shipping crate",new Vector3(side*(16+i*2.1f),.8f,z*13),new Vector3(1.8f,1.6f,1.8f),metal);
                }
                Box("Catwalk support",new Vector3(side*6,1.8f,0),new Vector3(1,3.6f,4),metal);
                var ramp=Box("Access ramp",new Vector3(side*11,1.7f,0),new Vector3(10,.35f,4),pale);
                ramp.transform.rotation=Quaternion.Euler(0,0,-side*20);
                Box("Upper railing",new Vector3(0,4.4f,side*2.3f),new Vector3(13,.15f,.12f),orange);
                for(int x=-6;x<=6;x+=3) Box("Railing post",new Vector3(x,3.9f,side*2.3f),new Vector3(.12f,1,.12f),metal);
            }
            Box("Central catwalk",new Vector3(0,3.4f,0),new Vector3(13,.4f,5),pale);
            for(int x=-30;x<=30;x+=5) {
                Box("Lane marking",new Vector3(x,.015f,7),new Vector3(2,.025f,.10f),pale);
                Box("Lane marking",new Vector3(x,.015f,-7),new Vector3(2,.025f,.10f),pale);
            }
            Sign("FOUNDRY",new Vector3(0,5.3f,26.4f),Quaternion.Euler(0,180,0),1.1f);
            Sign("01 / CASTING YARD",new Vector3(0,3.8f,-26.4f),Quaternion.identity,.65f);
            for(int i=0;i<Spawns.Length;i++) {
                var pad=Box("Deployment pad",new Vector3(Spawns[i].x,.02f,Spawns[i].z),new Vector3(2,.04f,2),i%2==0?orange:teal);
                Object.Destroy(pad.GetComponent<Collider>());
            }
            Dress();
            Lights();
            StripLights();
            Reflection();
        }
        static void Sky(Light sun)
        {
            var skyMaterial=Resources.Load<Material>("Foundry/Sky/Skybox");
            if(skyMaterial==null) {
                var hdr=Resources.Load<Texture2D>("Foundry/Sky/industrial_sunset_02_4k");
                var panoramic=Shader.Find("Skybox/Panoramic");
                if(hdr!=null&&panoramic!=null) {
                    hdr.wrapMode=TextureWrapMode.Repeat;
                    skyMaterial=new Material(panoramic);
                    skyMaterial.SetTexture("_MainTex",hdr);
                    skyMaterial.SetFloat("_Mapping",1f);
                    skyMaterial.SetFloat("_ImageType",0f);
                    skyMaterial.SetFloat("_Exposure",1.05f);
                }
            }
            if(skyMaterial!=null) {
                skyMaterial.SetColor("_Tint",Color.white);
                skyMaterial.SetFloat("_Exposure",1.1f);
                RenderSettings.skybox=skyMaterial;
                RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Skybox;
                RenderSettings.ambientIntensity=1.0f;
            } else {
                var shader=Shader.Find("Skybox/Procedural");
                if(shader!=null) {
                    var sky=new Material(shader);
                    sky.SetColor("_SkyTint",new Color(.48f,.58f,.70f));
                    sky.SetColor("_GroundColor",new Color(.20f,.21f,.20f));
                    sky.SetFloat("_AtmosphereThickness",1.02f);
                    sky.SetFloat("_Exposure",1.35f);
                    sky.SetFloat("_SunSize",.035f);
                    RenderSettings.skybox=sky;
                }
                RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Trilight;
                RenderSettings.ambientSkyColor=new Color(.55f,.62f,.70f);
                RenderSettings.ambientEquatorColor=new Color(.38f,.43f,.46f);
                RenderSettings.ambientGroundColor=new Color(.20f,.21f,.20f);
            }
            RenderSettings.sun=sun;
            RenderSettings.fogColor=new Color(.58f,.62f,.66f);
        }
        static Texture2D GridTexture(Color baseColor,Color lineColor,int cell,bool panels)
        {
            const int size=128;
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false);
            var pixels=new Color[size*size];
            for(int y=0;y<size;y++) for(int x=0;x<size;x++) {
                float noise=Mathf.PerlinNoise(x*.09f+3.1f,y*.09f+7.7f);
                var color=baseColor*(.82f+noise*.30f);
                if(x%cell==0||y%cell==0) color=Color.Lerp(color,lineColor,.55f);
                if(panels&&y%32==0) color=Color.Lerp(color,lineColor,.7f);
                pixels[y*size+x]=color;
            }
            texture.SetPixels(pixels); texture.wrapMode=TextureWrapMode.Repeat;
            texture.filterMode=FilterMode.Bilinear; texture.anisoLevel=4; texture.Apply();
            return texture;
        }
        static void ApplyPbr(Material material,string folder,string prefix,Vector2 tiling,float metallic,float glossiness)
        {
            var diffuse=Resources.Load<Texture2D>($"Foundry/Textures/{folder}/{prefix}_diff_2k");
            if(diffuse==null)return;
            material.color=Color.white;material.mainTexture=diffuse;material.mainTextureScale=tiling;
            var normal=Resources.Load<Texture2D>($"Foundry/Textures/{folder}/{prefix}_nor_gl_2k");
            if(normal!=null) {
                material.SetTexture("_BumpMap",normal);material.SetTextureScale("_BumpMap",tiling);material.SetFloat("_BumpScale",.9f);
            }
            var ao=Resources.Load<Texture2D>($"Foundry/Textures/{folder}/{prefix}_ao_2k");
            if(ao!=null) {
                material.SetTexture("_OcclusionMap",ao);material.SetTextureScale("_OcclusionMap",tiling);material.SetFloat("_OcclusionStrength",.85f);
            }
            material.SetFloat("_Metallic",metallic);material.SetFloat("_Glossiness",glossiness);
        }
        static void Container(Vector3 position,Material material)
        {
            Box("Freight container",position,new Vector3(9,3,4),material);
            for(int i=-4;i<=4;i++) for(int side=-1;side<=1;side+=2)
                Box("Corrugation",position+new Vector3(i,0,side*2.03f),new Vector3(.08f,2.8f,.08f),metal);
            Box("Container roof",position+Vector3.up*1.55f,new Vector3(9.1f,.1f,4.1f),metal);
        }
        static void Sign(string text,Vector3 position,Quaternion rotation,float scale)
        {
            var go=new GameObject(text);go.transform.SetPositionAndRotation(position,rotation);
            var label=go.AddComponent<TextMesh>();label.text=text;label.fontSize=96;label.characterSize=.12f*scale;
            label.anchor=TextAnchor.MiddleCenter;label.color=new Color(.95f,.71f,.40f);
        }
        static void Dress()
        {
            propRoot=new GameObject("Foundry props").transform;
            // Barrels and consoles give the arena readable industrial landmarks.
            Prop("barrelLarge",new Vector3(-28.8f,0,-11),0,1f);
            Prop("barrel",new Vector3(-27.2f,0,-9.3f),25,.95f);
            Prop("barrelLarge",new Vector3(28.8f,0,11),0,1f);
            Prop("barrel",new Vector3(27.2f,0,9.3f),-25,.95f);
            Prop("barrel",new Vector3(-18.5f,0,23.4f),40,.9f);
            Prop("barrelLarge",new Vector3(18.5f,0,-23.4f),-40,1f);
            Prop("console",new Vector3(-14,0,25.2f),180,1f);
            Prop("console",new Vector3(14,0,25.2f),180,1f);
            Prop("console",new Vector3(-14,0,-25.2f),0,1f);
            Prop("console",new Vector3(14,0,-25.2f),0,1f);
            Prop("consoleScreen",new Vector3(-14,3.65f,24.85f),180,.7f,false);
            Prop("consoleScreen",new Vector3(14,3.65f,24.85f),180,.7f,false);
            Prop("consoleScreen",new Vector3(-14,3.65f,-24.85f),0,.7f,false);
            Prop("consoleScreen",new Vector3(14,3.65f,-24.85f),0,.7f,false);
            // Catwalk edge protection.
            for(int x=-4;x<=4;x+=4) {
                Prop("metalFence",new Vector3(x,3.62f,2.45f),0,.32f,false);
                Prop("metalFence",new Vector3(x,3.62f,-2.45f),0,.32f,false);
            }
            // Wall pipes and access stairs.
            Prop("pipeStraight",new Vector3(30.85f,2.7f,-9),90,.3f,false);
            Prop("pipeStraight",new Vector3(30.85f,2.7f,9),90,.3f,false);
            Prop("pipeCorner",new Vector3(30.85f,2.7f,22),180,.3f,false);
            Prop("stairs",new Vector3(-27,0,16),90,.7f,false);
            Prop("stairsLong",new Vector3(27,0,-16),-90,.6f,false);
            // Floor plates, machinery and pickups.
            Prop("metalTileLarge",new Vector3(-20,.02f,-10),0,.4f,false);
            Prop("metalTileLarge",new Vector3(20,.02f,10),0,.4f,false);
            Prop("groundTileRough",new Vector3(0,.03f,-14),0,.8f,false);
            Prop("robot",new Vector3(-3.5f,0,12),35,.45f);
            Prop("satelliteDish",new Vector3(-29,0,23),-30,.32f,false);
            Prop("itemWeapon",new Vector3(7,.05f,-12),20,.55f,false);
        }
        static void Lights()
        {
            foreach(var p in new[]{new Vector3(-14,4.3f,25),new Vector3(14,4.3f,25),new Vector3(-14,4.3f,-25),new Vector3(14,4.3f,-25)}) {
                var lamp=new GameObject("Console glow").AddComponent<Light>(); lamp.transform.position=p;
                lamp.type=LightType.Point; lamp.color=new Color(.25f,.85f,.95f); lamp.range=12; lamp.intensity=2.5f; lamp.shadows=LightShadows.None;
            }
            foreach(var p in new[]{new Vector3(-26,6,-24),new Vector3(26,6,-24),new Vector3(-26,6,24),new Vector3(26,6,24),new Vector3(0,9,0)}) {
                var lamp=new GameObject("Arena lamp").AddComponent<Light>(); lamp.transform.position=p;
                lamp.type=LightType.Point; lamp.color=new Color(1,.82f,.62f); lamp.range=20; lamp.intensity=2.4f; lamp.shadows=LightShadows.None;
            }
            var fill=new GameObject("Fill light").AddComponent<Light>(); fill.type=LightType.Directional;
            fill.transform.rotation=Quaternion.Euler(24,145,0); fill.color=new Color(.48f,.62f,.78f); fill.intensity=.45f; fill.shadows=LightShadows.None;
        }
        static void StripLights()
        {
            var strip=Material(new Color(.035f,.04f,.045f));
            strip.EnableKeyword("_EMISSION");
            strip.SetColor("_EmissionColor",new Color(1f,.66f,.30f)*1.8f);
            Box("North light strip",new Vector3(0,7.55f,26.35f),new Vector3(54,.09f,.09f),strip);
            Box("South light strip",new Vector3(0,7.55f,-26.35f),new Vector3(54,.09f,.09f),strip);
            Box("West light strip",new Vector3(-31.35f,7.55f,0),new Vector3(.09f,.09f,48),strip);
            Box("East light strip",new Vector3(31.35f,7.55f,0),new Vector3(.09f,.09f,48),strip);
        }
        static void Reflection()
        {
            if(Application.isBatchMode)return;
            var probeGo=new GameObject("Foundry reflection");
            var probe=probeGo.AddComponent<ReflectionProbe>();
            probe.size=new Vector3(60,14,50);
            probe.center=new Vector3(0,5,0);
            probe.resolution=256;
            probe.hdr=true;
            probe.mode=ReflectionProbeMode.Realtime;
            probe.refreshMode=ReflectionProbeRefreshMode.ViaScripting;
            probe.clearFlags=ReflectionProbeClearFlags.Skybox;
            probe.RenderProbe();
        }
        static GameObject Prop(string name,Vector3 position,float yaw,float scale,bool solid=true)
        {
            var prefab=Resources.Load<GameObject>("Foundry/Props/"+name);
            if(prefab==null) { Debug.LogWarning("Missing prop "+name); return null; }
            var go=Object.Instantiate(prefab,propRoot);
            go.name=name;
            go.transform.position=Vector3.zero;
            go.transform.rotation=Quaternion.Euler(0,yaw,0);
            go.transform.localScale=Vector3.one*scale;
            var renderers=go.GetComponentsInChildren<Renderer>();
            if(renderers.Length==0) return go;
            var bounds=renderers[0].bounds;
            foreach(var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            go.transform.position=position-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
            foreach(var renderer in renderers) foreach(var material in renderer.materials) {
                material.SetFloat("_Glossiness",.32f);
                material.SetFloat("_Metallic",.38f);
            }
            if(solid) {
                var collider=new GameObject(name+" collider").AddComponent<BoxCollider>();
                collider.transform.SetParent(propRoot,true);
                collider.transform.position=position+Vector3.up*(bounds.size.y*.5f);
                collider.size=new Vector3(Mathf.Max(.2f,bounds.size.x*.8f),bounds.size.y,Mathf.Max(.2f,bounds.size.z*.8f));
            }
            return go;
        }
        public static Vector3 SelectSpawn(ulong seed)
        {
            var best=Spawns[(int)(seed%(ulong)Spawns.Length)]; float bestDistance=-1;
            for(int i=0;i<Spawns.Length;i++) {
                var candidate=Spawns[(i+(int)(seed%8))%Spawns.Length];float nearest=1000;
                foreach(var p in ArenaPlayer.Players) if(p.Health.Value>0)
                    nearest=Mathf.Min(nearest,Vector3.Distance(candidate,p.Position.Value));
                if(nearest>bestDistance){best=candidate;bestDistance=nearest;}
            }
            return best;
        }
    }
}
