using System;
using UnityEngine;

namespace Defense.Units
{
    /// <summary>
    /// 도박 설정. 도박으로 뽑을 수 있는 등급마다 비용, 성공당 비용 증가량, 기본 성공 확률, 유물 보정 여부를 가진다.
    /// Create > Defense > Gamble Config 로 하나만 만든다. 표에 없는 등급은 도박할 수 없다.
    /// </summary>
    [CreateAssetMenu(fileName = "GambleConfig", menuName = "Defense/Gamble Config")]
    public class GambleConfig : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            [SerializeField] UnitGrade _grade;

            [Tooltip("한 번 도박하는 데 드는 전투 재화.")]
            [Min(1)][SerializeField] int _cost = 1;

            [Tooltip("기본 성공 확률(%). 60이면 60%.")]
            [Range(0f, 100f)][SerializeField] float _baseChance;

            [Tooltip("이 등급으로 도박에 성공(유닛을 얻음)할 때마다 이 등급의 비용이 오르는 양. 0이면 오르지 않는다. 실패하면 오르지 않는다.")]
            [Min(0)][SerializeField] int _costIncrease;

            [Tooltip("켜면 아래 유물 효과의 증가분(%p)을 기본 확률에 더한다.")]
            [SerializeField] bool _hasArtifactBonus;

            [Tooltip("기본 확률에 더할 유물 효과. 위의 '유물 보정'을 켠 등급만 쓴다.")]
            [SerializeField] ArtifactEffect _artifactEffect = ArtifactEffect.HeroGambleChance;

            public UnitGrade Grade => _grade;
            public int Cost => _cost;

            /// <summary>기본 성공 확률(%).</summary>
            public float BaseChance => _baseChance;

            /// <summary>성공할 때마다 오르는 비용.</summary>
            public int CostIncrease => _costIncrease;

            public bool HasArtifactBonus => _hasArtifactBonus;
            public ArtifactEffect ArtifactEffect => _artifactEffect;

            public Entry(UnitGrade grade, int cost, float baseChance, bool hasArtifactBonus = false, int costIncrease = 0,
                         ArtifactEffect artifactEffect = ArtifactEffect.HeroGambleChance)
            {
                _grade = grade;
                _cost = cost;
                _baseChance = baseChance;
                _hasArtifactBonus = hasArtifactBonus;
                _costIncrease = costIncrease;
                _artifactEffect = artifactEffect;
            }
        }

        [SerializeField] Entry[] _entries = NewDefaultEntries();

        /// <summary>해당 등급의 도박 설정을 찾는다. 표에 없으면 false.</summary>
        public bool TryGetEntry(UnitGrade grade, out Entry entry)
        {
            if (_entries != null)
            {
                for (int i = 0; i < _entries.Length; i++)
                {
                    if (_entries[i] == null || _entries[i].Grade != grade) continue;

                    entry = _entries[i];
                    return true;
                }
            }

            entry = null;
            return false;
        }

        // 임시 표. 비용과 확률은 밸런스를 보면서 조정할 것.
        // 희귀/영웅/전설은 각자 등급의 유물(도박 확률 증가)의 보정을 받는다.
        static Entry[] NewDefaultEntries() => new[]
        {
            new Entry(UnitGrade.Rare, cost: 50, baseChance: 60f, hasArtifactBonus: true, costIncrease: 10, artifactEffect: ArtifactEffect.RareGambleChance),
            new Entry(UnitGrade.Hero, cost: 150, baseChance: 20f, hasArtifactBonus: true, costIncrease: 30, artifactEffect: ArtifactEffect.HeroGambleChance),
            new Entry(UnitGrade.Legendary, cost: 400, baseChance: 10f, hasArtifactBonus: true, costIncrease: 80, artifactEffect: ArtifactEffect.LegendGambleChance)
        };

        // 같은 등급이 두 번 들어가면 앞의 항목만 쓰이므로 바로 알려준다.
        void OnValidate()
        {
            if (_entries == null) return;

            for (int i = 0; i < _entries.Length; i++)
            {
                for (int j = i + 1; j < _entries.Length; j++)
                {
                    if (_entries[i] == null || _entries[j] == null) continue;

                    if (_entries[i].Grade == _entries[j].Grade)
                        Debug.LogWarning($"[GambleConfig] {_entries[i].Grade} 등급이 중복되었습니다. 앞의 항목만 쓰입니다.", this);
                }
            }
        }
    }
}