using System;

namespace Shmup.Core.Simulation
{
    /// <summary>
    /// 탄 하나의 보조 상태 묶음 (정리 7번, 2026-08-07). 예전에는 이 16개
    /// 필드가 각각 별도 List로 존재해 인덱스 동기화를 수동으로 지켰다 —
    /// 추가/삭제에서 한 리스트만 빠지면 전체 탄 데이터가 꼬이는 구조였다.
    /// 한 구조체로 묶어 그 사고 유형을 원천 차단한다.
    /// </summary>
    struct BulletAux
    {
        public int XRemainder;
        public int YRemainder;
        // 적탄 조준 벡터: 서브유닛/틱 = (numX, numY) / den. 플레이어 탄은 den 0 (kind 기반 속도).
        public int VelXNumerator;
        public int VelYNumerator;
        public int VelDenominator;
        public int PiercesRemaining;
        public int RicochetUsed;
        public int HomingTargetId;
        public byte GrazeScored;
        public int SplitAfterTicks;
        public int MineTravelTicks;
        public int MineTelegraphTicks;
        public int AccelerationXNumerator;
        public int AccelerationYNumerator;
        public int AccelerationDenominator;
        public int HomingTurnLutSlotsPerTick;
    }

    /// <summary>
    /// <see cref="BulletAux"/> 전용 리스트. List&lt;T&gt;는 struct 인덱서가
    /// 복사본을 돌려줘 <c>aux[i].X = v</c> 꼴 제자리 쓰기가 불가능하므로,
    /// 배열 백킹 + ref 인덱서로 기존 병렬 리스트 시절의 쓰기 문법을 그대로
    /// 유지한다. 삭제는 List.RemoveAt과 동일한 순서 보존 시프트다 —
    /// 순회 순서가 판정 순서라 스왑 삭제는 결정론을 깬다.
    /// </summary>
    sealed class BulletAuxList
    {
        BulletAux[] _items;
        int _count;

        public BulletAuxList(int capacity)
        {
            _items = new BulletAux[Math.Max(4, capacity)];
        }

        public int Count => _count;

        public ref BulletAux this[int index]
        {
            get
            {
                if ((uint)index >= (uint)_count)
                    throw new ArgumentOutOfRangeException(nameof(index));
                return ref _items[index];
            }
        }

        public void Add(in BulletAux item)
        {
            if (_count == _items.Length)
                Array.Resize(ref _items, _items.Length * 2);
            _items[_count++] = item;
        }

        public void RemoveAt(int index)
        {
            if ((uint)index >= (uint)_count)
                throw new ArgumentOutOfRangeException(nameof(index));
            _count--;
            if (index < _count)
                Array.Copy(
                    _items, index + 1, _items, index, _count - index);
            _items[_count] = default;
        }

        public void RemoveRange(int index, int count)
        {
            if (index < 0 || count < 0 || index + count > _count)
                throw new ArgumentOutOfRangeException(nameof(index));
            int tail = _count - index - count;
            if (tail > 0)
                Array.Copy(
                    _items, index + count, _items, index, tail);
            Array.Clear(_items, _count - count, count);
            _count -= count;
        }
    }
}
