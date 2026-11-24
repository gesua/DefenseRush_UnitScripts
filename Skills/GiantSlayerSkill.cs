using UnityEngine;

namespace Defense.Units
{
    /// <summary>거인 학살: 공격할 때 대상의 최대 체력에 비례한 추가 대미지를 준다. (몬스터가 IMonsterHealth를 구현해야 적용된다)</summary>
    /// <remarks>
    /// 이 유닛의 일반 공격 대미지(context.Damage)에 더한다. 체력이 아주 높은 적에게 무한정 커지지 않게,
    /// 추가 대미지는 공격력(스킬 반영 전)의 _maxBonusMultiplier배를 넘지 못한다. 멀티샷 같은 추가 타격은 포함되지 않는다.
    /// </remarks>
    [CreateAssetMenu(fileName = "Skill_GiantSlayer", menuName = "Defense/Skill/Giant Slayer (거인 학살)")]
    public class GiantSlayerSkill : UnitSkill
    {
        [Tooltip("대상 최대 체력의 이 비율만큼 추가 대미지. 0.03이면 최대 체력의 3%")]
        [Min(0f)][SerializeField] float _healthRatio = 0.03f;
        [Tooltip("추가 대미지의 상한. 공격력(스킬 반영 전) 기준 배수. 1이면 공격력만큼")]
        [Min(0f)][SerializeField] float _maxBonusMultiplier = 1f;

        public override void OnAttack(AttackContext context)
        {
            if (_healthRatio <= 0f || _maxBonusMultiplier <= 0f) return;

            if (context.Target is IMonsterHealth health)
            {
                float bonus = Mathf.Min(health.MaximumHealth * _healthRatio, context.BaseDamage * _maxBonusMultiplier);
                if (bonus <= 0f) return;

                context.Damage += bonus * context.Attacker.SkillDamageMultiplier;
                NotifyVisual(SkillVisualKind.Heavy, context.Attacker, context.Target);
            }
        }
    }
}