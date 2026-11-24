using UnityEngine;

namespace Defense.Units
{
    /// <summary>노획: 공격할 때 일정 확률로 코인을 얻는다.</summary>
    [CreateAssetMenu(fileName = "Skill_ChanceCoin", menuName = "Defense/Skill/Chance Coin (노획)")]
    public class ChanceCoinSkill : UnitSkill
    {
        [Tooltip("발동 확률 (0~1)")]
        [Range(0f, 1f)][SerializeField] float _chance = 0.15f;
        [Tooltip("발동했을 때 받는 코인")]
        [Min(1)][SerializeField] int _amount = 1;

        public override void OnSpawned(Unit unit)
        {
            // 코인을 받을 곳이 없으면 설정 누락이다. 공격할 때마다 로그가 쌓이지 않게 소환될 때 한 번만 오류로 알린다.
            if (unit.CurrencyReceiver == null)
                Debug.LogError($"[ChanceCoinSkill] {unit.name}: CurrencyReceiver가 없습니다. GridField의 currencySource에 BattleEconomy를 연결하세요.", this);
        }

        public override void OnAttack(AttackContext context)
        {
            ICurrencyReceiver receiver = context.Attacker.CurrencyReceiver;
            if (receiver == null) return; // 연결 누락은 OnSpawned에서 이미 알렸다.

            if (Random.value < _chance)
            {
                receiver.AddCurrency(_amount);
                NotifyVisual(SkillVisualKind.Loot, context.Attacker);
            }
        }
    }
}