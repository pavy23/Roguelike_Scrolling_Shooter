using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Shmup.Core.Generation
{
    /// <summary>
    /// Theme-neutral progression roles for a three-act persistent multipart
    /// encounter. The names describe simulation semantics rather than art.
    /// </summary>
    public enum WarshipGroupRole
    {
        MidbossGate = 0,
        AttritionLine = 1,
        FinalCore = 2
    }

    /// <summary>
    /// Ordered group of sibling boss parts. A zero duration means every part must
    /// be destroyed; AttritionLine instead advances after its positive duration.
    /// </summary>
    public sealed class WarshipPartGroupDefinition
    {
        readonly ReadOnlyCollection<string> _partIds;

        public WarshipPartGroupDefinition(
            string groupId,
            WarshipGroupRole role,
            IReadOnlyList<string> partIds,
            int advanceAfterTicks)
        {
            if (string.IsNullOrEmpty(groupId))
                throw new ArgumentException(
                    "Warship group id cannot be null or empty.",
                    nameof(groupId));
            if (!Enum.IsDefined(typeof(WarshipGroupRole), role))
                throw new ArgumentOutOfRangeException(nameof(role));
            if (advanceAfterTicks < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(advanceAfterTicks));
            if (role == WarshipGroupRole.AttritionLine
                && advanceAfterTicks < 1)
                throw new ArgumentException(
                    "The attrition line requires a positive duration.",
                    nameof(advanceAfterTicks));
            if (role != WarshipGroupRole.AttritionLine
                && advanceAfterTicks != 0)
                throw new ArgumentException(
                    "Only the attrition line may advance on a timer.",
                    nameof(advanceAfterTicks));

            int count = partIds == null ? 0 : partIds.Count;
            if (count == 0)
                throw new ArgumentException(
                    "A warship group requires at least one part.",
                    nameof(partIds));
            var copy = new string[count];
            for (int i = 0; i < copy.Length; i++)
            {
                string partId = partIds[i];
                if (string.IsNullOrEmpty(partId))
                    throw new ArgumentException(
                        "Warship group part ids cannot be null or empty.",
                        nameof(partIds));
                for (int earlier = 0; earlier < i; earlier++)
                    if (string.Equals(
                            copy[earlier],
                            partId,
                            StringComparison.Ordinal))
                        throw new ArgumentException(
                            $"Duplicate warship group part id '{partId}'.",
                            nameof(partIds));
                copy[i] = partId;
            }

            GroupId = groupId;
            Role = role;
            AdvanceAfterTicks = advanceAfterTicks;
            _partIds = new ReadOnlyCollection<string>(copy);
        }

        public string GroupId { get; }
        public WarshipGroupRole Role { get; }
        public IReadOnlyList<string> PartIds => _partIds;
        public int AdvanceAfterTicks { get; }
    }

    /// <summary>
    /// Immutable waves.json contract for a persistent three-act multipart
    /// encounter. Positions remain boss-local; runtime scroll is applied exactly.
    /// HoldX is the shared midboss/core anchor inherited from the owning boss.
    /// </summary>
    public sealed class WarshipEncounterDefinition
    {
        readonly ReadOnlyCollection<WarshipPartGroupDefinition> _groups;

        public WarshipEncounterDefinition(
            string encounterId,
            int eventEntityId,
            int warningTicks,
            int originX,
            int originY,
            int holdX,
            int scrollSpeedNumerator,
            int scrollSpeedDenominator,
            int baseCoreOpeningWays,
            int waysReductionPerTurret,
            int minimumCoreOpeningWays,
            IReadOnlyList<WarshipPartGroupDefinition> groups,
            IReadOnlyList<BossPartDefinition> parts)
        {
            if (string.IsNullOrEmpty(encounterId))
                throw new ArgumentException(
                    "Warship encounter id cannot be null or empty.",
                    nameof(encounterId));
            if (eventEntityId < 0)
                throw new ArgumentOutOfRangeException(nameof(eventEntityId));
            if (warningTicks < 0)
                throw new ArgumentOutOfRangeException(nameof(warningTicks));
            if (holdX > originX)
                throw new ArgumentOutOfRangeException(
                    nameof(holdX),
                    "Warship hold X cannot be to the right of its origin X.");
            if (scrollSpeedNumerator < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(scrollSpeedNumerator));
            if (scrollSpeedDenominator < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(scrollSpeedDenominator));
            if (baseCoreOpeningWays < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(baseCoreOpeningWays));
            if (waysReductionPerTurret < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(waysReductionPerTurret));
            if (minimumCoreOpeningWays < 1
                || minimumCoreOpeningWays > baseCoreOpeningWays)
                throw new ArgumentOutOfRangeException(
                    nameof(minimumCoreOpeningWays));
            if (groups == null || groups.Count != 3)
                throw new ArgumentException(
                    "A warship encounter requires exactly three groups.",
                    nameof(groups));
            if (parts == null || parts.Count == 0)
                throw new ArgumentException(
                    "A warship encounter requires multipart boss parts.",
                    nameof(parts));

            var groupCopy = new WarshipPartGroupDefinition[groups.Count];
            for (int i = 0; i < groupCopy.Length; i++)
                groupCopy[i] = groups[i] ?? throw new ArgumentException(
                    "Warship groups cannot contain null.", nameof(groups));
            if (groupCopy[0].Role != WarshipGroupRole.MidbossGate
                || groupCopy[1].Role != WarshipGroupRole.AttritionLine
                || groupCopy[2].Role != WarshipGroupRole.FinalCore)
                throw new ArgumentException(
                    "Warship groups must be midbossGate, attritionLine, finalCore.",
                    nameof(groups));

            ValidatePartPartition(groupCopy, parts);

            EncounterId = encounterId;
            EventEntityId = eventEntityId;
            WarningTicks = warningTicks;
            OriginX = originX;
            OriginY = originY;
            HoldX = holdX;
            ScrollSpeedNumerator = scrollSpeedNumerator;
            ScrollSpeedDenominator = scrollSpeedDenominator;
            BaseCoreOpeningWays = baseCoreOpeningWays;
            WaysReductionPerTurret = waysReductionPerTurret;
            MinimumCoreOpeningWays = minimumCoreOpeningWays;
            _groups = new ReadOnlyCollection<WarshipPartGroupDefinition>(
                groupCopy);
        }

        public string EncounterId { get; }
        public int EventEntityId { get; }
        public int WarningTicks { get; }
        public int OriginX { get; }
        public int OriginY { get; }
        public int HoldX { get; }
        public int ScrollSpeedNumerator { get; }
        public int ScrollSpeedDenominator { get; }
        public int BaseCoreOpeningWays { get; }
        public int WaysReductionPerTurret { get; }
        public int MinimumCoreOpeningWays { get; }
        public IReadOnlyList<WarshipPartGroupDefinition> Groups => _groups;

        internal void ValidateParts(IReadOnlyList<BossPartDefinition> parts)
        {
            if (parts == null || parts.Count == 0)
                throw new ArgumentException(
                    "A warship encounter requires multipart boss parts.",
                    nameof(parts));
            ValidatePartPartition(_groups, parts);
        }

        static void ValidatePartPartition(
            IReadOnlyList<WarshipPartGroupDefinition> groups,
            IReadOnlyList<BossPartDefinition> parts)
        {
            int referenced = 0;
            for (int group = 0; group < groups.Count; group++)
            {
                WarshipPartGroupDefinition definition = groups[group];
                for (int member = 0; member < definition.PartIds.Count; member++)
                {
                    string partId = definition.PartIds[member];
                    int partIndex = FindPart(parts, partId);
                    if (partIndex < 0)
                        throw new ArgumentException(
                            $"Warship group references unknown part '{partId}'.",
                            nameof(groups));
                    for (int earlierGroup = 0;
                        earlierGroup <= group;
                        earlierGroup++)
                    {
                        int memberLimit = earlierGroup == group
                            ? member
                            : groups[earlierGroup].PartIds.Count;
                        for (int earlierMember = 0;
                            earlierMember < memberLimit;
                            earlierMember++)
                            if (string.Equals(
                                    groups[earlierGroup]
                                        .PartIds[earlierMember],
                                    partId,
                                    StringComparison.Ordinal))
                                throw new ArgumentException(
                                    $"Warship part '{partId}' belongs to multiple groups.",
                                    nameof(groups));
                    }
                    bool finalCore = group == groups.Count - 1;
                    if (parts[partIndex].IsCore != finalCore)
                        throw new ArgumentException(
                            finalCore
                                ? "Every finalCore group part must be a boss core."
                                : "Only finalCore group parts may be boss cores.",
                            nameof(groups));
                    referenced++;
                }
            }
            if (referenced != parts.Count)
                throw new ArgumentException(
                    "Every multipart boss part must belong to one warship group.",
                    nameof(groups));
        }

        static int FindPart(
            IReadOnlyList<BossPartDefinition> parts,
            string partId)
        {
            for (int i = 0; i < parts.Count; i++)
                if (string.Equals(
                        parts[i].PartId,
                        partId,
                        StringComparison.Ordinal))
                    return i;
            return -1;
        }
    }
}
