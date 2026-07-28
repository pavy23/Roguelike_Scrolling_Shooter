using System;
using System.IO;
using Shmup.Core;
using Shmup.Core.Content;
using UnityEngine;

namespace Shmup.Presentation.Battle
{
    /// <summary>
    /// 함선 해금 메타 저장 (CODEX 후속 요청). Core MetaState의 ExportData/FromData만 쓰고,
    /// 파일 경로·원자적 쓰기·손상 복구는 여기(Presentation) 소유다.
    /// </summary>
    public static class MetaSave
    {
        [Serializable]
        sealed class MetaSaveDto
        {
            public long totalCurrency;
            public string[] unlockedShipIds;
            public string selectedShipId;
        }

        static string SavePath => Path.Combine(Application.persistentDataPath, "meta.json");

        /// <summary>저장이 없거나 손상이면 기본 함선만 해금된 초기 상태를 만든다.</summary>
        public static MetaState Load(GameDataSet data)
        {
            try
            {
                if (File.Exists(SavePath))
                {
                    var dto = JsonUtility.FromJson<MetaSaveDto>(File.ReadAllText(SavePath));
                    var state = MetaState.FromData(new MetaStateData
                    {
                        totalCurrency = dto.totalCurrency,
                        unlockedShipIds = dto.unlockedShipIds ?? Array.Empty<string>(),
                        selectedShipId = dto.selectedShipId
                    });
                    // 삭제된 함선을 선택 중인 구버전 저장 복구 (Core는 검증 예외를 낸다)
                    if (data.FindShip(state.SelectedShipId) == null)
                    {
                        Debug.LogWarning(
                            $"[MetaSave] 저장된 선택 함선 '{state.SelectedShipId}' 이 카탈로그에 없음 — 기본 함선으로 복구.");
                        return CreateFresh(data, state.ExportData().totalCurrency);
                    }
                    return state;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[MetaSave] 저장 로드 실패({e.GetType().Name}) — 초기화. {e.Message}");
            }
            return CreateFresh(data, 0);
        }

        static MetaState CreateFresh(GameDataSet data, long carryCurrency)
        {
            var defaultShip = data.DefaultShip;
            return MetaState.FromData(new MetaStateData
            {
                totalCurrency = carryCurrency,
                unlockedShipIds = new[] { defaultShip.Id },
                selectedShipId = defaultShip.Id
            });
        }

        /// <summary>임시 파일 → 교체로 원자적 저장.</summary>
        public static void Save(MetaState state)
        {
            var exported = state.ExportData();
            var dto = new MetaSaveDto
            {
                totalCurrency = exported.totalCurrency,
                unlockedShipIds = exported.unlockedShipIds,
                selectedShipId = exported.selectedShipId
            };
            string temp = SavePath + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(dto, prettyPrint: true));
            if (File.Exists(SavePath)) File.Delete(SavePath);
            File.Move(temp, SavePath);
        }
    }
}
