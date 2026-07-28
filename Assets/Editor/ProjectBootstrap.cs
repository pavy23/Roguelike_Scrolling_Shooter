using System;
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
            ppc.refResolutionX = 384;
            ppc.refResolutionY = 224;

            // Retro AA filter mode only exists in newer URP versions — set it if present.
            bool retroSet = false;
            var fmProp = typeof(PixelPerfectCamera).GetProperty("filterMode");
            if (fmProp != null && fmProp.PropertyType.IsEnum)
            {
                foreach (var name in Enum.GetNames(fmProp.PropertyType))
                {
                    if (name.IndexOf("retro", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        fmProp.SetValue(ppc, Enum.Parse(fmProp.PropertyType, name));
                        retroSet = true;
                        break;
                    }
                }
            }
            Debug.Log("[Bootstrap] PixelPerfectCamera: 384x224, PPU 16, RetroAA=" +
                      (retroSet ? "set" : "NOT AVAILABLE (set Filter Mode manually in Inspector)"));

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[Bootstrap] Done — scene saved.");
        }
    }
}
