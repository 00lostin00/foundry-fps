using System;
using System.IO;
using System.Linq;
using Foundry;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class FoundryBuild
{
    const string Scene="Assets/Scenes/Foundry.unity";
    [InitializeOnLoadMethod]
    static void OnLoad()
    {
        if(!Application.isBatchMode)EditorApplication.delayCall+=()=> {
            if(!File.Exists(Scene))Prepare();
        };
    }
    [MenuItem("Foundry/Prepare Project")]
    public static void Prepare()
    {
        Directory.CreateDirectory("Assets/Scenes");Directory.CreateDirectory("Assets/Resources");
        PlayerSettings.companyName="LocalWorkshop";PlayerSettings.productName="Foundry";
        PlayerSettings.bundleVersion="1.0.0";PlayerSettings.runInBackground=true;
        PlayerSettings.defaultScreenWidth=1280;PlayerSettings.defaultScreenHeight=720;
        PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
        PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
        PlayerSettings.SetApiCompatibilityLevel(UnityEditor.Build.NamedBuildTarget.Standalone,ApiCompatibilityLevel.NET_Standard);
        PlayerSettings.insecureHttpOption=InsecureHttpOption.DevelopmentOnly;
        // HTTP is restricted to this development project's loopback lobby. Release deployment requires HTTPS.
        if(!File.Exists("Assets/Resources/Operator.prefab")) {
            var go=new GameObject("Operator");go.layer=8;go.AddComponent<NetworkObject>();
            go.AddComponent<CharacterController>();go.AddComponent<ArenaPlayer>();
            PrefabUtility.SaveAsPrefabAsset(go,"Assets/Resources/Operator.prefab");UnityEngine.Object.DestroyImmediate(go);
        }
        if(!File.Exists("Assets/Resources/RuntimePalette.mat"))
            AssetDatabase.CreateAsset(new Material(Shader.Find("Standard")),"Assets/Resources/RuntimePalette.mat");
        if(!File.Exists("Assets/Resources/RuntimeEffects.mat"))
            AssetDatabase.CreateAsset(new Material(Shader.Find("Sprites/Default")),"Assets/Resources/RuntimeEffects.mat");
        EnsureFxMaterials();
        EnsureSkyMaterial();
        ConfigureAxes();
        if(!File.Exists(Scene)) {
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene,Scene);
        }
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(Scene,true)};
        AssetDatabase.SaveAssets();AssetDatabase.Refresh();
        Debug.Log("FOUNDRY_PROJECT_READY");
    }
    static void EnsureFxMaterials()
    {
        Directory.CreateDirectory("Assets/Resources/Foundry/FX");
        var sprite=Shader.Find("Sprites/Default");
        var additive=Shader.Find("Legacy Shaders/Particles/Additive");
        if(additive==null)additive=sprite;
        EnsureMaterial("Assets/Resources/Foundry/FX/Tracer.mat",sprite,"Assets/Resources/Foundry/Particles/trace_01.png");
        EnsureMaterial("Assets/Resources/Foundry/FX/Muzzle.mat",additive,"Assets/Resources/Foundry/Particles/muzzle_01.png");
        EnsureMaterial("Assets/Resources/Foundry/FX/Spark.mat",additive,"Assets/Resources/Foundry/Particles/spark_01.png");
        EnsureMaterial("Assets/Resources/Foundry/FX/Smoke.mat",sprite,"Assets/Resources/Foundry/Particles/smoke_01.png");
    }
    static void EnsureSkyMaterial()
    {
        Directory.CreateDirectory("Assets/Resources/Foundry/Sky");
        const string path="Assets/Resources/Foundry/Sky/Skybox.mat";
        if(File.Exists(path))return;
        var shader=Shader.Find("Skybox/Panoramic");
        if(shader==null)return;
        var material=new Material(shader);
        var hdr=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/Foundry/Sky/industrial_sunset_02_4k.hdr");
        if(hdr!=null)material.SetTexture("_MainTex",hdr);
        material.SetFloat("_Mapping",1);
        material.SetFloat("_ImageType",0);
        material.SetFloat("_Exposure",1.05f);
        AssetDatabase.CreateAsset(material,path);
    }
    static void EnsureMaterial(string path,Shader shader,string texturePath)
    {
        if(File.Exists(path)||shader==null)return;
        var material=new Material(shader);
        var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        if(texture!=null)material.mainTexture=texture;
        AssetDatabase.CreateAsset(material,path);
    }
    static void ConfigureAxes()
    {
        var obj=AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/InputManager.asset").FirstOrDefault();
        if(obj==null)return;
        var serialized=new SerializedObject(obj);var axes=serialized.FindProperty("m_Axes");
        Add("Horizontal","a","d","left","right",0,0,3,3);
        Add("Vertical","s","w","down","up",0,0,3,3);
        Add("Mouse X","","","","",1,0,0,.1f);
        Add("Mouse Y","","","","",1,1,0,.1f);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        void Add(string name,string negative,string positive,string altNegative,string altPositive,int type,int axis,float gravity,float sensitivity)
        {
            SerializedProperty item=null;
            for(int i=0;i<axes.arraySize;i++)if(axes.GetArrayElementAtIndex(i).FindPropertyRelative("m_Name").stringValue==name){item=axes.GetArrayElementAtIndex(i);break;}
            if(item==null){axes.InsertArrayElementAtIndex(axes.arraySize);item=axes.GetArrayElementAtIndex(axes.arraySize-1);}
            item.FindPropertyRelative("m_Name").stringValue=name;
            item.FindPropertyRelative("negativeButton").stringValue=negative;item.FindPropertyRelative("positiveButton").stringValue=positive;
            item.FindPropertyRelative("altNegativeButton").stringValue=altNegative;item.FindPropertyRelative("altPositiveButton").stringValue=altPositive;
            item.FindPropertyRelative("gravity").floatValue=gravity;item.FindPropertyRelative("dead").floatValue=.001f;
            item.FindPropertyRelative("sensitivity").floatValue=sensitivity;item.FindPropertyRelative("snap").boolValue=true;
            item.FindPropertyRelative("invert").boolValue=false;item.FindPropertyRelative("type").intValue=type;
            item.FindPropertyRelative("axis").intValue=axis;item.FindPropertyRelative("joyNum").intValue=0;
        }
    }
    [MenuItem("Foundry/Build Windows Client")]
    public static void Windows()=>Build(BuildTarget.StandaloneWindows64,"windows/Foundry.exe");
    [MenuItem("Foundry/Build Linux Server")]
    public static void Linux()=>Build(BuildTarget.StandaloneLinux64,"linux/Foundry.x86_64");
    static void Build(BuildTarget target,string relative)
    {
        Prepare();var path=Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts",relative));
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{Scene},locationPathName=path,target=target,options=BuildOptions.Development});
        if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Foundry build failed: "+report.summary.result);
        Debug.Log("FOUNDRY_BUILD_SUCCEEDED "+path);
    }
}
