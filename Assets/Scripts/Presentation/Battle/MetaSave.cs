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

            /// <summary>구매한 컨티뉴 재고 (REQ-104). 구 저장에는 키가 없어 0으로 읽힌다.</summary>
            public int continueStock;
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
                    var state = MetaState.FromData(ToCoreData(dto));
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

        /// <summary>
        /// 저장 DTO → Core 입력 DTO. 컨티뉴 재고는 **현행 스키마로 선언**해야 Core
        /// 마이그레이션이 값을 살려 준다 — 레거시(schemaVersion 0)로 넣으면
        /// <c>continueStock</c>이 구 저장 취급으로 0이 되어 구매한 재고가 사라진다.
        /// 체크섬은 여기서 봉인한다: 이 파일은 원래부터 체크섬 없는 평문 DTO였고
        /// (변조 방어는 로컬 저장에 애초에 없다) 봉인은 Core 검증을 통과시키는 절차다.
        ///
        /// 손으로 고친 파일의 범위 밖 재고는 예외가 아니라 클램프로 처리한다 —
        /// Core 생성자가 던지면 상위 catch가 저장 **전체**를 초기화해 해금까지 날아간다.
        /// </summary>
        static MetaStateData ToCoreData(MetaSaveDto dto)
        {
            var data = new MetaStateData
            {
                totalCurrency = dto.totalCurrency,
                unlockedShipIds = dto.unlockedShipIds ?? Array.Empty<string>(),
                selectedShipId = dto.selectedShipId,
                continueStock = Mathf.Clamp(
                    dto.continueStock,
                    0,
                    Shmup.Core.Simulation.ContinueEconomyConfig.DefaultMaximumStock)
            };
            SaveDataIntegrity.Seal(data);
            return data;
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
            return MetaState.FromData(ToCoreData(new MetaSaveDto
            {
                totalCurrency = carryCurrency,
                unlockedShipIds = new[] { defaultShip.Id },
                selectedShipId = defaultShip.Id
            }));
        }

        /// <summary>tmp 기록 → 기존 파일을 bak으로 승격 → 교체 (SafeFile).</summary>
        /// <summary>
        /// **개발용 초기화.** 저장 파일과 백업을 지워 다음 Load가 신품 상태를
        /// 만들게 한다. 크레딧·해금·컨티뉴 재고가 전부 사라진다.
        ///
        /// 왜 필요한가: 진행은 브라우저 안(IndexedDB로 매핑되는 persistentDataPath)에
        /// 있어서 밖에서 지울 방법이 없다. 사이트 데이터를 통째로 지우면 되지만
        /// 그건 옵션 설정까지 함께 날린다 — 화폐만 되돌리고 싶을 때가 있다
        /// (사람 요청 2026-08-05: "내 장비 크레딧 기록도 초기화해줘").
        /// </summary>
        public static void DeleteSave()
        {
            SafeFile.Delete(SavePath);
        }

        public static void Save(MetaState state)
        {
            var exported = state.ExportData();
            var dto = new MetaSaveDto
            {
                totalCurrency = exported.totalCurrency,
                unlockedShipIds = exported.unlockedShipIds,
                selectedShipId = exported.selectedShipId,
                continueStock = exported.continueStock
            };
            SafeFile.Write(SavePath, JsonUtility.ToJson(dto, prettyPrint: true));
        }
    }
}
