using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Shmup.EditorTools
{
    /// <summary>
    /// One-shot project setup, runnable headless:
    /// Unity.exe -batchmode -projectPath . -executeMethod Shmup.EditorTools.ProjectBootstrap.Configure -quit
    /// </summary>
    public static class ProjectBootstrap
    {
        public static void Configure()
        {
            EditorSettings.serializationMode = SerializationMode.ForceText;
            VersionControlSettings.mode = "Visible Meta Files";
            Debug.Log("[Bootstrap] EditorSettings: ForceText + Visible Meta Files");

            const string scenePath = "Assets/Scenes/SampleScene.unity";
            var scene = EditorSceneManager.OpenScene(scenePath);

            var cam = Camera.main;
            if (cam == null)
                cam = UnityEngine.Object.FindFirstObjectByType<Camera>();
            if (cam == null)
            {
                Debug.LogError("[Bootstrap] No camera found in " + scenePath);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }

            var ppc = cam.GetComponent<PixelPerfectCamera>();
            if (ppc == null) ppc = cam.gameObject.AddComponent<PixelPerfectCamera>();
            ppc.assetsPPU = 16;
            ppc.refResolutionX = 640;
            ppc.refResolutionY = 360;

            // URP 17의 PixelPerfectCamera는 Filter Mode를 public 프로퍼티로 노출하지 않고
            // private [SerializeField] m_FilterMode로만 들고 있다 — SerializedObject로 직접 쓴다.
            var ppcSo = new SerializedObject(ppc);
            var filterModeProp = ppcSo.FindProperty("m_FilterMode");
            bool retroSet = filterModeProp != null;
            if (retroSet)
            {
                filterModeProp.enumValueIndex = (int)PixelPerfectCamera.PixelPerfectFilterMode.RetroAA;
                ppcSo.ApplyModifiedPropertiesWithoutUndo();
            }
            Debug.Log("[Bootstrap] PixelPerfectCamera: 640x360, PPU 16, RetroAA=" +
                      (retroSet ? "set" : "NOT AVAILABLE (set Filter Mode manually in Inspector)"));

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[Bootstrap] Done — scene saved.");
        }
    }
}
