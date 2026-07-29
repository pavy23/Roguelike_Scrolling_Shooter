using System;
using System.IO;
using Shmup.Core.Simulation;
using UnityEngine;

namespace Shmup.Presentation.Battle
{
    /// <summary>
    /// 런 중단 저장 파일 (REQ-017 Presentation 몫). MetaSave와 같은 원자적 쓰기 패턴.
    /// Core RunSuspendData가 원본이고 여기는 직렬화/파일 IO만 담당한다.
    /// 손상/버전 불일치는 null로 폴백 — 이어하기가 없던 것처럼 동작한다.
    /// </summary>
    public static class RunSave
    {
        static string FilePath => Path.Combine(Application.persistentDataPath, "run.json");

        public static void Save(RunSuspendData data)
        {
            if (data == null) return;
            try
            {
                string temp = FilePath + ".tmp";
                File.WriteAllText(temp, JsonUtility.ToJson(data, prettyPrint: true));
                if (File.Exists(FilePath)) File.Delete(FilePath);
                File.Move(temp, FilePath);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RunSave] 저장 실패({e.GetType().Name}) — 이어하기 없이 진행. {e.Message}");
            }
        }

        public static RunSuspendData TryLoad()
        {
            try
            {
                if (!File.Exists(FilePath)) return null;
                var data = JsonUtility.FromJson<RunSuspendData>(File.ReadAllText(FilePath));
                if (data == null || data.schemaVersion != RunSuspendData.CurrentSchemaVersion)
                    return null;
                return data;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RunSave] 로드 실패({e.GetType().Name}) — 이어하기 무시. {e.Message}");
                return null;
            }
        }

        public static void Delete()
        {
            try
            {
                if (File.Exists(FilePath)) File.Delete(FilePath);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RunSave] 삭제 실패({e.GetType().Name}). {e.Message}");
            }
        }
    }
}
