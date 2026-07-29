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
                // 본 파일 손상 시 .bak 폴백 (SafeFile) — 해금 진행 소실 방지
                string text = SafeFile.Read(SavePath, IsUsable);
                if (text != null)
                {
                    var dto = JsonUtility.FromJson<MetaSaveDto>(text);
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

        static bool IsUsable(string text)
        {
            try
            {
                var dto = JsonUtility.FromJson<MetaSaveDto>(text);
                return dto != null && !string.IsNullOrEmpty(dto.selectedShipId);
            }
            catch
            {
                return false;
            }
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

        /// <summary>tmp 기록 → 기존 파일을 bak으로 승격 → 교체 (SafeFile).</summary>
        public static void Save(MetaState state)
        {
            var exported = state.ExportData();
            var dto = new MetaSaveDto
            {
                totalCurrency = exported.totalCurrency,
                unlockedShipIds = exported.unlockedShipIds,
                selectedShipId = exported.selectedShipId
            };
            SafeFile.Write(SavePath, JsonUtility.ToJson(dto, prettyPrint: true));
        }
    }
}
