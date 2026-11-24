using UnityEngine;

namespace Defense.Units
{
    /// <summary>처형: 체력이 일정 비율 이하로 낮은 적을 공격할 때 대미지를 배수로 늘린다. (몬스터가 IMonsterHealth를 구현해야 적용된다)</summary>
    /// <remarks>이 유닛의 일반 공격 대미지(context.Damage)에만 곱한다. 멀티샷 같은 추가 타격은 스킬 반영 전 공격력 기준이라 포함되지 않는다.</remarks>
    [CreateAssetMenu(fileName = "Skill_Execute", menuName = "Defense/Skill/Execute (처형)")]
    public class ExecuteSkill : UnitSkill
    {
        [Tooltip("이 비율 이하의 체력이면 발동한다. 0.3이면 체력 30% 이하")]
        [Range(0f, 1f)][SerializeField] float _healthThreshold = 0.3f;
        [Tooltip("발동했을 때 대미지 배수. 1.5면 +50%")]
        [Min(1f)][SerializeField] float _multiplier = 1.5f;

        public override void OnAttack(AttackContext context)
        {
            if (_multiplier <= 1f || _healthThreshold <= 0f) return;
            if (context.Target is IMonsterHealth health && health.HealthRatio <= _healthThreshold)
            {
                context.Damage *= _multiplier;
                NotifyVisual(SkillVisualKind.Execute, context.Attacker, context.Target);
            }
        }
    }
}
