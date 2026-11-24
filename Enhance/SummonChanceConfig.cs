using System;
using System.Collections.Generic;
using UnityEngine;

namespace Defense.Units
{
    /// <summary>
    /// 소환 확률 강화 설정. 레벨마다 등급별 소환 가중치와, 다음 레벨로 올리는 비용을 가진다.
    /// Create > Defense > Summon Chance Config 로 하나만 만든다.
    /// 표의 줄 수가 곧 최고 레벨이다. (줄 1 = 레벨 1)
    /// </summary>
    [CreateAssetMenu(fileName = "SummonChanceConfig", menuName = "Defense/Summon Chance Config")]
    public class SummonChanceConfig : ScriptableObject
    {
        [Serializable]
        class LevelEntry
        {
            [Tooltip("등급 순서(일반, 희귀, 영웅)대로의 소환 가중치. 합이 100일 필요는 없다.")]
            public float[] Weights = new float[UnitGrades.Count];

            [Tooltip("이 레벨에서 다음 레벨로 올리는 비용. 마지막 레벨은 쓰지 않는다.")]
            public int UpgradeCost;
        }

        [SerializeField] LevelEntry[] _levels = NewDefaultLevels();

        /// <summary>최고 레벨. 표가 비어 있으면 1.</summary>
        public int MaxLevel => (_levels != null && _levels.Length > 0) ? _levels.Length : 1;

        /// <summary>해당 레벨의 등급별 가중치. 레벨은 1~MaxLevel로 보정한다. 표가 없으면 null.</summary>
        public IReadOnlyList<float> GetWeights(int level)
        {
            if (_levels == null || _levels.Length == 0) return null;

            return _levels[Mathf.Clamp(level, 1, _levels.Length) - 1].Weights;
        }

        /// <summary>현재 레벨에서 다음 레벨로 올리는 비용. 최고 레벨이거나 표가 없으면 -1.</summary>
        public int GetUpgradeCost(int level)
        {
            if (_levels == null || level < 1 || level >= _levels.Length) return -1;

            return _levels[level - 1].UpgradeCost;
        }

        // 임시 표(10레벨). 레벨 1은 기본 확률(일반 97.45 / 희귀 1.95 / 영웅 0.5 / 전설 0.1)이고,
        // 마지막 레벨의 확률(희귀 28 / 영웅 12 / 전설 3)까지 일정하게 늘려 간다. 일반은 나머지(합 100).
        static LevelEntry[] NewDefaultLevels()
        {
            const int LEVEL_COUNT = 10;
            const float FIRST_RARE = 1.95f, LAST_RARE = 28f;
            const float FIRST_HERO = 0.5f, LAST_HERO = 12f;
            const float FIRST_LEGENDARY = 0.1f, LAST_LEGENDARY = 3f;

            var levels = new LevelEntry[LEVEL_COUNT];
            for (int i = 0; i < LEVEL_COUNT; i++)
            {
                float t = i / (float)(LEVEL_COUNT - 1);
                float rare = Round2(Mathf.Lerp(FIRST_RARE, LAST_RARE, t));
                float hero = Round2(Mathf.Lerp(FIRST_HERO, LAST_HERO, t));
                float legendary = Round2(Mathf.Lerp(FIRST_LEGENDARY, LAST_LEGENDARY, t));

                var entry = new LevelEntry { UpgradeCost = 30 + 20 * i };
                entry.Weights[(int)UnitGrade.Common] = Round2(100f - rare - hero - legendary);
                entry.Weights[(int)UnitGrade.Rare] = rare;
                entry.Weights[(int)UnitGrade.Hero] = hero;
                entry.Weights[(int)UnitGrade.Legendary] = legendary;
                levels[i] = entry;
            }
            return levels;
        }

        static float Round2(float value) => Mathf.Round(value * 100f) / 100f;

        // 가중치 칸 수는 항상 등급 수에 맞게 유지한다. (등급이 추가되면 새 칸은 0)
        void OnValidate()
        {
            if (_levels == null) return;

            for (int i = 0; i < _levels.Length; i++)
            {
                if (_levels[i] == null) _levels[i] = new LevelEntry();
                if (_levels[i].Weights == null || _levels[i].Weights.Length != UnitGrades.Count)
                    Array.Resize(ref _levels[i].Weights, UnitGrades.Count);
            }
        }
    }
}