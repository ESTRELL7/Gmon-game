using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeepDwarf.EditorTools
{
    /// <summary>Project configuration and builds. Available from the menu and from the command line.</summary>
    public static class ProjectSetup
    {
        const string ScenePath = "Assets/Scenes/Main.unity";
        const string ConfigPath = "Assets/Resources/GameConfig.asset";

        [MenuItem("Tools/Deep Dwarf/Setup Project")]
        public static void Run()
        {
            ConfigurePlayer();
            CreateConfig();
            CreateScene();
            AssetDatabase.SaveAssets();
            Debug.Log("Deep Dwarf: project setup finished");
        }

        static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "ESTRELL7";
            PlayerSettings.productName = "Deep Dwarf";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.runInBackground = false;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.estrell7.deepdwarf");

            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;

            PlayerSettings.defaultWebScreenWidth = 540;
            PlayerSettings.defaultWebScreenHeight = 960;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            // Lets the web build run on hosts that do not send Content-Encoding headers.
            PlayerSettings.WebGL.decompressionFallback = true;
        }

        static void CreateConfig()
        {
            if (AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath) != null) return;
            AssetDatabase.CreateAsset(GameConfig.CreateDefault(), ConfigPath);
        }

        static void CreateScene()
        {
            Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            cameraObject.AddComponent<AudioListener>();
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            new GameObject("Game").AddComponent<GameController>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        [MenuItem("Tools/Deep Dwarf/Build WebGL")]
        public static void BuildWebGL() => Build(BuildTarget.WebGL, "Builds/WebGL");

        [MenuItem("Tools/Deep Dwarf/Build Android APK")]
        public static void BuildAndroid()
        {
            EditorUserBuildSettings.buildAppBundle = false;
            Build(BuildTarget.Android, "Builds/Android/DeepDwarf.apk");
        }

        static void Build(BuildTarget target, string path)
        {
            string folder = Path.HasExtension(path) ? Path.GetDirectoryName(path) : path;
            Directory.CreateDirectory(folder);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = path,
                target = target,
                options = BuildOptions.None,
            });
            Debug.Log($"Deep Dwarf build {target}: {report.summary.result}, {report.summary.totalSize / (1024 * 1024)} MB");
            if (Application.isBatchMode && report.summary.result != BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }

        [MenuItem("Tools/Deep Dwarf/Reset Save")]
        public static void ResetSave()
        {
            PlayerPrefs.DeleteKey(GameController.SaveKey);
            PlayerPrefs.Save();
            Debug.Log("Deep Dwarf: save deleted");
        }
    }
}
