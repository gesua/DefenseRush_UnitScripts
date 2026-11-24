using UnityEngine;

namespace Defense.Units
{
    /// <summary>멀티샷: 공격할 때 일정 확률로 같은 대상에게 추가 타격을 한다. 추가 타격도 유닛의 공격 방식(투사체)과 적중 효과를 그대로 거친다.</summary>
    [CreateAssetMenu(fileName = "Skill_MultiShot", menuName = "Defense/Skill/MultiShot (멀티샷)")]
    public class MultiShotSkill : UnitSkill
    {
        [Tooltip("발동 확률 (0~1). 1이면 공격할 때마다 추가 타격한다.")]
        [Range(0f, 1f)][SerializeField] float _chance = 0.2f;
        [Tooltip("추가 타격 횟수")]
        [Min(1)][SerializeField] int _extraHits = 1;
        [Tooltip("추가 타격 한 번의 대미지 비율. 공격력(스킬 반영 전) 기준")]
        [Min(0f)][SerializeField] float _damageRatio = 1f;

        public override void OnAttack(AttackContext context)
        {
            if (_chance < 1f && Random.value >= _chance) return;

            float damage = context.GetSkillDamage(_damageRatio);
            if (damage <= 0f) return;

            // 멀티샷은 연출이 없어 NotifyVisual을 지나지 않으므로, 발동했을 때 스킬 골드 판정을 직접 한다.
            NotifyTriggered(context.Attacker);

            // MonsterSkillDamage 대신 DealDamage를 써야 투사체와 적중 효과(Hit 알림)가 일반 공격과 똑같이 나간다.
            for (int i = 0; i < _extraHits; i++)
            {
                context.Attacker.DealDamage(context.Target, damage, SkillDamageKind.Normal);
            }
        }
    }
}
