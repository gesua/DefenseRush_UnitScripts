using UnityEngine;

namespace Defense.Units
{
    /// <summary>처치 보상: 이 유닛이 적을 처치할 때마다 코인을 얻는다. (적을 처치했을 때의 기본 보상과 별개로 더 받는다)</summary>
    [CreateAssetMenu(fileName = "Skill_KillReward", menuName = "Defense/Skill/Kill Reward (처치 보상)")]
    public class KillRewardSkill : UnitSkill
    {
        [Tooltip("처치할 때마다 받는 코인")]
        [Min(1)][SerializeField] int _amount = 1;

        public override void OnSpawned(Unit unit)
        {
            // 코인을 받을 곳이 없으면 설정 누락이다. 처치할 때마다 로그가 쌓이지 않게 소환될 때 한 번만 오류로 알린다.
            if (unit.CurrencyReceiver == null)
                Debug.LogError($"[KillRewardSkill] {unit.name}: CurrencyReceiver가 없습니다. GridField의 currencySource에 BattleEconomy를 연결하세요.", this);
        }

        public override void OnKill(Unit unit, IMonster target)
        {
            ICurrencyReceiver receiver = unit.CurrencyReceiver;
            if (receiver == null) return; // 연결 누락은 OnSpawned에서 이미 알렸다.

            if (receiver.AddCurrency(_amount)) NotifyVisual(SkillVisualKind.Loot, unit);
        }
    }
}