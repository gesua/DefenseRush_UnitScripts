using UnityEngine;

namespace Defense.Units
{
    /// <summary>약탈: 소환될 때 코인을 얻는다.</summary>
    /// <remarks>지금 소환 비용에 비례(_costRatio)해서 받는다. 소환 비용을 읽을 수 없으면(연결 누락) 오류를 남기고 코인을 주지 않는다.</remarks>
    [CreateAssetMenu(fileName = "Skill_Plunder", menuName = "Defense/Skill/Plunder (약탈)")]
    public class PlunderSkill : UnitSkill
    {
        [Tooltip("지금 소환 비용의 이 비율만큼 받는다. (0.3 = 30%, 1 = 100%)")]
        [Min(0.01f)][SerializeField] float _costRatio = 0.3f;

        public override void OnSpawned(Unit unit)
        {
            // 소환 비용을 읽을 수 없으면 설정 누락이므로, 조용히 넘어가지 않고 오류로 알린다.
            if (unit.CurrencyReceiver is ISummonCostSource costSource)
            {
                unit.CurrencyReceiver.AddCurrency(GetAmount(costSource));
                NotifyVisual(SkillVisualKind.Plunder, unit);
                return;
            }

            Debug.LogError($"[PlunderSkill] {unit.name}: CurrencyReceiver가 없거나 ISummonCostSource를 구현하지 않았습니다. GridField의 currencySource에 BattleEconomy를 연결하세요.", this);
        }

        // 소환 비용의 비율만큼(최소 1).
        int GetAmount(ISummonCostSource costSource)
        {
            return Mathf.Max(1, Mathf.CeilToInt(costSource.CurrentSummonCost * _costRatio));
        }
    }
}