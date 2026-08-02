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
        public const int CurrentSchemaVersion = 2;

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
            RefreshPartView();
        }

        public WarshipEncounterDefinition Definition => _definition;
        public int Tick => _tick;
        public long ScrollOffset => _scrollOffset;
        public long ScrollRemainder => _scrollRemainder;
        public int ActiveGroupIndex => _activeGroupIndex;
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
            AdvanceScroll();
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
                _activeGroupElapsedTicks++;

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
            _destroyedAttritionParts = data.destroyedAttritionParts;
            _warningEmitted = data.warningEmitted;
            _midbossDefeated = data.midbossDefeated;
            _completed = data.completed;
            _coreOpeningConsumed = data.coreOpeningConsumed;
            _tickOpen = false;
            _eventCount = 0;
            RefreshPartView();
        }

        void AdvanceScroll()
        {
            long total = _scrollRemainder
                + _definition.ScrollSpeedNumerator;
            _scrollOffset += total
                / _definition.ScrollSpeedDenominator;
            _scrollRemainder = total
                % _definition.ScrollSpeedDenominator;
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
            _activeGroupIndex = groupIndex;
            _activeGroupElapsedTicks = 0;
            WarshipPartGroupDefinition group =
                _definition.Groups[groupIndex];
            Emit(
                SimEventType.WarshipGroupActivated,
                _definition.OriginX - SaturatingScrollOffset(),
                _definition.OriginY,
                groupIndex,
                group.GroupId);
            if (group.Role == WarshipGroupRole.FinalCore)
                Emit(
                    SimEventType.WarshipCoreBattleStarted,
                    _definition.OriginX - SaturatingScrollOffset(),
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
                + _parts[partIndex].OffsetY;
            return world < int.MinValue
                ? int.MinValue
                : world > int.MaxValue ? int.MaxValue : (int)world;
        }

        int SaturatingScrollOffset()
        {
            return _scrollOffset > int.MaxValue
                ? int.MaxValue
                : (int)_scrollOffset;
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
