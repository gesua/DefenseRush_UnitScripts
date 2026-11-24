using UnityEngine;

namespace Defense.Units
{
    /// <summary>보스 사냥꾼: 보스 몬스터를 공격할 때 대미지를 배수로 늘린다. (몬스터가 IBossTarget을 구현해야 적용된다)</summary>
    /// <remarks>이 유닛의 일반 공격 대미지(context.Damage)에만 곱한다. 멀티샷 같은 추가 타격은 스킬 반영 전 공격력 기준이라 포함되지 않는다.</remarks>
    [CreateAssetMenu(fileName = "Skill_BossHunter", menuName = "Defense/Skill/Boss Hunter (보스 사냥꾼)")]
    public class BossHunterSkill : UnitSkill
    {
        [Tooltip("보스에게 주는 대미지 배수. 1.5면 +50%")]
        [Min(1f)][SerializeField] float _multiplier = 1.5f;

        public override void OnAttack(AttackContext context)
        {
            if (_multiplier <= 1f) return;

            if (context.Target is IBossTarget boss && boss.IsBoss) context.Damage *= _multiplier;
        }
    }
}
