using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Shmup.EditorTools
{
    /// <summary>
    /// 원격 플레이용 모바일/웹 빌드. 배치모드에서 -executeMethod로 호출한다.
    ///
    /// APK:   Unity.exe -batchmode -projectPath . -executeMethod Shmup.EditorTools.MobileBuilder.BuildAndroid -quit
    /// WebGL: Unity.exe -batchmode -projectPath . -executeMethod Shmup.EditorTools.MobileBuilder.BuildWebGl -quit
    /// </summary>
    public static class MobileBuilder
    {
        static readonly string[] Scenes =
        {
            "Assets/Scenes/Title.unity",
            "Assets/Scenes/Battle.unity"
        };

        public static void BuildAndroid()
        {
            // 세로 화면이 아니라 가로 고정 (횡스크롤 슈팅)
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;

            PlayerSettings.companyName = "pavy";
            PlayerSettings.productName = "Roguelike Scrolling Shooter";
            PlayerSettings.SetApplicationIdentifier(
                UnityEditor.Build.NamedBuildTarget.Android, "com.pavy.rss");
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            // 디버그 서명으로 충분 (사이드로드 설치용)
            PlayerSettings.Android.useCustomKeystore = false;
            EditorUserBuildSettings.buildAppBundle = false;

            string outDir = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", "Builds", "Mobile"));
            Directory.CreateDirectory(outDir);
            string apk = Path.Combine(outDir, "RSS.apk");

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = apk,
                target = BuildTarget.Android,
                options = BuildOptions.None
            });

            Report(report, apk);
        }

        public static void BuildWebGl()
        {
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.template = "PROJECT:Default";
            PlayerSettings.runInBackground = false;

            string outDir = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", "Builds", "Web"));
            Directory.CreateDirectory(outDir);

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = outDir,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            });

            Report(report, outDir);
        }

        static void Report(BuildReport report, string path)
        {
            var summary = report.summary;
            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[MobileBuilder] 성공: {path} " +
                          $"({summary.totalSize / 1024 / 1024}MB, {summary.totalTime})");
            }
            else
            {
                Debug.LogError($"[MobileBuilder] 실패: {summary.result} " +
                               $"(errors={summary.totalErrors})");
                EditorApplication.Exit(1);
            }
        }
    }
}
