using UnityEngine;

namespace Foundry
{
    // Original articulated model and weapons. No unavailable Asset Store dependencies.
    public sealed class SoldierRig
    {
        public readonly Transform Root, Rifle, Pistol, LeftLeg, RightLeg, LeftArm, RightArm, Head;
        readonly Material armor, trim, black;
        public SoldierRig(Transform parent,bool firstPerson)
        {
            Root=new GameObject(firstPerson?"Viewmodel":"Armored soldier").transform;Root.SetParent(parent,false);
            armor=ArenaWorld.Material(new Color(.17f,.32f,.35f));trim=ArenaWorld.Material(new Color(.96f,.40f,.17f));
            black=ArenaWorld.Material(new Color(.065f,.08f,.09f));
            if(!firstPerson) {
                Part("Chest",Root,new Vector3(0,1.18f,0),new Vector3(.63f,.66f,.34f),armor);
                Part("Plate",Root,new Vector3(0,1.28f,.20f),new Vector3(.46f,.36f,.10f),trim);
                Part("Belt",Root,new Vector3(0,.85f,0),new Vector3(.65f,.13f,.38f),black);
                Head=Pivot("Head",Root,new Vector3(0,1.63f,0));
                Part("Helmet",Head,Vector3.zero,new Vector3(.39f,.38f,.4f),armor);
                Part("Visor",Head,new Vector3(0,.01f,.215f),new Vector3(.34f,.13f,.04f),trim);
                LeftLeg=Limb("Left leg",Root,new Vector3(-.18f,.81f,0),new Vector3(.23f,.76f,.28f));
                RightLeg=Limb("Right leg",Root,new Vector3(.18f,.81f,0),new Vector3(.23f,.76f,.28f));
                LeftArm=Limb("Left arm",Root,new Vector3(-.43f,1.47f,.04f),new Vector3(.19f,.60f,.21f));
                RightArm=Limb("Right arm",Root,new Vector3(.43f,1.47f,.04f),new Vector3(.19f,.60f,.21f));
                Part("Jetpack",Root,new Vector3(0,1.22f,-.26f),new Vector3(.45f,.5f,.25f),black);
                Part("Thruster L",Root,new Vector3(-.2f,1.05f,-.39f),new Vector3(.14f,.32f,.14f),trim);
                Part("Thruster R",Root,new Vector3(.2f,1.05f,-.39f),new Vector3(.14f,.32f,.14f),trim);
            }
            var mount=Pivot("Weapon mount",Root,firstPerson?new Vector3(.23f,-.25f,.36f):new Vector3(.26f,1.24f,.35f));
            Rifle=Weapon(mount,false);Pistol=Weapon(mount,true);
            if(firstPerson) {
                Part("Glove",mount,new Vector3(0,-.13f,-.08f),new Vector3(.12f,.17f,.15f),armor);
                Part("Forearm",mount,new Vector3(.01f,-.19f,-.25f),new Vector3(.13f,.13f,.30f),black);
                Part("Support glove",mount,new Vector3(-.12f,-.07f,.28f),new Vector3(.11f,.12f,.16f),armor);
                foreach(var r in Root.GetComponentsInChildren<Renderer>()) r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }
        Transform Weapon(Transform parent,bool pistol)
        {
            var t=Pivot(pistol?"Desert Eagle":"M16A1",parent,Vector3.zero);
            // CC0 weapon models from Kenney's Weapon Pack replace the old box gun.
            var prefab=Resources.Load<GameObject>(pistol?"Foundry/Weapons/pistol":"Foundry/Weapons/machinegun");
            if(prefab==null) Debug.LogWarning("Weapon model missing: Foundry/Weapons/"+(pistol?"pistol":"machinegun"));
            if(prefab!=null) {
                var model=Object.Instantiate(prefab,t,false);
                model.name=pistol?"Desert Eagle model":"M16A1 model";
                model.transform.localPosition=new Vector3(0,pistol?-.02f:.01f,pistol?-.01f:-.04f);
                model.transform.localRotation=Quaternion.identity;
                model.transform.localScale=Vector3.one*(pistol?6.5f:6f);
                foreach(var collider in model.GetComponentsInChildren<Collider>()) Object.Destroy(collider);
                foreach(var renderer in model.GetComponentsInChildren<Renderer>()) {
                    foreach(var material in renderer.materials) {
                        material.SetFloat("_Glossiness",.38f);
                        material.SetFloat("_Metallic",.55f);
                    }
                }
                return t;
            }
            Part("Receiver",t,Vector3.zero,new Vector3(.10f,.14f,pistol?.30f:.54f),black);
            Part("Barrel",t,new Vector3(0,.025f,pistol?.23f:.48f),new Vector3(.055f,.055f,pistol?.18f:.45f),black);
            Part("Grip",t,new Vector3(0,-.14f,-.09f),new Vector3(.09f,.25f,.12f),armor);
            Part("Top rail",t,new Vector3(0,.10f,.01f),new Vector3(.04f,.03f,pistol?.24f:.44f),trim);
            if(!pistol) {
                Part("Stock",t,new Vector3(0,-.015f,-.40f),new Vector3(.09f,.18f,.27f),armor);
                Part("Magazine",t,new Vector3(0,-.19f,.12f),new Vector3(.08f,.26f,.16f),armor);
                Part("Handguard",t,new Vector3(0,0,.28f),new Vector3(.13f,.12f,.25f),armor);
                Part("Sight",t,new Vector3(0,.16f,.30f),new Vector3(.04f,.1f,.035f),black);
            }
            return t;
        }
        public void Animate(float speed,float phase,float pitch,bool crouch,bool flying,int weapon,bool reload)
        {
            Rifle.gameObject.SetActive(weapon==0);Pistol.gameObject.SetActive(weapon==1);
            if(LeftLeg==null)return;
            float swing=Mathf.Sin(phase)*Mathf.Clamp01(speed/6)*32;
            LeftLeg.localRotation=Quaternion.Euler(swing+(flying?25:0),0,0);
            RightLeg.localRotation=Quaternion.Euler(-swing+(flying?25:0),0,0);
            LeftArm.localRotation=Quaternion.Euler(-68+pitch*.3f+(reload?25:0),15,-10);
            RightArm.localRotation=Quaternion.Euler(-68+pitch*.3f,0,8);
            Head.localRotation=Quaternion.Euler(pitch,0,0);
            Root.localScale=new Vector3(1,crouch?.72f:1,1);
        }
        static Transform Pivot(string name,Transform parent,Vector3 position)
        {var t=new GameObject(name).transform;t.SetParent(parent,false);t.localPosition=position;return t;}
        Transform Limb(string name,Transform parent,Vector3 position,Vector3 size)
        {var t=Pivot(name,parent,position);Part(name,t,new Vector3(0,-size.y/2,0),size,armor);return t;}
        static void Part(string name,Transform parent,Vector3 position,Vector3 size,Material material)
        {
            var go=ArenaWorld.Box(name,position,size,material,parent);var collider=go.GetComponent<Collider>();
            collider.enabled=false;Object.Destroy(collider);
        }
    }
}
