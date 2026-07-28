using System.Runtime.Serialization;

#pragma warning disable CS0649 // Fields are assigned through reflection by the JSON serializer.

namespace Shmup.Core.Content
{
    // DataContractJsonSerializer maps these field names directly to camelCase JSON.
    // Nullable value types let validation distinguish a missing field from zero.
    [DataContract]
    internal sealed class EnemiesDto
    {
        [DataMember]
        public int? schemaVersion;
        [DataMember]
        public DropTableDto dropTable;
        [DataMember]
        public EnemyDto[] enemies;
    }

    [DataContract]
    internal sealed class DropTableDto
    {
        [DataMember]
        public int? noDropWeight;
    }

    [DataContract]
    internal sealed class EnemyDto
    {
        [DataMember]
        public string id;
        [DataMember]
        public string displayName;
        [DataMember]
        public int? hp;
        [DataMember]
        public int? contactDamage;
        [DataMember]
        public int? scoreValue;
        [DataMember]
        public string movePattern;
        [DataMember]
        public decimal? moveSpeed;
        [DataMember]
        public int? fireIntervalTicks;
        [DataMember]
        public int? dropWeight;
        [DataMember]
        public decimal? halfWidth;
        [DataMember]
        public decimal? halfHeight;
        [DataMember]
        public decimal? amplitude;
        [DataMember]
        public int? periodTicks;
    }

    [DataContract]
    internal sealed class WeaponsDto
    {
        [DataMember]
        public int? schemaVersion;
        [DataMember]
        public WeaponDto[] weapons;
    }

    [DataContract]
    internal sealed class WeaponDto
    {
        [DataMember]
        public string id;
        [DataMember]
        public string slot;
        [DataMember]
        public int? baseDamage;
        [DataMember]
        public int? fireIntervalTicks;
        [DataMember]
        public decimal? projectileSpeed;
        [DataMember]
        public decimal? projectileHalfWidth;
        [DataMember]
        public decimal? projectileHalfHeight;
        [DataMember]
        public int? maxLevel;
    }

    [DataContract]
    internal sealed class WavesDto
    {
        [DataMember]
        public int? schemaVersion;
        [DataMember]
        public decimal? scrollSpeed;
        [DataMember]
        public decimal? spawnX;
        [DataMember]
        public int? laneCount;
        [DataMember]
        public int? segmentsPerStage;
        [DataMember]
        public int? startLaneMask;
        [DataMember]
        public SegmentDto[] segments;
        [DataMember]
        public BossDto[] bosses;
    }

    [DataContract]
    internal sealed class SegmentDto
    {
        [DataMember]
        public string id;
        [DataMember]
        public int? difficultyMin;
        [DataMember]
        public int? difficultyMax;
        [DataMember]
        public int? lengthTicks;
        [DataMember]
        public int? entryLaneMask;
        [DataMember]
        public int? exitLaneMask;
        [DataMember]
        public int[] traversableLaneMasks;
        [DataMember]
        public SpawnDto[] spawns;
    }

    [DataContract]
    internal sealed class SpawnDto
    {
        [DataMember]
        public int? tick;
        [DataMember]
        public string enemyId;
        [DataMember]
        public decimal? y;
    }

    [DataContract]
    internal sealed class BossDto
    {
        [DataMember]
        public string id;
        [DataMember]
        public int? stageIndexMin;
        [DataMember]
        public int? stageIndexMax;
        [DataMember]
        public int? difficultyMin;
        [DataMember]
        public int? difficultyMax;
        [DataMember]
        public int? entryLaneMask;
        [DataMember]
        public int? hp;
    }
}

#pragma warning restore CS0649
