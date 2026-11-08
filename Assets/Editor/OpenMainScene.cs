using UnityEditor;
using UnityEditor.SceneManagement;

namespace DeepDwarf.EditorTools
{
    /// <summary>Opens the game scene when the editor starts on an empty, unsaved scene (e.g. a fresh clone).</summary>
    [InitializeOnLoad]
    static class OpenMainScene
    {
        const string ScenePath = "Assets/Scenes/Main.unity";
        const string DoneKey = "DeepDwarf.OpenMainScene.Done";

        static OpenMainScene()
        {
            if (SessionState.GetBool(DoneKey, false)) return;
            SessionState.SetBool(DoneKey, true);
            EditorApplication.delayCall += () =>
            {
                if (UnityEngine.Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) return;
                var active = EditorSceneManager.GetActiveScene();
                if (string.IsNullOrEmpty(active.path) && !active.isDirty && System.IO.File.Exists(ScenePath))
                    EditorSceneManager.OpenScene(ScenePath);
            };
        }
    }
}
