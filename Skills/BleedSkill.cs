using UnityEngine;

namespace Defense.Units
{
    /// <summary>출혈: 공격한 적에게 일정 시간 동안 지속 피해를 건다. (몬스터가 IDotReceiver를 구현해야 적용된다)</summary>
    /// <remarks>
    /// 같은 유닛이 같은 스킬로 다시 걸면 값과 지속 시간만 갱신되고, 서로 다른 유닛이 건 출혈은 합산된다.
    /// 출혈로 적이 죽어도 건 유닛의 처치 효과(OnKill)가 실행된다.
    /// </remarks>
    [CreateAssetMenu(fileName = "Skill_Bleed", menuName = "Defense/Skill/Bleed (출혈)")]
    public class BleedSkill : UnitSkill
    {
        [Tooltip("발동 확률 (0~1). 1이면 공격할 때마다 건다.")]
        [Range(0f, 1f)][SerializeField] float _chance = 1f;
        [Tooltip("지속 시간 동안 들어가는 피해의 합계 비율. 공격력(스킬 반영 전) 기준. 0.9면 지속 시간 동안 공격력의 90%")]
        [Min(0f)][SerializeField] float _damageRatio = 0.9f;
        [Tooltip("지속 시간(초). 다시 걸리면 갱신된다.")]
        [Min(0f)][SerializeField] float _duration = 3f;

        public override void OnAttack(AttackContext context)
        {
            if (_chance < 1f && Random.value >= _chance) return;
            if (_damageRatio <= 0f || _duration <= 0f) return;

            if (context.Target is IDotReceiver receiver)
            {
                float damagePerSecond = context.GetSkillDamage(_damageRatio) / _duration;
                receiver.ApplyDot(context.Attacker, this, damagePerSecond, _duration);
                NotifyVisual(SkillVisualKind.Bleed, context.Attacker, context.Target);
            }
        }
    }
}
