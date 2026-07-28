using System.Collections.Generic;
using Shmup.Core.Generation;
using Shmup.Core.Simulation;

namespace Shmup.Presentation.Battle
{
    /// <summary>
    /// [임시 스캐폴딩] GameData JSON → Core 주입 모델의 손 변환.
    ///
    /// JSON 파싱은 Core 소유로 결정되었지만(Reviews/from-codex 2026-07-28), 파서는
    /// GROK의 스키마 확장(halfWidth, sine 파라미터, scrollSpeed, noDropWeight 등) 승인
    /// 후에 들어온다. 그때까지 여기 값은 GameData/*.json에서 **복사**한 것이며
    /// (스키마에 없는 항목만 잠정 플레이스홀더로 표시), 파서가 landed되면 이 파일을
    /// 삭제하고 Core 로더로 교체한다. 값의 권위는 항상 GameData JSON 쪽에 있다.
    /// </summary>
    public static class TempStageContent
    {
        const int U = SimSpace.SubUnitsPerWorldUnit;      // 256
        const int Tps = SimSpace.TicksPerSecond;          // 60

        public static BattleSimConfig CreateConfig()
        {
            var config = BattleSimConfig.CreateDefault();
            config.EnemyDespawnX = -14 * U;
            config.CapsuleHalfWidth = U * 5 / 16;          // 캡슐 스프라이트 10px 폭 절반 (잠정)
            config.CapsuleHalfHeight = U / 4;
            config.CapsuleNoDropWeight = 8;                // 잠정 — GROK 확정 대기 (dropWeight 4면 33% 드롭)
            config.ScrollSpeedNumerator = 3 * U;           // 3 u/s (잠정)
            config.ScrollSpeedDenominator = Tps;
            config.PlayerMaxHp = 3;                        // 잠정 — 접촉 1~2딜 체감용
            return config;
        }

        /// <summary>enemies.json 사본 (hp/contactDamage/moveSpeed/dropWeight). sine 진폭·주기와 히트박스만 잠정.</summary>
        public static BattleContent CreateContent()
        {
            var enemies = new List<EnemyDefinition>
            {
                new EnemyDefinition("zako_straight", 10, 1, EnemyMovePattern.Straight,
                    3 * U, Tps, U / 2, U * 3 / 8, 4, 0, 1),
                new EnemyDefinition("zako_sine", 10, 1, EnemyMovePattern.Sine,
                    (int)(2.5f * U), Tps, U / 2, U * 3 / 8, 5, (int)(1.5f * U), 120),
                new EnemyDefinition("turret_ground", 30, 1, EnemyMovePattern.Static,
                    0, 1, U / 2, U / 2, 2, 0, 1)
            };

            var weapons = new List<WeaponDefinition>
            {
                // weapons.json main_shot 사본. 탄 히트박스는 8×3px 스프라이트 절반 (잠정).
                new WeaponDefinition("main_shot", 10, 8, 12 * U, Tps, U / 4, U * 3 / 32)
            };

            return new BattleContent(enemies, weapons, "main_shot");
        }

        /// <summary>waves.json 사본 (기존 3세그먼트 + 보스 메타). 스폰 X만 화면 우측 밖 고정값(잠정).</summary>
        public static StageGenerationCatalog CreateCatalog()
        {
            const int spawnX = 13 * U;

            var segments = new List<StageSegmentTemplate>
            {
                new StageSegmentTemplate("seg_intro_line", 1, 3, 600, 7, 7, new[] { 7 },
                    new[]
                    {
                        new SpawnEvent(60, "zako_straight", spawnX, 2 * U),
                        new SpawnEvent(90, "zako_straight", spawnX, 2 * U),
                        new SpawnEvent(120, "zako_straight", spawnX, 2 * U)
                    }),
                new StageSegmentTemplate("seg_sine_pair", 1, 5, 600, 7, 7, new[] { 2 },
                    new[]
                    {
                        new SpawnEvent(60, "zako_sine", spawnX, 3 * U),
                        new SpawnEvent(60, "zako_sine", spawnX, -3 * U)
                    }),
                new StageSegmentTemplate("seg_turret_floor", 2, 5, 900, 7, 7, new[] { 6 },
                    new[]
                    {
                        new SpawnEvent(30, "turret_ground", spawnX, (int)(-5.5f * U)),
                        new SpawnEvent(300, "turret_ground", spawnX, (int)(-5.5f * U))
                    })
            };

            var bosses = new List<StageBossTemplate>
            {
                new StageBossTemplate("boss_stage1", 1, 1, 1, 5, 7)
            };

            return new StageGenerationCatalog(3, 3, 2, segments, bosses);
        }
    }
}
