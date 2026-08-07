using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Shmup.EditorTools
{
    /// <summary>
    /// GameData 원본 → Resources 사본 동기화를 사람이 기억하지 않아도 되게 하는
    /// 두 개의 훅. 예전에는 WebGL 빌드 함수만 동기화를 불러서, 에디터 Play와
    /// Android 빌드는 낡은 사본으로 돌 수 있었다 (2026-08-05 회귀와 같은 계열의
    /// 구멍). 여기 두 훅은 빌드 파이프라인과 Play 진입에 구조적으로 걸려 있어
    /// 어떤 경로로 실행해도 건너뛸 수 없다.
    /// </summary>
    sealed class GameDataSyncOnBuild : IPreprocessBuildWithReport
    {
        // 다른 전처리보다 먼저 — 데이터가 맞춰진 뒤에 나머지가 돌아야 한다.
        public int callbackOrder => -100;

        public void OnPreprocessBuild(BuildReport report)
            => BattleSceneBuilder.SyncGameDataToResources();
    }

    [InitializeOnLoad]
    static class GameDataSyncOnPlay
    {
        static GameDataSyncOnPlay()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingEditMode)
                    BattleSceneBuilder.SyncGameDataToResources();
            };
        }
    }
}
