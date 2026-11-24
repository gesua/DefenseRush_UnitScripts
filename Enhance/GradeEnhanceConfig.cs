using System;
using System.Collections.Generic;
using UnityEngine;

namespace Defense.Units
{
    /// <summary>
    /// 인게임 강화(등급별) 설정. 등급마다 레벨별 보너스 표와 강화 비용 표를 가진다.
    /// 일부 등급은 묶어서(예: 일반+희귀) 레벨을 같이 올릴 수 있다. 이때 비용 표는 묶음의 첫 등급 항목을 쓰고,
    /// 보너스 표는 등급마다 자기 항목을 쓴다.
    /// Create > Defense > Grade Enhance Config 로 하나만 만든다.
    /// </summary>
    [CreateAssetMenu(fileName = "GradeEnhanceConfig", menuName = "Defense/Grade Enhance Config")]
    public class GradeEnhanceConfig : ScriptableObject
    {
        [Serializable]
        class GradeEntry
        {
            public UnitGrade Grade;

            [Tooltip("인덱스 = 레벨 - 1. 인게임 레벨 N일 때 해당 등급 유닛 전체에 더해지는 누적 보너스. 레벨 1 칸은 보통 0.")]
            public StatBonus[] Bonuses = new StatBonus[UnitLevel.MAX];

            [Tooltip("인덱스 = 현재 레벨 - 1. 현재 레벨에서 다음 레벨로 올리는 비용. (1→2 ... 14→15, 14칸)")]
            public int[] UpgradeCosts = new int[UnitLevel.MAX - 1];
        }

        [Serializable]
        class GradeGroup
        {
            [Tooltip("레벨을 같이 올리는 등급들. 비용 표는 첫 등급의 항목을 쓴다.")]
            public UnitGrade[] Grades;
        }

        // 묶음에 없는 등급은 혼자 하나의 묶음이다. (인덱스 = 등급)
        static readonly UnitGrade[][] _singleGroups = NewSingleGroups();

        [SerializeField] GradeEntry[] _entries = new GradeEntry[0];

        [Tooltip("레벨과 강화 비용을 같이 쓰는 등급 묶음. 묶음에 없는 등급은 혼자 하나의 묶음이 된다. (기본: 일반+희귀)")]
        [SerializeField] GradeGroup[] _groups = NewDefaultGroups();

        static UnitGrade[][] NewSingleGroups()
        {
            var singles = new UnitGrade[UnitGrades.Count][];
            for (int i = 0; i < singles.Length; i++) singles[i] = new[] { (UnitGrade)i };
            return singles;
        }

        static GradeGroup[] NewDefaultGroups() => new[]
        {
            new GradeGroup { Grades = new[] { UnitGrade.Common, UnitGrade.Rare } }
        };

        // 등급마다 항목이 하나씩, 표 길이는 항상 레벨 규칙에 맞게 유지한다.
        void OnValidate()
        {
            int gradeCount = UnitGrades.Count;
            if (_entries == null) _entries = new GradeEntry[0];
            if (_entries.Length != gradeCount) Array.Resize(ref _entries, gradeCount);

            for (int i = 0; i < gradeCount; i++)
            {
                // 새로 생긴 등급 항목은 직전 등급의 표를 복사해서 시작한다. (숫자는 직접 조절)
                if (_entries[i] == null) _entries[i] = (i > 0 && _entries[i - 1] != null) ? CopyOf(_entries[i - 1]) : new GradeEntry();

                GradeEntry entry = _entries[i];
                entry.Grade = (UnitGrade)i;
                if (entry.Bonuses == null || entry.Bonuses.Length != UnitLevel.MAX)
                    Array.Resize(ref entry.Bonuses, UnitLevel.MAX);
                if (entry.UpgradeCosts == null || entry.UpgradeCosts.Length != UnitLevel.MAX - 1)
                    Array.Resize(ref entry.UpgradeCosts, UnitLevel.MAX - 1);
            }
        }

        static GradeEntry CopyOf(GradeEntry source) => new GradeEntry
        {
            Bonuses = (source.Bonuses != null) ? (StatBonus[])source.Bonuses.Clone() : null,
            UpgradeCosts = (source.UpgradeCosts != null) ? (int[])source.UpgradeCosts.Clone() : null
        };

        /// <summary>레벨을 같이 올리는 등급들(자기 자신 포함). 묶음에 없으면 자기 혼자.</summary>
        public IReadOnlyList<UnitGrade> GetGroup(UnitGrade grade)
        {
            if (_groups != null)
            {
                foreach (GradeGroup group in _groups)
                    if (group != null && group.Grades != null && Array.IndexOf(group.Grades, grade) >= 0 && group.Grades.Length > 0)
                        return group.Grades;
            }
            return _singleGroups[(int)grade];
        }

        GradeEntry Find(UnitGrade grade)
        {
            if (_entries == null) return null;

            foreach (GradeEntry entry in _entries)
                if (entry != null && entry.Grade == grade) return entry;
            return null;
        }

        /// <summary>해당 등급의 인게임 레벨에 대한 누적 보너스. 레벨은 1~15로 보정한다.</summary>
        public StatBonus GetBonus(UnitGrade grade, int level)
        {
            GradeEntry entry = Find(grade);
            if (entry == null || entry.Bonuses == null || entry.Bonuses.Length == 0) return default;

            int index = Mathf.Clamp(UnitLevel.Clamp(level) - 1, 0, entry.Bonuses.Length - 1);
            return entry.Bonuses[index];
        }

        /// <summary>현재 레벨에서 다음 레벨로 올리는 비용. 최고 레벨이거나 표가 없으면 -1.</summary>
        public int GetUpgradeCost(UnitGrade grade, int level)
        {
            GradeEntry entry = Find(GetGroup(grade)[0]); // 묶음이면 첫 등급의 비용 표를 같이 쓴다
            if (entry == null || entry.UpgradeCosts == null) return -1;
            if (level < 1 || level >= UnitLevel.MAX) return -1;
            if (level - 1 >= entry.UpgradeCosts.Length) return -1;

            return entry.UpgradeCosts[level - 1];
        }
    }
}