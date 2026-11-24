using System.Collections.Generic;
using UnityEngine;

namespace Defense.Units
{
    /// <summary>
    /// 그리드의 한 칸. 같은 종류의 유닛만 최대 maxUnits마리까지 쌓을 수 있다.
    /// </summary>
    public class Slot
    {
        public int Row { get; }
        public int Col { get; }
        public Vector3 Center { get; }

        /// <summary>이 칸이 속한 그리드. 유닛의 스킬이 인접 칸을 찾을 때 쓴다.</summary>
        public GridField Grid { get; }

        readonly int _maxUnits;
        readonly float _stackRadius;
        readonly Quaternion _rotation;
        readonly List<Unit> _units = new List<Unit>();

        public Slot(int row, int col, Vector3 center, int maxUnits, float stackRadius, Quaternion rotation, GridField grid)
        {
            Row = row;
            Col = col;
            Center = center;
            _maxUnits = maxUnits;
            _stackRadius = stackRadius;
            _rotation = rotation;
            Grid = grid;
        }

        public IReadOnlyList<Unit> Units => _units;
        public int Count => _units.Count;
        public bool IsEmpty => _units.Count == 0;
        public bool IsFull => _units.Count >= _maxUnits;

        /// <summary>칸에 쌓인 유닛의 종류. 비어 있으면 null.</summary>
        public UnitData Type => (_units.Count > 0) ? _units[0].Data : null;

        /// <summary>비어 있거나, 같은 종류이면서 자리가 남아 있으면 true.</summary>
        public bool CanAdd(UnitData data) => IsEmpty || (Type == data && IsFull == false);

        public void Add(Unit unit)
        {
            _units.Add(unit);
            unit.Slot = this;
        }

        public void AddRange(IEnumerable<Unit> list)
        {
            foreach (Unit unit in list) Add(unit);
        }

        /// <summary>가장 마지막에 쌓인 유닛 한 마리를 꺼낸다. 없으면 null.</summary>
        public Unit TakeLast()
        {
            if (_units.Count == 0) return null;

            Unit last = _units[_units.Count - 1];
            _units.RemoveAt(_units.Count - 1);
            last.Slot = null;
            return last;
        }

        /// <summary>칸의 유닛 전체를 꺼내고 칸을 비운다.</summary>
        public List<Unit> TakeAll()
        {
            var all = new List<Unit>(_units);
            _units.Clear();
            foreach (Unit unit in all) unit.Slot = null;
            return all;
        }

        /// <summary>
        /// 현재 마릿수에 맞게 유닛 위치를 다시 배치한다.
        /// instantUnit은 즉시 그 자리로, 나머지는 부드럽게 이동한다.
        /// </summary>
        public void Relayout(Unit instantUnit = null)
        {
            for (int i = 0; i < _units.Count; i++)
            {
                Vector3 target = Center + StackOffset(i, _units.Count);
                _units[i].SetRangeCenter(Center); // 같은 칸의 유닛은 모두 칸 중심 기준으로 사거리를 공유한다
                _units[i].MoveTo(target, instant: _units[i] == instantUnit);
            }
        }

        /// <summary>
        /// 기준점(칸 중심, 또는 드래그 중인 포인터 위치)에서 index번째 유닛이 떨어지는 오프셋.
        /// 1마리는 중앙, 2마리 이상은 원형 배치 (2마리: 좌우, 3마리: 삼각형).
        /// </summary>
        public Vector3 StackOffset(int index, int count)
        {
            if (count <= 1) return Vector3.zero;

            float angle = Mathf.PI * 2f * index / count;
            if (count % 2 == 1) angle += Mathf.PI * 0.5f; // 홀수는 한 마리가 앞쪽(+z)을 보도록

            var local = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * _stackRadius;
            return _rotation * local;
        }
    }
}