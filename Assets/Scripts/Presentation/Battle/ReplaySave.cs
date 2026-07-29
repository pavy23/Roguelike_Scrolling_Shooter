using System;
using System.IO;
using Shmup.Core.Simulation;
using UnityEngine;

namespace Shmup.Presentation.Battle
{
    /// <summary>
    /// 마지막 런 리플레이 파일 (REQ-018/019 Presentation 몫).
    /// 시드+함선+입력 기록이면 결정론 코어가 런 전체를 재현한다.
    /// </summary>
    [Serializable]
    public sealed class ReplayFileData
    {
        public long seed;
        public string shipId;
        public long finalScore;
        public int difficultyNumerator = 1;
        public int difficultyDenominator = 1;
        public int[] rewardChoices;
        public InputRecordingData recording;
    }

    public static class ReplaySave
    {
        static string FilePath => Path.Combine(Application.persistentDataPath, "replay_last.json");

        public static void Save(ReplayFileData data)
        {
            if (data == null || data.recording == null) return;
            try
            {
                string temp = FilePath + ".tmp";
                File.WriteAllText(temp, JsonUtility.ToJson(data));
                if (File.Exists(FilePath)) File.Delete(FilePath);
                File.Move(temp, FilePath);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ReplaySave] 저장 실패({e.GetType().Name}). {e.Message}");
            }
        }

        public static ReplayFileData TryLoad()
        {
            try
            {
                if (!File.Exists(FilePath)) return null;
                var data = JsonUtility.FromJson<ReplayFileData>(File.ReadAllText(FilePath));
                return data != null && data.recording != null ? data : null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ReplaySave] 로드 실패({e.GetType().Name}) — 리플레이 무시. {e.Message}");
                return null;
            }
        }
    }
}
