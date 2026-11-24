using System.Collections.Generic;
using UnityEngine;

namespace Defense.Units
{
    /// <summary>모든 UnitData를 모아두는 목록. 소환/업그레이드의 랜덤 뽑기가 여기서 일어난다.</summary>
    [CreateAssetMenu(fileName = "UnitDatabase", menuName = "Defense/Unit Database")]
    public class UnitDatabase : ScriptableObject
    {
        [SerializeField] List<UnitData> _units = new List<UnitData>();

        Dictionary<UnitGrade, List<UnitData>> _byGrade;

        public IReadOnlyList<UnitData> All => _units;

        /// <summary>유닛 등급을 수정한 뒤 등급별 소환 목록을 다시 구성하도록 한다.</summary>
        public void InvalidateCache() => _byGrade = null;

        void OnValidate()
        {
            InvalidateCache(); // 에디터에서 목록이 바뀌면 다시 구성

            // id는 로비 레벨 저장 키라서 중복되면 안 된다. (에셋을 복제하면 id도 같이 복제되니 주의)
            var seen = new HashSet<string>();
            foreach (UnitData unit in _units)
            {
                if (unit == null) continue;

                if (seen.Add(unit.Id) == false)
                    Debug.LogError($"[UnitDatabase] id가 중복되었습니다: '{unit.Id}' ({unit.name}). 유닛마다 달라야 합니다.", this);
            }
        }

        void Build()
        {
            _byGrade = new Dictionary<UnitGrade, List<UnitData>>();
            foreach (UnitData unit in _units)
            {
                if (unit == null) continue;

                if (_byGrade.TryGetValue(unit.Grade, out List<UnitData> list) == false)
                {
                    list = new List<UnitData>();
                    _byGrade[unit.Grade] = list;
                }
                list.Add(unit);
            }
        }

        /// <summary>해당 등급의 유닛이 하나라도 등록되어 있으면 true.</summary>
        public bool HasUnits(UnitGrade grade)
        {
            if (_byGrade == null) Build();

            return _byGrade.TryGetValue(grade, out List<UnitData> list) && list.Count > 0;
        }

        /// <summary>해당 등급의 유닛 중 균등 확률로 하나를 뽑는다. 없으면 null.</summary>
        public UnitData GetRandom(UnitGrade grade)
        {
            if (_byGrade == null) Build();

            if (_byGrade.TryGetValue(grade, out List<UnitData> list) == false || list.Count == 0)
            {
                Debug.LogError($"[UnitDatabase] {grade} 등급 유닛이 등록되어 있지 않습니다.", this);
                return null;
            }
            return list[Random.Range(0, list.Count)];
        }
    }
}
