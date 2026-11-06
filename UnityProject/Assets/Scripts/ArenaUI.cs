using System;
using System.Linq;
using UnityEngine;

namespace Foundry
{
    public sealed partial class ArenaApp
    {
        GUIStyle title,heading,label,small,button,field,large;
        readonly Color ink=new Color(.055f,.07f,.08f,.95f),paper=new Color(.91f,.92f,.86f),accent=new Color(1,.40f,.17f);
        Vector2 roomScroll;
        Texture2D crosshair;
        void Styles()
        {
            if(title!=null)return;
            var semibold=Resources.Load<Font>("Foundry/Fonts/Rajdhani-SemiBold");
            var medium=Resources.Load<Font>("Foundry/Fonts/Rajdhani-Medium");
            title=new GUIStyle(GUI.skin.label){fontSize=66,fontStyle=FontStyle.Bold};title.normal.textColor=paper;
            if(semibold!=null)title.font=semibold;
            heading=new GUIStyle(title){fontSize=23};
            label=new GUIStyle(title){fontSize=17,fontStyle=FontStyle.Normal};
            if(medium!=null)label.font=medium;
            small=new GUIStyle(label){fontSize=13};large=new GUIStyle(title){fontSize=42};
            button=new GUIStyle(GUI.skin.button){fontSize=15,fixedHeight=39,alignment=TextAnchor.MiddleCenter};
            if(semibold!=null)button.font=semibold;
            field=new GUIStyle(GUI.skin.textField){fontSize=16,fixedHeight=35,padding=new RectOffset(10,10,7,7)};
            if(medium!=null)field.font=medium;
            crosshair=Resources.Load<Texture2D>("Foundry/Crosshairs/crosshair");
        }
        static void Panel(Rect rect,Color color)
        {var old=GUI.color;GUI.color=color;GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=old;}
        void OnGUI()
        {
            if(Application.isBatchMode)return;Styles();
            float scale=Mathf.Min(Screen.width/1280f,Screen.height/720f);
            GUI.matrix=Matrix4x4.TRS(new Vector3((Screen.width-1280*scale)/2,(Screen.height-720*scale)/2,0),Quaternion.identity,new Vector3(scale,scale,1));
            if(network==null || !network.IsListening)Menu();
            else if(ArenaPlayer.Local!=null)Hud();
            else {
                Panel(new Rect(360,260,560,200),ink);GUI.Label(new Rect(390,285,500,60),"CONNECTING TO FOUNDRY…",heading);
                if(GUI.Button(new Rect(390,365,500,40),"CANCEL CONNECTION",button)){network.Shutdown();ReturnToMenu();}
            }
            if(Paused)PauseMenu();
            if(settingsOpen)SettingsPanel();
        }
        void Menu()
        {
            Panel(new Rect(0,0,510,720),ink);Panel(new Rect(38,40,42,5),accent);
            GUI.Label(new Rect(38,64,430,90),"FOUNDRY",title);
            GUI.Label(new Rect(42,150,430,26),"MULTIPLAYER FPS  /  LOCAL OPERATIONS",small);
            GUI.Label(new Rect(42,207,420,24),"OPERATOR",small);
            playerName=GUI.TextField(new Rect(42,233,420,35),playerName,24,field);
            GUI.Label(new Rect(42,282,420,24),"ROOM NAME",small);
            roomName=GUI.TextField(new Rect(42,308,420,35),roomName,40,field);
            GUI.Label(new Rect(42,357,420,24),"ROOM PASSWORD  /  OPTIONAL",small);
            password=GUI.PasswordField(new Rect(42,383,420,35),password,'•',64,field);
            GUI.enabled=!busy && network!=null && network.NetworkConfig.PlayerPrefab!=null;
            if(GUI.Button(new Rect(42,440,420,44),busy?"WORKING…":"CREATE ROOM",button))StartCoroutine(CreateRoom());
            if(GUI.Button(new Rect(42,495,205,39),"LOCAL HOST",button))StartDirect(false);
            if(GUI.Button(new Rect(257,495,205,39),"DIRECT CONNECT",button))ConnectDirect();
            GUI.enabled=true;
            GUI.Label(new Rect(42,550,145,20),"GAME ADDRESS",small);
            address=GUI.TextField(new Rect(42,575,275,35),address,field);
            portText=GUI.TextField(new Rect(328,575,134,35),portText,5,field);
            if(GUI.Button(new Rect(42,641,205,35),"SETTINGS",button))settingsOpen=true;
            if(GUI.Button(new Rect(257,641,205,35),"QUIT",button))Application.Quit();
            Panel(new Rect(550,40,690,578),new Color(.055f,.07f,.08f,.85f));
            GUI.Label(new Rect(580,65,450,35),"AVAILABLE ROOMS",heading);
            GUI.enabled=!busy;
            if(GUI.Button(new Rect(1085,62,125,35),"REFRESH",button))StartCoroutine(RefreshRooms());
            GUI.enabled=true;
            roomScroll=GUI.BeginScrollView(new Rect(575,123,645,462),roomScroll,new Rect(0,0,620,Mathf.Max(440,rooms.Length*76)));
            if(rooms.Length==0) {
                GUI.Label(new Rect(15,100,590,35),"THE YARD IS QUIET.",heading);
                GUI.Label(new Rect(15,145,580,80),"Run tools/start.ps1, then create a room.\nFor two windows on this PC, use tools/play-local.ps1.",label);
            }
            for(int i=0;i<rooms.Length;i++) {
                var r=rooms[i];float y=i*76;
                Panel(new Rect(0,y,615,65),new Color(.14f,.18f,.19f,.95f));
                GUI.Label(new Rect(15,y+8,390,25),r.name+(r.locked?"  [LOCKED]":""),label);
                GUI.Label(new Rect(15,y+36,390,22),r.map+"   /   "+r.players+" OF "+r.capacity+" OPERATORS",small);
                GUI.enabled=!busy&&r.players<r.capacity;
                if(GUI.Button(new Rect(490,y+13,110,37),"JOIN",button))StartCoroutine(Join(r));
                GUI.enabled=true;
            }
            GUI.EndScrollView();
            Panel(new Rect(550,632,690,46),ink);GUI.Label(new Rect(565,643,660,35),status,small);
            GUI.Label(new Rect(553,690,680,22),"01 / FOUNDRY    •    FREE FOR ALL    •    FIRST TO 20",small);
        }
        void Hud()
        {
            var p=ArenaPlayer.Local;
            Panel(new Rect(30,28,245,66),ink);GUI.Label(new Rect(46,38,215,28),"FOUNDRY / 01",heading);
            GUI.Label(new Rect(46,68,215,23),"FFA  /  20 ELIMINATIONS",small);
            int seconds=(int)Math.Max(0,MatchEnds-network.ServerTime.Time);
            Panel(new Rect(558,28,164,56),ink);GUI.Label(new Rect(591,35,120,45),$"{seconds/60:00}:{seconds%60:00}",heading);
            for(int i=0;i<killFeed.Count;i++)GUI.Label(new Rect(880,38+i*27,370,25),killFeed[i].text,small);
            if(p.Health.Value>0&&!Paused) {
                float spread=5+Mathf.Abs(Input.GetAxisRaw("Horizontal"))*3;
                if(crosshair!=null) {
                    float size=24+spread*1.6f;
                    var old=GUI.color;GUI.color=new Color(1,1,1,.88f);
                    GUI.DrawTexture(new Rect(640-size/2,360-size/2,size,size),crosshair,ScaleMode.ScaleToFit,true);
                    GUI.color=old;
                } else {
                    Panel(new Rect(639-spread-8,359,8,2),paper);Panel(new Rect(641+spread,359,8,2),paper);
                    Panel(new Rect(639,359-spread-8,2,8),paper);Panel(new Rect(639,361+spread,2,8),paper);
                }
                if(Time.time<p.HitMarkerUntil)GUI.Label(new Rect(627,345,35,40),"×",heading);
            }
            if(p.Health.Value>0 && !Paused && Cursor.lockState!=CursorLockMode.Locked) {
                Panel(new Rect(420,430,440,40),ink);
                GUI.Label(new Rect(435,440,410,24),"CLICK TO CAPTURE MOUSE  /  WASD",small);
            }
            Panel(new Rect(30,607,288,83),ink);GUI.Label(new Rect(47,616,160,47),p.Health.Value.ToString("000"),large);
            GUI.Label(new Rect(147,639,125,25),"HEALTH",small);
            Panel(new Rect(47,674,250,5),new Color(.25f,.29f,.29f));Panel(new Rect(47,674,250*p.Health.Value/100f,5),accent);
            Panel(new Rect(344,650,225,40),ink);GUI.Label(new Rect(357,658,70,20),"JET",small);
            Panel(new Rect(398,666,151,4),new Color(.25f,.29f,.29f));Panel(new Rect(398,666,151*p.Fuel.Value,4),new Color(.25f,.8f,.8f));
            Panel(new Rect(978,607,272,83),ink);
            GUI.Label(new Rect(995,614,240,25),WeaponRules.All[p.Weapon.Value].Name,small);
            GUI.Label(new Rect(995,638,245,45),p.Reloading.Value?"RELOADING":$"{p.Ammo.Value:00} / {p.Reserve.Value:000}",heading);
            GUI.Label(new Rect(34,575,700,22),"WASD MOVE   SHIFT SPRINT   SPACE JUMP   F JET   CTRL CROUCH   R RELOAD   1/2 WEAPON",small);
            if(Time.time<p.DamageFlashUntil) {
                Panel(new Rect(0,0,1280,12),new Color(1,.12f,.05f,.6f));Panel(new Rect(0,708,1280,12),new Color(1,.12f,.05f,.6f));
            }
            if(p.Health.Value<=0) {
                Panel(new Rect(380,265,520,150),ink);GUI.Label(new Rect(415,287,450,45),"OPERATOR DOWN",large);
                GUI.Label(new Rect(416,350,450,30),"REDEPLOYING IN "+Math.Max(0,Math.Ceiling(p.RespawnAt.Value-network.ServerTime.Time)),label);
            } else if(network.ServerTime.Time<p.SpawnProtectionUntil.Value)GUI.Label(new Rect(475,480,500,25),"SPAWN PROTECTION / FIRING CANCELS",small);
            if(Input.GetKey(KeyCode.Tab)||MatchFinished)Scoreboard();
            DrawHealthBars(p);
        }
        void Scoreboard()
        {
            Panel(new Rect(330,115,620,450),ink);
            GUI.Label(new Rect(357,134,570,44),MatchFinished?"ROUND COMPLETE":"OPERATORS",heading);
            GUI.Label(new Rect(357,180,570,30),MatchFinished?"WINNER / "+Winner:"PLAYER                                      KILLS      DEATHS",small);
            int row=0;
            foreach(var p in ArenaPlayer.Players.OrderByDescending(x=>x.Kills.Value)) {
                if(row>=10)break;float y=225+row++*28;
                GUI.Label(new Rect(360,y,355,26),p.PlayerName.Value.ToString(),label);
                GUI.Label(new Rect(739,y,70,26),p.Kills.Value.ToString(),label);GUI.Label(new Rect(846,y,70,26),p.Deaths.Value.ToString(),label);
            }
            if(MatchFinished)GUI.Label(new Rect(357,524,570,25),"ESC / MENU     •     HOST CAN START THE NEXT ROUND",small);
        }
        void DrawHealthBars(ArenaPlayer self)
        {
            var camera=Camera.main;
            // Runtime player cameras are selected explicitly because no tagged camera is required.
            if(camera==null)camera=FindObjectsByType<Camera>(FindObjectsSortMode.None).FirstOrDefault(c=>c.isActiveAndEnabled&&c!=MenuCamera);
            if(camera==null)return;
            float scale=Mathf.Min(Screen.width/1280f,Screen.height/720f);
            foreach(var p in ArenaPlayer.Players) {
                if(p==self || p.Health.Value<=0 || Vector3.Distance(self.Position.Value,p.Position.Value)>30)continue;
                var from=self.Position.Value+Vector3.up*1.6f;var target=p.Position.Value+Vector3.up*2.15f;
                if(Physics.Linecast(from,target,1,QueryTriggerInteraction.Ignore))continue;
                var v=camera.WorldToScreenPoint(target);if(v.z<=0)continue;
                float x=(v.x-(Screen.width-1280*scale)/2)/scale,y=(Screen.height-v.y-(Screen.height-720*scale)/2)/scale;
                Panel(new Rect(x-32,y,64,4),ink);Panel(new Rect(x-32,y,p.Health.Value*.64f,4),accent);
                GUI.Label(new Rect(x-60,y-22,150,22),p.PlayerName.Value.ToString(),small);
            }
        }
        void PauseMenu()
        {
            Panel(new Rect(0,0,1280,720),new Color(0,0,0,.62f));Panel(new Rect(430,145,420,430),ink);
            GUI.Label(new Rect(465,174,350,55),"OPERATIONS",large);
            if(GUI.Button(new Rect(465,250,350,42),"RESUME",button))TogglePause();
            if(GUI.Button(new Rect(465,307,350,42),"SETTINGS",button))settingsOpen=true;
            if(network.IsServer && GUI.Button(new Rect(465,364,350,42),"RESTART ROUND",button)) {
                MatchFinished=false;Winner="";MatchEnds=network.ServerTime.Time+480;
                foreach(var player in ArenaPlayer.Players)player.ResetMatch();TogglePause();
            }
            if(GUI.Button(new Rect(465,421,350,42),"LEAVE SESSION",button))StartCoroutine(Leave());
            GUI.Label(new Rect(465,490,350,50),"Online matches continue while this menu is open.",small);
        }
        void SettingsPanel()
        {
            Panel(new Rect(0,0,1280,720),new Color(0,0,0,.7f));Panel(new Rect(400,140,480,440),ink);
            GUI.Label(new Rect(435,166,420,40),"SETTINGS",heading);
            GUI.Label(new Rect(435,233,420,25),$"MOUSE SENSITIVITY / {Settings.Sensitivity:0.0}",label);
            Settings.Sensitivity=GUI.HorizontalSlider(new Rect(435,272,410,20),Settings.Sensitivity,.2f,6);
            GUI.Label(new Rect(435,310,420,25),$"FIELD OF VIEW / {Settings.Fov:0}",label);
            Settings.Fov=GUI.HorizontalSlider(new Rect(435,349,410,20),Settings.Fov,65,110);
            GUI.Label(new Rect(435,387,420,25),$"VOLUME / {Settings.Volume*100:0}%",label);
            Settings.Volume=GUI.HorizontalSlider(new Rect(435,426,410,20),Settings.Volume,0,1);
            if(GUI.Button(new Rect(435,491,410,42),"SAVE & CLOSE",button)) {
                PlayerPrefs.SetString("foundry.settings",JsonUtility.ToJson(Settings));PlayerPrefs.Save();settingsOpen=false;
            }
        }
    }
}
