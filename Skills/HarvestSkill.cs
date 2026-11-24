using UnityEngine;

namespace Defense.Units
{
    /// <summary>수확: 라운드가 시작될 때마다 코인을 얻는다. (필드에 있는 유닛만 받는다)</summary>
    /// <remarks>UnitRoundSkillRunner가 라운드 시작을 전해 줘야 작동한다. 같은 유닛이 여러 마리면 마리마다 받는다.</remarks>
    [CreateAssetMenu(fileName = "Skill_Harvest", menuName = "Defense/Skill/Harvest (수확)")]
    public class HarvestSkill : UnitSkill
    {
        [Tooltip("라운드가 시작될 때마다 받는 코인")]
        [Min(1)][SerializeField] int _amount = 4;

        public override void OnSpawned(Unit unit)
        {
            // 코인을 받을 곳이 없으면 설정 누락이다. 라운드마다 로그가 쌓이지 않게 소환될 때 한 번만 오류로 알린다.
            if (unit.CurrencyReceiver == null)
                Debug.LogError($"[HarvestSkill] {unit.name}: CurrencyReceiver가 없습니다. GridField의 currencySource에 BattleEconomy를 연결하세요.", this);
        }

        public override void OnRoundStart(Unit unit, int round)
        {
            ICurrencyReceiver receiver = unit.CurrencyReceiver;
            if (receiver == null) return; // 연결 누락은 OnSpawned에서 이미 알렸다.

            if (receiver.AddCurrency(_amount)) NotifyVisual(SkillVisualKind.Loot, unit);
        }
    }
}