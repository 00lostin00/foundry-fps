using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Foundry
{
    [RequireComponent(typeof(NetworkObject),typeof(CharacterController))]
    public sealed class ArenaPlayer : NetworkBehaviour
    {
        public static readonly List<ArenaPlayer> Players=new List<ArenaPlayer>();
        public static ArenaPlayer Local;
        public readonly NetworkVariable<Vector3> Position=new NetworkVariable<Vector3>();
        public readonly NetworkVariable<float> Yaw=new NetworkVariable<float>();
        public readonly NetworkVariable<float> Pitch=new NetworkVariable<float>();
        public readonly NetworkVariable<int> Health=new NetworkVariable<int>(100);
        public readonly NetworkVariable<int> Weapon=new NetworkVariable<int>();
        public readonly NetworkVariable<int> Ammo=new NetworkVariable<int>(30);
        public readonly NetworkVariable<int> Reserve=new NetworkVariable<int>(150);
        public readonly NetworkVariable<int> Kills=new NetworkVariable<int>();
        public readonly NetworkVariable<int> Deaths=new NetworkVariable<int>();
        public readonly NetworkVariable<float> Fuel=new NetworkVariable<float>(1);
        public readonly NetworkVariable<bool> Reloading=new NetworkVariable<bool>();
        public readonly NetworkVariable<bool> Crouched=new NetworkVariable<bool>();
        public readonly NetworkVariable<bool> Flying=new NetworkVariable<bool>();
        public readonly NetworkVariable<FixedString64Bytes> PlayerName=new NetworkVariable<FixedString64Bytes>();
        public readonly NetworkVariable<double> SpawnProtectionUntil=new NetworkVariable<double>();
        public readonly NetworkVariable<double> RespawnAt=new NetworkVariable<double>();
        public readonly NetworkVariable<double> RoundEnds=new NetworkVariable<double>();
        public readonly NetworkVariable<bool> RoundOver=new NetworkVariable<bool>();
        public readonly NetworkVariable<FixedString64Bytes> RoundWinner=new NetworkVariable<FixedString64Bytes>();
        readonly int[] magazines={30,7},reserves={150,42};
        CharacterController controller;
        SoldierRig worldRig,viewRig;
        Camera view;
        Vector2 moveInput;
        Vector3 previousPosition;
        float aimYaw,aimPitch,verticalSpeed,sendAt,phase,recoilVisual,shotAt,frameInputAt,nextStep,nextDebugLog;
        float desiredYaw,desiredPitch,lastInputTime;
        bool sprint,jump,jet,crouch,trigger,triggerWasDown,reloadInput;
        int desiredWeapon;
        double nextShot,reloadEnds;
        int reloadWeapon=-1;
        ulong inputSequence,lastSequence;
        public float HitMarkerUntil, DamageFlashUntil;

        public override void OnNetworkSpawn()
        {
            controller=GetComponent<CharacterController>();
            controller.height=1.85f;controller.radius=.32f;controller.center=new Vector3(0,.925f,0);
            controller.stepOffset=.32f;controller.slopeLimit=50;controller.enabled=IsServer;
            Players.Add(this);
            if(IsServer) {
                transform.position=SpawnPoint();Position.Value=transform.position;
                SpawnProtectionUntil.Value=NetworkManager.ServerTime.Time+2;
                PlayerName.Value=new FixedString64Bytes(ArenaApp.NameFor(OwnerClientId));
                lastInputTime=Time.unscaledTime;
            }
            transform.position=Position.Value;previousPosition=Position.Value;
            worldRig=new SoldierRig(transform,false);
            if(IsOwner && !Application.isBatchMode) {
                Local=this;worldRig.Root.gameObject.SetActive(false);
                var cameraObject=new GameObject("Player camera");view=cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<AudioListener>();cameraObject.AddComponent<ArenaPostFX>();view.nearClipPlane=.05f;view.farClipPlane=160;view.fieldOfView=ArenaApp.Settings.Fov;
                viewRig=new SoldierRig(view.transform,true);
                aimYaw=Yaw.Value;aimPitch=Pitch.Value;Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;
                ArenaApp.Instance.MenuCamera.gameObject.SetActive(false);
            }
        }
        public override void OnNetworkDespawn()
        {
            Players.Remove(this);
            if(Local==this) {
                Local=null;if(view!=null)Destroy(view.gameObject);
                Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
                if(ArenaApp.Instance!=null)ArenaApp.Instance.MenuCamera.gameObject.SetActive(true);
            }
        }
        void Update()
        {
            if(!IsSpawned)return;
            if(IsOwner && !Application.isBatchMode) {
                if(!ArenaApp.Instance.Paused && Health.Value>0 && Cursor.lockState!=CursorLockMode.Locked && Input.GetMouseButtonDown(0)) {
                    Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;
                }
                ReadInput();
            }
            if(!IsServer) {
                if(Vector3.Distance(transform.position,Position.Value)>5)transform.position=Position.Value;
                else transform.position=Vector3.Lerp(transform.position,Position.Value,1-Mathf.Exp(-18*Time.deltaTime));
            }
            transform.rotation=Quaternion.Euler(0,Yaw.Value,0);
            float speed=(Position.Value-previousPosition).magnitude/Mathf.Max(.001f,Time.deltaTime);
            previousPosition=Position.Value;phase+=Time.deltaTime*Mathf.Min(12,speed*2);
            if(speed>.8f && Health.Value>0 && Time.time>=nextStep) {
                nextStep=Time.time+(Crouched.Value?.62f:.42f);
                ArenaEffects.Footstep(transform.position);
            }
            if(worldRig!=null) {
                worldRig.Root.gameObject.SetActive(!IsOwner && Health.Value>0);
                worldRig.Animate(speed,phase,Pitch.Value,Crouched.Value,Flying.Value,Weapon.Value,Reloading.Value);
            }
        }
        void OnApplicationFocus(bool focus)
        {
            if(focus && IsSpawned && IsOwner && !Application.isBatchMode && !ArenaApp.Instance.Paused && Health.Value>0) {
                Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;
            }
        }
        void ReadInput()
        {
            if(ArenaApp.Instance.DebugInput && Time.unscaledTime>=nextDebugLog) {
                nextDebugLog=Time.unscaledTime+.5f;
                Debug.Log($"FOUNDRY_INPUT cursor={Cursor.lockState} focus={Application.isFocused} h={Input.GetAxisRaw("Horizontal")} v={Input.GetAxisRaw("Vertical")} health={Health.Value}");
            }
            if(ArenaApp.Instance.SmokeClient){SmokeInput();return;}
            if(Input.GetKeyDown(KeyCode.Escape))ArenaApp.Instance.TogglePause();
            bool enabled=Cursor.lockState==CursorLockMode.Locked && !ArenaApp.Instance.Paused && Health.Value>0;
            if(enabled) {
                aimYaw+=Input.GetAxisRaw("Mouse X")*ArenaApp.Settings.Sensitivity;
                aimPitch=Mathf.Clamp(aimPitch-Input.GetAxisRaw("Mouse Y")*ArenaApp.Settings.Sensitivity,-85,85);
                if(Input.GetKeyDown(KeyCode.Alpha1))desiredWeapon=0;
                if(Input.GetKeyDown(KeyCode.Alpha2))desiredWeapon=1;
                if(Mathf.Abs(Input.mouseScrollDelta.y)>.01f)desiredWeapon=1-desiredWeapon;
                if(Time.unscaledTime>=sendAt) {
                    sendAt=Time.unscaledTime+1f/30;
                    InputRpc(++inputSequence,new Vector2(Input.GetAxisRaw("Horizontal"),Input.GetAxisRaw("Vertical")),
                        aimYaw,aimPitch,Input.GetKey(KeyCode.LeftShift),Input.GetKey(KeyCode.Space),
                        Input.GetKey(KeyCode.F),Input.GetKey(KeyCode.LeftControl),Input.GetMouseButton(0),Input.GetKey(KeyCode.R),desiredWeapon);
                }
            } else if(Time.unscaledTime>=sendAt) {
                sendAt=Time.unscaledTime+1f/30;
                InputRpc(++inputSequence,Vector2.zero,aimYaw,aimPitch,false,false,false,false,false,false,Weapon.Value);
            }
        }
        void SmokeInput()
        {
            if(Time.unscaledTime-frameInputAt<.05f)return;frameInputAt=Time.unscaledTime;
            var target=Players.Find(p=>p!=this && p.Health.Value>0);
            float yaw=0,pitch=0;
            if(target!=null) {
                var delta=(target.Position.Value+Vector3.up*1.4f)-(Position.Value+Vector3.up*1.6f);
                yaw=Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg;
                pitch=-Mathf.Atan2(delta.y,new Vector2(delta.x,delta.z).magnitude)*Mathf.Rad2Deg;
            }
            InputRpc(++inputSequence,Vector2.zero,yaw,pitch,false,false,false,false,true,false,0);
        }
        [Rpc(SendTo.Server,InvokePermission=RpcInvokePermission.Owner,Delivery=RpcDelivery.Unreliable)]
        void InputRpc(ulong sequence,Vector2 move,float yaw,float pitch,bool run,bool leap,bool fly,bool duck,bool fire,bool reload,int weapon)
        {
            if(sequence<=lastSequence || !Finite(move.x)||!Finite(move.y)||!Finite(yaw)||!Finite(pitch))return;
            // Bound message processing to 60 updates/sec per player, including hostile clients.
            if(Time.unscaledTime-lastInputTime<1f/65)return;
            lastSequence=sequence;lastInputTime=Time.unscaledTime;
            if(ArenaApp.Instance.DebugInput)Debug.Log($"FOUNDRY_SERVER_INPUT {OwnerClientId} move={move}");
            moveInput=Vector2.ClampMagnitude(move,1);desiredYaw=Mathf.Repeat(yaw,360);
            desiredPitch=Mathf.Clamp(pitch,-85,85);sprint=run;jump=leap;jet=fly;crouch=duck;
            trigger=fire;reloadInput=reload;desiredWeapon=Mathf.Clamp(weapon,0,1);
        }
        static bool Finite(float f)=>!float.IsNaN(f)&&!float.IsInfinity(f);
        void FixedUpdate()
        {
            if(!IsSpawned || !IsServer)return;
            RoundEnds.Value=ArenaApp.Instance.MatchEnds;RoundOver.Value=ArenaApp.Instance.MatchFinished;
            RoundWinner.Value=new FixedString64Bytes(ArenaApp.Instance.Winner);
            var now=NetworkManager.ServerTime.Time;
            if(Health.Value<=0) {
                if(now>=RespawnAt.Value)Respawn();return;
            }
            if(ArenaApp.Instance.MatchFinished) {moveInput=Vector2.zero;trigger=false;jet=false;}
            if(Time.unscaledTime-lastInputTime>.3f) {moveInput=Vector2.zero;trigger=false;jet=false;jump=false;}
            Yaw.Value=Mathf.MoveTowardsAngle(Yaw.Value,desiredYaw,1080*Time.fixedDeltaTime);
            Pitch.Value=desiredPitch;transform.rotation=Quaternion.Euler(0,Yaw.Value,0);
            bool grounded=controller.isGrounded;
            Crouched.Value=crouch;
            controller.height=crouch?1.3f:1.85f;controller.center=new Vector3(0,controller.height/2,0);
            // Prevent standing into ceilings without changing the authoritative world position.
            if(!crouch && Physics.SphereCast(transform.position+Vector3.up*.6f,.3f,Vector3.up,out _,1.0f,1,QueryTriggerInteraction.Ignore)) {
                controller.height=1.3f;controller.center=new Vector3(0,.65f,0);Crouched.Value=true;
            }
            if(grounded&&verticalSpeed<0)verticalSpeed=-2;
            if(jump&&grounded)verticalSpeed=7;
            bool flying=jet&&Fuel.Value>.015f;
            Flying.Value=flying;
            if(flying) {
                verticalSpeed=Mathf.Min(8,verticalSpeed+22*Time.fixedDeltaTime);
                Fuel.Value=Mathf.Max(0,Fuel.Value-.33f*Time.fixedDeltaTime);
            } else {
                verticalSpeed-=20*Time.fixedDeltaTime;Fuel.Value=Mathf.Min(1,Fuel.Value+.18f*Time.fixedDeltaTime);
            }
            var direction=transform.right*moveInput.x+transform.forward*moveInput.y;
            var velocity=direction*(Crouched.Value?2.6f:sprint?8:5.6f)+Vector3.up*verticalSpeed;
            controller.Move(velocity*Time.fixedDeltaTime);Position.Value=transform.position;
            if(transform.position.y < -12 || transform.position.y>40) {Die(null);return;}
            if(desiredWeapon!=Weapon.Value) {
                magazines[Weapon.Value]=Ammo.Value;reserves[Weapon.Value]=Reserve.Value;
                Reloading.Value=false;reloadWeapon=-1;Weapon.Value=desiredWeapon;
                Ammo.Value=magazines[Weapon.Value];Reserve.Value=reserves[Weapon.Value];nextShot=Math.Max(nextShot,now+.25f);
            }
            var spec=WeaponRules.All[Weapon.Value];
            if(Reloading.Value && now>=reloadEnds) {
                int amount=WeaponRules.ReloadAmount(Ammo.Value,Reserve.Value,spec.Magazine);
                if(reloadWeapon==Weapon.Value){Ammo.Value+=amount;Reserve.Value-=amount;}
                Reloading.Value=false;reloadWeapon=-1;
            }
            if(reloadInput && !Reloading.Value && Ammo.Value<spec.Magazine && Reserve.Value>0) {
                Reloading.Value=true;reloadWeapon=Weapon.Value;reloadEnds=now+spec.ReloadSeconds;
            }
            if(trigger && (spec.Automatic || !triggerWasDown) && WeaponRules.CanFire(Ammo.Value,now,nextShot,Reloading.Value,true))Fire(now,spec);
            triggerWasDown=trigger;
        }
        void Fire(double now,WeaponSpec spec)
        {
            nextShot=now+spec.Interval;Ammo.Value--;SpawnProtectionUntil.Value=0;
            var origin=transform.position+Vector3.up*(Crouched.Value?1.12f:1.62f);
            var rotation=Quaternion.Euler(Pitch.Value,Yaw.Value,0);
            var spread=UnityEngine.Random.insideUnitCircle*spec.Spread;
            var direction=(rotation*new Vector3(spread.x,spread.y,1)).normalized;
            controller.enabled=false;
            var end=origin+direction*spec.Range;bool hitPlayer=false;
            bool hitWorld=Physics.Raycast(origin,direction,out var hit,spec.Range,~0,QueryTriggerInteraction.Ignore);
            if(hitWorld) {
                end=hit.point;var victim=hit.collider.GetComponentInParent<ArenaPlayer>();
                if(victim!=null && victim!=this && victim.Health.Value>0 && now>=victim.SpawnProtectionUntil.Value) {
                    hitPlayer=true;int damage=WeaponRules.Damage(spec.Damage,hit.point.y-victim.transform.position.y>(victim.Crouched.Value?1.1f:1.5f));
                    victim.Health.Value=Mathf.Max(0,victim.Health.Value-damage);
                    victim.DamagedRpc();if(victim.Health.Value==0)victim.Die(this);
                }
            }
            controller.enabled=true;
            ShotRpc(origin,end,Weapon.Value,hitPlayer,hitWorld);
        }
        void Die(ArenaPlayer killer)
        {
            Health.Value=0;Deaths.Value++;RespawnAt.Value=NetworkManager.ServerTime.Time+3;
            Reloading.Value=false;reloadWeapon=-1;trigger=false;controller.enabled=false;
            if(killer!=null)killer.Kills.Value++;
            KillRpc(killer==null?"WORLD":killer.PlayerName.Value.ToString(),PlayerName.Value.ToString());
        }
        void Respawn()
        {
            controller.enabled=false;transform.position=SpawnPoint();
            Position.Value=transform.position;controller.enabled=true;verticalSpeed=0;
            Health.Value=100;Fuel.Value=1;SpawnProtectionUntil.Value=NetworkManager.ServerTime.Time+2;
            magazines[0]=30;magazines[1]=7;reserves[0]=150;reserves[1]=42;
            Ammo.Value=magazines[Weapon.Value];Reserve.Value=reserves[Weapon.Value];nextShot=0;
        }
        public void ResetMatch()
        {if(!IsServer)return;Kills.Value=0;Deaths.Value=0;Respawn();}
        Vector3 SpawnPoint()=>ArenaApp.Instance.SmokeClient?new Vector3((OwnerClientId%2==0?-4:4),1,-17):
            ArenaWorld.SelectSpawn(OwnerClientId+(ulong)Deaths.Value);
        [Rpc(SendTo.ClientsAndHost)] void ShotRpc(Vector3 origin,Vector3 end,int weapon,bool hit,bool hitWorld)
        {
            ArenaEffects.Shot(origin,end,weapon);
            if(hit||hitWorld)ArenaEffects.Impact(end,hit);
            if(IsOwner) {recoilVisual+=WeaponRules.All[weapon].Recoil;aimPitch=Mathf.Max(-85,aimPitch-WeaponRules.All[weapon].Recoil*.35f);shotAt=Time.time;if(hit)HitMarkerUntil=Time.time+.18f;}
        }
        [Rpc(SendTo.ClientsAndHost)] void DamagedRpc(){if(IsOwner)DamageFlashUntil=Time.time+.25f;}
        [Rpc(SendTo.ClientsAndHost)] void KillRpc(string killer,string victim)=>ArenaApp.Instance.AddKill(killer,victim);
        void LateUpdate()
        {
            if(!IsOwner || view==null)return;
            var cameraPos=transform.position+Vector3.up*(Crouched.Value?1.12f:1.62f);
            view.transform.position=cameraPos;
            recoilVisual=Mathf.Lerp(recoilVisual,0,1-Mathf.Exp(-12*Time.deltaTime));
            view.transform.rotation=Quaternion.Euler(aimPitch-recoilVisual,aimYaw,0);
            view.fieldOfView=Mathf.Lerp(view.fieldOfView,Input.GetMouseButton(1)?ArenaApp.Settings.Fov*.72f:ArenaApp.Settings.Fov,Time.deltaTime*12);
            viewRig.Root.gameObject.SetActive(Health.Value>0 && !ArenaApp.Instance.Paused);
            viewRig.Animate(0,0,0,false,false,Weapon.Value,Reloading.Value);
            float reload=Reloading.Value?Mathf.Sin(Time.time*7)*.04f:0;
            viewRig.Root.localPosition=new Vector3(Mathf.Sin(phase)*.007f,reload+Mathf.Cos(phase*2)*.005f,-Mathf.Max(0,.08f-(Time.time-shotAt))*.65f);
            viewRig.Root.localRotation=Quaternion.Euler(Reloading.Value?25:0,0,Reloading.Value?-18:0);
        }
    }
}
