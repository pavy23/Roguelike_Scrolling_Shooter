using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.Serialization;
using Shmup.Core.Generation;

namespace Shmup.Core.Simulation
{
    public readonly struct WarshipDamageCommand
    {
        public WarshipDamageCommand(string partId, int damage)
        {
            if (string.IsNullOrEmpty(partId))
                throw new ArgumentException(
                    "Warship damage requires a part id.", nameof(partId));
            if (damage < 1)
                throw new ArgumentOutOfRangeException(nameof(damage));
            PartId = partId;
            Damage = damage;
        }

        public string PartId { get; }
        public int Damage { get; }
    }

    public readonly struct WarshipPartState
    {
        internal WarshipPartState(
            string partId,
            string groupId,
            WarshipGroupRole groupRole,
            int x,
            int y,
            int hp,
            int maxHp,
            bool active)
        {
            PartId = partId;
            GroupId = groupId;
            GroupRole = groupRole;
            X = x;
            Y = y;
            Hp = hp;
            MaxHp = maxHp;
            Active = active;
        }

        public string PartId { get; }
        public string GroupId { get; }
        public WarshipGroupRole GroupRole { get; }
        public int X { get; }
        public int Y { get; }
        public int Hp { get; }
        public int MaxHp { get; }
        public bool Destroyed => Hp == 0;
        public bool Active { get; }
        public bool Invulnerable => !Active || Destroyed;
    }

    [Serializable]
    [DataContract]
    public sealed class WarshipEncounterSuspendData
    {
        // v3: vertical staging anchor (REQ-139). Older payloads restore with a
        // zero anchor, which is exactly the pre-REQ-139 behaviour.
        public const int CurrentSchemaVersion = 3;

        [DataMember(Order = 0)]
        public int schemaVersion;
        [DataMember(Order = 1)]
        public string encounterId;
        [DataMember(Order = 2)]
        public int tick;
        [DataMember(Order = 3)]
        public long scrollOffset;
        [DataMember(Order = 4)]
        public long scrollRemainder;
        [DataMember(Order = 5)]
        public int activeGroupIndex;
        [DataMember(Order = 6)]
        public int activeGroupElapsedTicks;
        [DataMember(Order = 7)]
        public int destroyedAttritionParts;
        [DataMember(Order = 8)]
        public bool warningEmitted;
        [DataMember(Order = 9)]
        public bool midbossDefeated;
        [DataMember(Order = 10)]
        public bool completed;
        [DataMember(Order = 11)]
        public int[] partHp;
        [DataMember(Order = 12)]
        public bool coreOpeningConsumed;
        [DataMember(Order = 13)]
        public int anchorFromY;
        [DataMember(Order = 14)]
        public int anchorTargetY;
        [DataMember(Order = 15)]
        public int anchorElapsedTicks;
    }

    /// <summary>
    /// Deterministic, allocation-free-after-construction state machine for the
    /// persistent three-act warship. Damage commands are resolved in caller order.
    /// </summary>
    public sealed class WarshipEncounter
    {
        readonly WarshipEncounterDefinition _definition;
        readonly ReadOnlyCollection<BossPartDefinition> _parts;
        readonly int[] _partHp;
        readonly int[] _partGroups;
        readonly WarshipPartState[] _partView;
        readonly SimEvent[] _eventBuffer;
        int _eventCount;
        int _tick;
        long _scrollOffset;
        long _scrollRemainder;
        int _activeGroupIndex = -1;
        int _activeGroupElapsedTicks;
        // Vertical staging (REQ-139). The hull can begin an act low enough that
        // only its superstructure is on screen and rise into frame for the next
        // act. Kept as two integers plus an elapsed count so interpolation is
        // exact and a restored run lands on the identical pixel.
        int _anchorFromY;
        int _anchorTargetY;
        int _anchorElapsedTicks;
        int _destroyedAttritionParts;
        bool _warningEmitted;
        bool _midbossDefeated;
        bool _completed;
        bool _coreOpeningConsumed;
        bool _tickOpen;

        public WarshipEncounter(
            WarshipEncounterDefinition definition,
            IReadOnlyList<BossPartDefinition> parts)
        {
            _definition = definition
                ?? throw new ArgumentNullException(nameof(definition));
            if (parts == null)
                throw new ArgumentNullException(nameof(parts));
            var copy = new BossPartDefinition[parts.Count];
            _partHp = new int[parts.Count];
            _partGroups = new int[parts.Count];
            _partView = new WarshipPartState[parts.Count];
            for (int i = 0; i < copy.Length; i++)
            {
                copy[i] = parts[i] ?? throw new ArgumentException(
                    "Warship parts cannot contain null.", nameof(parts));
                _partHp[i] = copy[i].MaxHp;
                _partGroups[i] = FindGroupIndex(copy[i].PartId);
                if (_partGroups[i] < 0)
                    throw new ArgumentException(
                        $"Warship part '{copy[i].PartId}' has no group.",
                        nameof(parts));
            }
            _parts = new ReadOnlyCollection<BossPartDefinition>(copy);
            _eventBuffer = new SimEvent[parts.Count + 6];

            // **경고 구간부터 1막 높이에 있다.** 예전에는 앵커가 0으로 시작해서,
            // 경고가 끝나고 1막이 열리는 프레임에 목표(-4.5)로 뚝 떨어졌다 —
            // 사람이 사진 두 장으로 보고한 그 끊김이다("처음엔 약간 위쪽에서
            // 등장하다가 갑자기 아래 위치로 부자연스럽게 끊기듯 이동해").
            //
            // AnchorOffsetY는 그룹이 열리기 전(_activeGroupIndex < 0)에는
            // _anchorTargetY를 그대로 돌려주므로, 여기서 1막 목표로 잡아 두면
            // 등장부터 그 자리에 선다.
            if (_definition.Groups.Count > 0)
            {
                _anchorTargetY = _definition.Groups[0].AnchorOffsetY;
                _anchorFromY = _anchorTargetY;
            }
            RefreshPartView();
        }

        public WarshipEncounterDefinition Definition => _definition;
        public int Tick => _tick;
        public long ScrollOffset => _scrollOffset;
        public long ScrollRemainder => _scrollRemainder;
        public int WorldX => SaturateToInt(
            (long)_definition.OriginX - _scrollOffset);
        public int ActiveGroupIndex => _activeGroupIndex;

        /// <summary>
        /// Current vertical offset from <see cref="WarshipEncounterDefinition.OriginY"/>,
        /// in sub-units. Interpolated with integer arithmetic only.
        /// </summary>
        public int AnchorOffsetY
        {
            get
            {
                if (_activeGroupIndex < 0) return _anchorTargetY;
                int travel = _definition.Groups[_activeGroupIndex]
                    .AnchorTravelTicks;
                if (travel <= 0 || _anchorElapsedTicks >= travel)
                    return _anchorTargetY;
                long delta = (long)_anchorTargetY - _anchorFromY;
                return SaturateToInt(
                    _anchorFromY + delta * _anchorElapsedTicks / travel);
            }
        }

        /// <summary>0 to 1 in thousandths: how far this act's move has run.
        /// The view uses it to lead the camera and time the reveal.</summary>
        /// <summary>
        /// 함체가 지금 세로로 **이동 중인가.**
        ///
        /// 2막에서 이동 중에는 앞쪽 포탑만 쏘고 멈추면 전부 쏜다 (사람 지시
        /// 2026-08-05: "움직일땐 앞의 3개 레이저, 멈추면 6개 전부 레이저 쏘자").
        /// 이동 중에 여섯이 다 쏘면 화면이 잠기고, 아무도 안 쏘면 썰렁하다 —
        /// 그 사이를 만드는 것이 이 상태다.
        /// </summary>
        public bool AnchorMoving
        {
            get
            {
                if (_activeGroupIndex < 0) return false;
                int travel = _definition.Groups[_activeGroupIndex]
                    .AnchorTravelTicks;
                return travel > 0 && _anchorElapsedTicks < travel;
            }
        }

        /// <summary>
        /// 지금 막의 함체 이동 길이(틱). 0이면 그 막은 제자리에서 시작한다.
        ///
        /// BattleSim이 이 값을 알아야 하는 이유: 2막 포탑 주기는 560~1060틱인데
        /// 이동은 240틱이다. 막이 바뀔 때 쿨다운을 통째로 깔면 이동 4초 동안
        /// **한 발도** 안 나가서 "이동 중엔 앞 3개만" 규칙이 발동할 기회조차
        /// 없다. 앞쪽 포탑의 첫 발을 이 창 안에 배치하려고 노출한다.
        /// </summary>
        public int ActiveAnchorElapsedTicks => _anchorElapsedTicks;

        public int ActiveAnchorTravelTicks =>
            _activeGroupIndex < 0
                ? 0
                : _definition.Groups[_activeGroupIndex].AnchorTravelTicks;

        public int AnchorTravelPermille
        {
            get
            {
                if (_activeGroupIndex < 0) return 1000;
                int travel = _definition.Groups[_activeGroupIndex]
                    .AnchorTravelTicks;
                if (travel <= 0) return 1000;
                if (_anchorElapsedTicks >= travel) return 1000;
                return (int)((long)_anchorElapsedTicks * 1000 / travel);
            }
        }
        public int ActiveGroupElapsedTicks => _activeGroupElapsedTicks;
        public bool WarningActive => _activeGroupIndex < 0;
        public bool MidbossDefeated => _midbossDefeated;
        public bool CoreBattleActive =>
            !_completed
            && _activeGroupIndex == _definition.Groups.Count - 1;
        public bool CoreOpeningPending =>
            CoreBattleActive && !_coreOpeningConsumed;
        public bool Completed => _completed;
        public int DestroyedAttritionParts => _destroyedAttritionParts;
        public int TotalAttritionParts =>
            _definition.Groups[1].PartIds.Count;
        public int CoreOpeningWays
        {
            get
            {
                long reduction =
                    (long)_destroyedAttritionParts
                    * _definition.WaysReductionPerTurret;
                long result = _definition.BaseCoreOpeningWays - reduction;
                return result < _definition.MinimumCoreOpeningWays
                    ? _definition.MinimumCoreOpeningWays
                    : (int)result;
            }
        }
        public ArraySegment<SimEvent> EventsThisTick =>
            new ArraySegment<SimEvent>(_eventBuffer, 0, _eventCount);
        public IReadOnlyList<WarshipPartState> Parts => _partView;

        public bool WasPartDestroyed(string partId)
        {
            int index = FindPartIndex(partId);
            return index >= 0 && _partHp[index] == 0;
        }

        public bool IsPartActive(string partId)
        {
            int index = FindPartIndex(partId);
            return index >= 0
                && !_completed
                && _partGroups[index] == _activeGroupIndex
                && _partHp[index] > 0;
        }

        /// <summary>
        /// Returns the attrition-adjusted opening density exactly once after the
        /// final group activates. Zero means the opening was already consumed or
        /// the core battle has not started.
        /// </summary>
        public int ConsumeCoreOpeningWays()
        {
            if (!CoreOpeningPending)
                return 0;
            _coreOpeningConsumed = true;
            return CoreOpeningWays;
        }

        public void Step(IReadOnlyList<WarshipDamageCommand> damageCommands)
        {
            BeginTick();
            if (!_tickOpen)
                return;
            if (_activeGroupIndex >= 0 && damageCommands != null)
                for (int i = 0; i < damageCommands.Count; i++)
                {
                    WarshipDamageCommand command = damageCommands[i];
                    ApplyDamage(in command);
                }
            CompleteTick();
        }

        /// <summary>
        /// Opens one encounter tick. BattleSim uses the split tick API so every
        /// gameplay collision in that BattleSim tick is resolved before the
        /// attrition timer advances to the final group.
        /// </summary>
        public void BeginTick()
        {
            if (_tickOpen)
                throw new InvalidOperationException(
                    "The current warship tick is already open.");
            _eventCount = 0;
            if (_completed)
                return;
            if (_tick == int.MaxValue)
                throw new InvalidOperationException(
                    "A warship encounter cannot exceed Int32.MaxValue ticks.");

            _tick++;
            AdvanceScrollForCurrentAct();
            if (!_warningEmitted)
            {
                _warningEmitted = true;
                Emit(
                    SimEventType.WarshipWarningStarted,
                    _definition.OriginX,
                    _definition.OriginY,
                    _definition.WarningTicks,
                    null);
            }
            if (_activeGroupIndex < 0
                && _tick >= _definition.WarningTicks)
                ActivateGroup(0);
            else if (_activeGroupIndex >= 0)
            {
                _activeGroupElapsedTicks++;
                int travel = _definition.Groups[_activeGroupIndex]
                    .AnchorTravelTicks;
                if (_anchorElapsedTicks < travel)
                    _anchorElapsedTicks++;
            }

            _tickOpen = true;
            RefreshPartView();
        }

        public void ApplyDamage(in WarshipDamageCommand command)
        {
            if (!_tickOpen)
                throw new InvalidOperationException(
                    "Warship damage must be applied inside an open tick.");
            ApplyDamageCore(in command);
            RefreshPartView();
        }

        public void CompleteTick()
        {
            if (_completed)
            {
                _tickOpen = false;
                RefreshPartView();
                return;
            }
            if (!_tickOpen)
                return;
            // 소모전은 **네 문을 다 부숴야** 넘어간다 (사람 지시 2026-08-04:
            // "아래 포대 4개가 다 부서지지도 않았는데 끝나버리는데 다 부숴야
            // 페이즈 3으로 이동하도록 고쳐줘").
            //
            // 파괴는 ApplyDamageCore에서 IsGroupDestroyed로 이미 처리한다 —
            // 여기 남은 타이머는 **교착 방지 안전장치**다. 포탑 하나가 닿지 않는
            // 자리에 서면 전투가 영원히 끝나지 않으므로 완전히 없애지는 않는다.
            // 다만 정상 경로가 되면 안 되므로 데이터의 주기를 넉넉히 잡아야 한다.
            if (!_completed
                && _activeGroupIndex >= 0
                && _definition.Groups[_activeGroupIndex].Role
                    == WarshipGroupRole.AttritionLine
                && _activeGroupElapsedTicks
                    >= _definition.Groups[_activeGroupIndex]
                        .AdvanceAfterTicks)
                ActivateGroup(_activeGroupIndex + 1);

            _tickOpen = false;
            RefreshPartView();
        }

        public WarshipEncounterSuspendData CaptureSuspendData()
        {
            if (_tickOpen)
                throw new InvalidOperationException(
                    "Warship suspend capture requires a completed tick.");
            return new WarshipEncounterSuspendData
            {
                schemaVersion =
                    WarshipEncounterSuspendData.CurrentSchemaVersion,
                encounterId = _definition.EncounterId,
                tick = _tick,
                scrollOffset = _scrollOffset,
                scrollRemainder = _scrollRemainder,
                activeGroupIndex = _activeGroupIndex,
                activeGroupElapsedTicks = _activeGroupElapsedTicks,
                anchorFromY = _anchorFromY,
                anchorTargetY = _anchorTargetY,
                anchorElapsedTicks = _anchorElapsedTicks,
                destroyedAttritionParts = _destroyedAttritionParts,
                warningEmitted = _warningEmitted,
                midbossDefeated = _midbossDefeated,
                completed = _completed,
                partHp = (int[])_partHp.Clone(),
                coreOpeningConsumed = _coreOpeningConsumed
            };
        }

        public static WarshipEncounter Restore(
            WarshipEncounterDefinition definition,
            IReadOnlyList<BossPartDefinition> parts,
            WarshipEncounterSuspendData data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            var encounter = new WarshipEncounter(definition, parts);
            encounter.RestoreCore(data);
            return encounter;
        }

        void RestoreCore(WarshipEncounterSuspendData data)
        {
            if (data.schemaVersion
                    != WarshipEncounterSuspendData.CurrentSchemaVersion)
                throw new ArgumentException(
                    "Unsupported warship suspend schema.", nameof(data));
            if (!string.Equals(
                    data.encounterId,
                    _definition.EncounterId,
                    StringComparison.Ordinal))
                throw new ArgumentException(
                    "Warship suspend encounter id does not match content.",
                    nameof(data));
            if (data.tick < 0
                || data.scrollOffset < 0
                || data.scrollRemainder < 0
                || data.scrollRemainder
                    >= _definition.ScrollSpeedDenominator
                || data.activeGroupIndex < -1
                || data.activeGroupIndex >= _definition.Groups.Count
                || data.activeGroupElapsedTicks < 0
                || data.destroyedAttritionParts < 0
                || data.destroyedAttritionParts > TotalAttritionParts
                || data.partHp == null
                || data.partHp.Length != _partHp.Length)
                throw new ArgumentException(
                    "Warship suspend state is outside valid bounds.",
                    nameof(data));
            for (int i = 0; i < _partHp.Length; i++)
            {
                if (data.partHp[i] < 0
                    || data.partHp[i] > _parts[i].MaxHp)
                    throw new ArgumentException(
                        "Warship suspend part HP is outside valid bounds.",
                        nameof(data));
                _partHp[i] = data.partHp[i];
            }

            int destroyedAttrition = CountDestroyedAttritionParts();
            if (destroyedAttrition != data.destroyedAttritionParts)
                throw new ArgumentException(
                    "Warship suspend turret count does not match part HP.",
                    nameof(data));
            if (data.completed
                && data.activeGroupIndex
                    != _definition.Groups.Count - 1)
                throw new ArgumentException(
                    "A completed warship must remain on its final group.",
                    nameof(data));
            if (data.coreOpeningConsumed
                && data.activeGroupIndex
                    != _definition.Groups.Count - 1)
                throw new ArgumentException(
                    "A consumed core opening requires the final group.",
                    nameof(data));

            _tick = data.tick;
            _scrollOffset = data.scrollOffset;
            _scrollRemainder = data.scrollRemainder;
            _activeGroupIndex = data.activeGroupIndex;
            _activeGroupElapsedTicks = data.activeGroupElapsedTicks;
            _anchorFromY = data.anchorFromY;
            _anchorTargetY = data.anchorTargetY;
            _anchorElapsedTicks = data.anchorElapsedTicks;
            _destroyedAttritionParts = data.destroyedAttritionParts;
            _warningEmitted = data.warningEmitted;
            _midbossDefeated = data.midbossDefeated;
            _completed = data.completed;
            _coreOpeningConsumed = data.coreOpeningConsumed;
            _tickOpen = false;
            _eventCount = 0;
            RefreshPartView();
        }

        void AdvanceScrollForCurrentAct()
        {
            if (_activeGroupIndex < 0
                || _definition.Groups[_activeGroupIndex].Role
                    == WarshipGroupRole.MidbossGate)
            {
                AdvanceScrollToHold();
                return;
            }
            if (_definition.Groups[_activeGroupIndex].Role
                    == WarshipGroupRole.AttritionLine)
            {
                AdvanceAttritionScroll();
                return;
            }
            if (_definition.Groups[_activeGroupIndex].Role
                    == WarshipGroupRole.FinalCore)
                ScrollTowardHold();
        }

        void AdvanceScrollToHold()
        {
            long holdOffset = (long)_definition.OriginX
                - _definition.HoldX;
            AdvanceScrollUpTo(holdOffset);
        }

        void AdvanceAttritionScroll()
        {
            long maximumOffset = MaximumVisibleAttritionScrollOffset();
            AdvanceScrollUpTo(maximumOffset);
        }

        void AdvanceScrollUpTo(long maximumOffset)
        {
            if (_scrollOffset >= maximumOffset)
            {
                _scrollRemainder = 0;
                return;
            }
            long total = _scrollRemainder
                + _definition.ScrollSpeedNumerator;
            long nextOffset = _scrollOffset + total
                / _definition.ScrollSpeedDenominator;
            if (nextOffset >= maximumOffset)
            {
                _scrollOffset = maximumOffset;
                _scrollRemainder = 0;
            }
            else
            {
                _scrollOffset = nextOffset;
                _scrollRemainder = total
                    % _definition.ScrollSpeedDenominator;
            }
        }

        /// <summary>
        /// 소모전 막에서 함체가 흘러갈 수 있는 한계.
        ///
        /// **지금 막의 파츠만 보면 안 된다.** 예전에는 그랬고, 그래서 소모전이
        /// 끝났을 때 함수(코어)가 이미 화면 왼쪽 밖에 있었다. 그 사실은
        /// 마지막 막 시작에서 좌표를 순간이동시켜 가리고 있었을 뿐이다 —
        /// 순간이동을 없애자(사람 보고 2026-08-04 "갑자기 워프를 해버려")
        /// 코어가 화면 밖에 선 채로 막이 열리는 것이 드러났다.
        ///
        /// 그래서 **앞으로 상대할 파츠까지** 함께 본다. 함체는 뒤에 나올 부위가
        /// 화면에 남는 선까지만 흘러간다.
        /// </summary>
        long MaximumVisibleAttritionScrollOffset()
        {
            long maximumOffset = long.MaxValue;
            for (int partIndex = 0; partIndex < _parts.Count; partIndex++)
            {
                if (_partGroups[partIndex] < _activeGroupIndex)
                    continue;                    // 이미 지나온 막의 부위
                if (_partHp[partIndex] == 0)
                    continue;
                long partMaximum = (long)_definition.OriginX
                    + _parts[partIndex].OffsetX
                    + SimSpace.PlayfieldHalfWidthSubUnits;
                if (partMaximum < maximumOffset)
                    maximumOffset = partMaximum;
            }
            return maximumOffset;
        }

        void ApplyDamageCore(in WarshipDamageCommand command)
        {
            int partIndex = FindPartIndex(command.PartId);
            if (partIndex < 0
                || _partGroups[partIndex] != _activeGroupIndex
                || _partHp[partIndex] == 0)
                return;

            int previousHp = _partHp[partIndex];
            _partHp[partIndex] = Damage.ApplyToHp(
                previousHp,
                command.Damage);
            if (_partHp[partIndex] != 0)
                return;

            WarshipPartGroupDefinition group =
                _definition.Groups[_activeGroupIndex];
            if (group.Role == WarshipGroupRole.AttritionLine)
                _destroyedAttritionParts++;
            Emit(
                SimEventType.BossPartDestroyed,
                GetPartWorldX(partIndex),
                GetPartWorldY(partIndex),
                _activeGroupIndex,
                _parts[partIndex].PartId);

            if (!IsGroupDestroyed(_activeGroupIndex))
                return;
            if (group.Role == WarshipGroupRole.AttritionLine)
            {
                // 다 부쉈으면 타이머를 기다리지 않는다.
                ActivateGroup(_activeGroupIndex + 1);
                return;
            }
            if (group.Role == WarshipGroupRole.MidbossGate)
            {
                _midbossDefeated = true;
                Emit(
                    SimEventType.MidBossDefeated,
                    GetPartWorldX(partIndex),
                    GetPartWorldY(partIndex),
                    _tick,
                    group.GroupId);
                ActivateGroup(_activeGroupIndex + 1);
            }
            else if (group.Role == WarshipGroupRole.FinalCore)
            {
                _completed = true;
                Emit(
                    SimEventType.StageCleared,
                    GetPartWorldX(partIndex),
                    GetPartWorldY(partIndex),
                    _destroyedAttritionParts,
                    group.GroupId);
            }
        }

        void ActivateGroup(int groupIndex)
        {
            if (groupIndex < 0
                || groupIndex >= _definition.Groups.Count)
                throw new InvalidOperationException(
                    "Warship group activation exceeded its definition.");
            WarshipPartGroupDefinition group =
                _definition.Groups[groupIndex];
            // 첫 막은 **이미 그 자리에서 등장한다.** 예전에는 앵커 0에서 시작해
            // 1막이 열리는 프레임에 목표(-4.5)로 즉시 튀었다 — 이동 시간이 0이라
            // 보간이 없다. 사람이 "처음 등장할때 또 위치가 툭 끊기듯 바뀌네.
            // 첫 페이즈 공격하려고 위치 잡은 위치를 기준으로 등장해줘"라고 한
            // 것이 이것이다.
            //
            // 두 번째 막부터는 지금 있는 자리에서 출발한다 — 막이 이동 도중에
            // 끝날 수 있으므로 목표가 아니라 실제 위치가 기준이어야 한다.
            _anchorFromY = groupIndex == 0
                ? group.AnchorOffsetY
                : AnchorOffsetY;
            _activeGroupIndex = groupIndex;
            _activeGroupElapsedTicks = 0;
            _anchorElapsedTicks = 0;
            _anchorTargetY = group.AnchorOffsetY;
            Emit(
                SimEventType.WarshipGroupActivated,
                WorldX,
                _definition.OriginY,
                groupIndex,
                group.GroupId);
            if (group.Role == WarshipGroupRole.FinalCore)
                Emit(
                    SimEventType.WarshipCoreBattleStarted,
                    WorldX,
                    _definition.OriginY,
                    CoreOpeningWays,
                    group.GroupId);
        }

        void Emit(
            SimEventType type,
            int x,
            int y,
            int arg,
            string partId)
        {
            if (_eventCount >= _eventBuffer.Length)
                throw new InvalidOperationException(
                    "Warship event capacity was exceeded.");
            _eventBuffer[_eventCount++] = new SimEvent(
                type,
                _definition.EventEntityId,
                x,
                y,
                arg,
                partId);
        }

        bool IsGroupDestroyed(int groupIndex)
        {
            WarshipPartGroupDefinition group =
                _definition.Groups[groupIndex];
            for (int i = 0; i < group.PartIds.Count; i++)
            {
                int partIndex = FindPartIndex(group.PartIds[i]);
                if (_partHp[partIndex] != 0)
                    return false;
            }
            return true;
        }

        int CountDestroyedAttritionParts()
        {
            int count = 0;
            WarshipPartGroupDefinition group = _definition.Groups[1];
            for (int i = 0; i < group.PartIds.Count; i++)
                if (_partHp[FindPartIndex(group.PartIds[i])] == 0)
                    count++;
            return count;
        }

        void RefreshPartView()
        {
            for (int i = 0; i < _partView.Length; i++)
            {
                WarshipPartGroupDefinition group =
                    _definition.Groups[_partGroups[i]];
                _partView[i] = new WarshipPartState(
                    _parts[i].PartId,
                    group.GroupId,
                    group.Role,
                    GetPartWorldX(i),
                    GetPartWorldY(i),
                    _partHp[i],
                    _parts[i].MaxHp,
                    !_completed
                        && _partGroups[i] == _activeGroupIndex);
            }
        }

        int GetPartWorldX(int partIndex)
        {
            long world = (long)_definition.OriginX
                + _parts[partIndex].OffsetX
                - _scrollOffset;
            return world < int.MinValue
                ? int.MinValue
                : world > int.MaxValue ? int.MaxValue : (int)world;
        }

        int GetPartWorldY(int partIndex)
        {
            long world = (long)_definition.OriginY
                + AnchorOffsetY
                + _parts[partIndex].OffsetY;
            return world < int.MinValue
                ? int.MinValue
                : world > int.MaxValue ? int.MaxValue : (int)world;
        }

        /// <summary>
        /// 마지막 막에서 정박점으로 **되돌아간다**. 예전에는 그 자리에서 좌표를 바꿔
        /// 버려서(SetAtHoldX) 함체가 한 프레임에 순간이동했다 — 사람 보고 2026-08-04:
        /// "부위가 파괴되면 전함이 천천히 이동해야하는데 갑자기 워프를 해버려."
        ///
        /// 소모전 막에서 함체는 살아 있는 파츠가 화면 끝에 닿을 때까지 왼쪽으로
        /// 흘러간다. 그래서 마지막 막이 열릴 때 정박점은 **오른쪽**에 있고, 지금까지의
        /// 스크롤과 반대 방향으로 움직여야 한다. 속도는 같은 값을 쓰고 나머지도
        /// 그대로 굴려 정수 정확도를 지킨다.
        /// </summary>
        void ScrollTowardHold()
        {
            long holdOffset = (long)_definition.OriginX - _definition.HoldX;
            if (_scrollOffset == holdOffset)
            {
                _scrollRemainder = 0;
                return;
            }
            if (_scrollOffset < holdOffset)
            {
                AdvanceScrollUpTo(holdOffset);
                return;
            }
            long total = _scrollRemainder + _definition.ScrollSpeedNumerator;
            long step = total / _definition.ScrollSpeedDenominator;
            long nextOffset = _scrollOffset - step;
            if (nextOffset <= holdOffset)
            {
                _scrollOffset = holdOffset;
                _scrollRemainder = 0;
                return;
            }
            _scrollOffset = nextOffset;
            _scrollRemainder = total % _definition.ScrollSpeedDenominator;
        }

        static int SaturateToInt(long value)
        {
            return value < int.MinValue
                ? int.MinValue
                : value > int.MaxValue ? int.MaxValue : (int)value;
        }

        int FindGroupIndex(string partId)
        {
            for (int group = 0;
                group < _definition.Groups.Count;
                group++)
                for (int member = 0;
                    member < _definition.Groups[group].PartIds.Count;
                    member++)
                    if (string.Equals(
                            _definition.Groups[group].PartIds[member],
                            partId,
                            StringComparison.Ordinal))
                        return group;
            return -1;
        }

        int FindPartIndex(string partId)
        {
            if (partId == null)
                return -1;
            for (int i = 0; i < _parts.Count; i++)
                if (string.Equals(
                        _parts[i].PartId,
                        partId,
                        StringComparison.Ordinal))
                    return i;
            return -1;
        }
    }
}
