using UnityEngine;

namespace Defense.Units
{
    /// <summary>피해 계산 방식은 유지하고, 몬스터의 피격 표시만 구분하는 정보다.</summary>
    [System.Flags]
    public enum SkillDamageKind { Normal = 0, Critical = 1, Heavy = 2, Area = 4 }

    /// <summary>기존 IMonster 구현은 그대로 사용하고, 지원하는 몬스터에만 스킬 정보를 전달한다.</summary>
    public interface ISkillDamageReceiver
    {
        void TakeSkillDamage(float damage, SkillDamageKind kind);
    }

    public static class MonsterSkillDamage
    {
        /// <param name="source">피해를 준 유닛. 넘기면 이 피해로 대상이 죽었을 때 그 유닛의 처치 효과(OnKill)가 실행된다.</param>
        public static void Apply(IMonster target, float damage, SkillDamageKind kind, Unit source = null)
        {
            if (target == null || target.IsDead) return;

            // 죽은 몬스터의 위치는 읽을 수 없으므로, 처치 효과(시체 폭발)가 쓸 위치를 피해를 주기 전에 기억해 둔다.
            Vector3 position = (source != null) ? target.Position : Vector3.zero;

            // 보스에게는 유물의 보스 데미지 증가를 곱한다. 일반 공격, 추가 타격, 범위 스킬이 모두 이 함수를 지난다.
            if (source != null && target is IBossTarget boss && boss.IsBoss) damage *= source.BossDamageMultiplier;

            if (target is ISkillDamageReceiver receiver) receiver.TakeSkillDamage(damage, kind);
            else target.TakeDamage(damage);

            // 죽음은 피해를 받는 즉시 반영되므로, 바로 확인해서 처치한 유닛에게 알린다.
            if (source != null && target.IsDead) source.NotifyKill(target, position);
        }
    }

    /// <summary>
    /// 한 번의 공격 정보. 스킬이 읽고, Damage를 바꿔서 최종 대미지를 조절한다.
    /// 공격마다 새로 만들지 않도록 유닛 하나가 하나를 재사용한다.
    /// </summary>
    public class AttackContext
    {
        public Unit Attacker { get; private set; }
        public IMonster Target { get; private set; }

        /// <summary>범위 공격처럼 주변 몬스터가 필요한 스킬이 쓴다. 없으면 null.</summary>
        public IMonsterRegistry Registry { get; private set; }

        /// <summary>스킬이 반영되기 전의 공격력. 스킬 실행 순서와 상관없이 쓰고 싶을 때 읽는다.</summary>
        public float BaseDamage { get; private set; }

        /// <summary>대상에게 줄 최종 대미지. 스킬이 순서대로 곱하거나 더한다.</summary>
        public float Damage { get; set; }
        public SkillDamageKind DamageKind { get; set; }

        /// <summary>
        /// 스킬이 따로 주는 피해(폭탄, 연쇄 번개, 충격파, 발도, 멀티샷, 출혈)의 대미지.
        /// 스킬 반영 전 공격력 × 비율에 유물의 스킬 피해 증가를 곱한다.
        /// </summary>
        public float GetSkillDamage(float ratio) => BaseDamage * ratio * Attacker.SkillDamageMultiplier;

        public void Setup(Unit attacker, IMonster target, IMonsterRegistry registry, float baseDamage)
        {
            Attacker = attacker;
            Target = target;
            Registry = registry;
            BaseDamage = baseDamage;
            Damage = baseDamage;
            // 재사용하는 컨텍스트에 이전 공격의 치명타 표시가 남지 않게 한다.
            DamageKind = SkillDamageKind.Normal;
        }
    }
}
