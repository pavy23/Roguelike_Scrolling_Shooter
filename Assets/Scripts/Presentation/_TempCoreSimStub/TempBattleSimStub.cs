// =============================================================================
//  임시 스텁 — CLAUDE 소유가 아닌 코드의 자리 표시자.  Reviews/from-claude/requests.md REQ-001
// =============================================================================
//  이 파일에 들어 있는 것은 전부 CODEX 소유(Shmup.Core)여야 하는 게임 로직이다.
//  Core에 전투 시뮬레이션이 아직 없어서 씬이 컴파일조차 되지 않기 때문에,
//  요청한 것과 **완전히 같은 네임스페이스·시그니처**로 최소 구현을 임시로 둔다.
//
//  CODEX가 Shmup.Core에 Shmup.Core.Simulation 을 올리면:
//      → 이 폴더(_TempCoreSimStub)를 통째로 삭제한다. 그게 전부다.
//      → Presentation 뷰 코드는 한 줄도 바뀌지 않는다 (타입 이름이 동일하므로).
//      → 삭제하지 않으면 타입 중복으로 컴파일 에러가 난다. 의도된 동작이다 —
//        스텁이 조용히 살아남아 Core 구현을 가리는 것보다 낫다.
//
//  여기 있는 수치와 로직에는 아무 권위가 없다. 확정판은 CODEX 구현이다.
// =============================================================================

using System;
using System.Collections.Generic;

namespace Shmup.Core.Simulation
{
    /// <summary>시뮬레이션 좌표계 상수. 위치는 전부 서브유닛 정수 (AGENTS.md §4.5 정수 우선).</summary>
    public static class SimSpace
    {
        public const int SubUnitsPerWorldUnit = 256;
        public const int TicksPerSecond = 60;
    }

    public enum BulletFaction
    {
        Player = 0,
        Enemy = 1
    }

    /// <summary>한 틱 분량의 플레이어 입력. MoveX/MoveY는 [-1, 1]로 클램프된 8방향 디지털 입력.</summary>
    public readonly struct InputCommand
    {
        public InputCommand(int moveX, int moveY, bool fire)
        {
            MoveX = moveX < 0 ? -1 : (moveX > 0 ? 1 : 0);
            MoveY = moveY < 0 ? -1 : (moveY > 0 ? 1 : 0);
            Fire = fire;
        }

        public int MoveX { get; }
        public int MoveY { get; }
        public bool Fire { get; }

        public static InputCommand None => default;
    }

    /// <summary>탄 하나의 관측 가능한 상태. Id는 스폰~소멸까지 불변.</summary>
    public readonly struct BulletState
    {
        public BulletState(int id, BulletFaction faction, int x, int y)
        {
            Id = id;
            Faction = faction;
            X = x;
            Y = y;
        }

        public int Id { get; }
        public BulletFaction Faction { get; }
        public int X { get; }
        public int Y { get; }
    }

    /// <summary>튜닝 값. 기본값은 전부 플레이스홀더 — 최종 확정은 사람/GROK (AGENTS.md §7).</summary>
    public sealed class BattleSimConfig
    {
        /// <summary>서브유닛/틱. 34 ≈ 8 월드유닛/초.</summary>
        public int PlayerSpeedPerTick { get; set; }

        /// <summary>서브유닛/틱. 51 ≈ 12 월드유닛/초 (GameData/weapons.json main_shot).</summary>
        public int PlayerBulletSpeedPerTick { get; set; }

        public int FireIntervalTicks { get; set; }
        public int MaxBullets { get; set; }
        public int PlayerMinX { get; set; }
        public int PlayerMaxX { get; set; }
        public int PlayerMinY { get; set; }
        public int PlayerMaxY { get; set; }
        public int BulletDespawnX { get; set; }
        public int PlayerSpawnX { get; set; }
        public int PlayerSpawnY { get; set; }

        const int U = SimSpace.SubUnitsPerWorldUnit;

        /// <summary>384×224 @ PPU 16 = 24×14 월드유닛 화면 기준.</summary>
        public static BattleSimConfig CreateDefault() => new BattleSimConfig
        {
            PlayerSpeedPerTick = 34,
            PlayerBulletSpeedPerTick = 51,
            FireIntervalTicks = 8,
            MaxBullets = 64,
            PlayerMinX = -23 * U / 2,
            PlayerMaxX = 23 * U / 2,
            PlayerMinY = -13 * U / 2,
            PlayerMaxY = 13 * U / 2,
            BulletDespawnX = 13 * U,
            PlayerSpawnX = -8 * U,
            PlayerSpawnY = 0
        };
    }

    public interface IBattleSim
    {
        int Tick { get; }
        int PlayerX { get; }
        int PlayerY { get; }
        IReadOnlyList<BulletState> Bullets { get; }
        void Step(in InputCommand input);
    }

    /// <summary>
    /// 임시 구현. 플레이어를 경계 안에서 움직이고, 쿨다운마다 탄을 하나 스폰하고,
    /// 탄을 +X로 전진시키고, 화면 밖 탄을 회수한다. 그 이상은 하지 않는다.
    /// </summary>
    public sealed class BattleSim : IBattleSim
    {
        readonly BattleSimConfig _config;
        readonly List<BulletState> _bullets;

        // REQ-001대로 생성자에서 받아만 두고 스텁에서는 쓰지 않는다 (확산탄/드롭이 붙을 때 사용).
#pragma warning disable 0414
        readonly Rng _rng;
#pragma warning restore 0414

        int _cooldown;
        int _nextBulletId = 1;

        public BattleSim(BattleSimConfig config, Rng rng)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
            _bullets = new List<BulletState>(_config.MaxBullets);
            PlayerX = _config.PlayerSpawnX;
            PlayerY = _config.PlayerSpawnY;
        }

        public int Tick { get; private set; }
        public int PlayerX { get; private set; }
        public int PlayerY { get; private set; }

        /// <summary>매 틱 재사용되는 리스트. 호출자는 보관하지 말고 즉시 순회할 것.</summary>
        public IReadOnlyList<BulletState> Bullets => _bullets;

        public void Step(in InputCommand input)
        {
            Tick++;

            PlayerX = Clamp(PlayerX + input.MoveX * _config.PlayerSpeedPerTick,
                            _config.PlayerMinX, _config.PlayerMaxX);
            PlayerY = Clamp(PlayerY + input.MoveY * _config.PlayerSpeedPerTick,
                            _config.PlayerMinY, _config.PlayerMaxY);

            AdvanceBullets();

            if (_cooldown > 0) _cooldown--;
            if (input.Fire && _cooldown == 0 && _bullets.Count < _config.MaxBullets)
            {
                _bullets.Add(new BulletState(_nextBulletId++, BulletFaction.Player, PlayerX, PlayerY));
                _cooldown = _config.FireIntervalTicks;
            }
        }

        void AdvanceBullets()
        {
            // 뒤에서 앞으로 훑으며 제자리 압축 — 리스트 순서는 스폰 순서로 유지된다.
            int write = 0;
            for (int read = 0; read < _bullets.Count; read++)
            {
                var b = _bullets[read];
                int x = b.X + _config.PlayerBulletSpeedPerTick;
                if (x > _config.BulletDespawnX) continue;
                _bullets[write++] = new BulletState(b.Id, b.Faction, x, b.Y);
            }
            _bullets.RemoveRange(write, _bullets.Count - write);
        }

        static int Clamp(int v, int min, int max) => v < min ? min : (v > max ? max : v);
    }
}
