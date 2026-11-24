using UnityEngine;

namespace Defense.Units
{
    /// <summary>
    /// 공격할 때 일정 확률로 대미지를 배수로 늘린다. 치명타(30%, 1.5배), 더블샷(20%, 2배)이 이 스킬의 값만 다른 에셋이다.
    /// 한 유닛이 둘 다 가지면 배수가 서로 곱해진다.
    /// </summary>
    [CreateAssetMenu(fileName = "Skill_ChanceDamage", menuName = "Defense/Skill/Chance Damage (치명타, 더블샷)")]
    public class ChanceDamageSkill : UnitSkill
    {
        [Tooltip("발동 확률 (0~1)")]
        [Range(0f, 1f)][SerializeField] float _chance = 0.3f;
        [Tooltip("발동했을 때 대미지 배수")]
        [Min(1f)][SerializeField] float _multiplier = 1.5f;

        public override void OnAttack(AttackContext context)
        {
            if (Random.value >= _chance) return;
            context.Damage *= _multiplier;
            // 현재 강타(8배)는 큰 피격 표시, 치명타·더블샷은 치명타 표시로 전달한다.
            if (_multiplier > 1f)
            {
                context.DamageKind |= _multiplier >= 4f ? SkillDamageKind.Heavy : SkillDamageKind.Critical;
                NotifyVisual(_multiplier >= 4f ? SkillVisualKind.Heavy : SkillVisualKind.Critical,
                    context.Attacker, context.Target);
            }
        }
    }
}
