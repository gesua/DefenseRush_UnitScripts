using System.Collections.Generic;
using UnityEngine;

namespace Defense.Units
{
    /// <summary>발도: 공격할 때 일정 확률로 유닛 사거리 안의 모든 적에게 공격력의 일부만큼 피해를 준다. (평소 공격을 받는 대상은 제외)</summary>
    [CreateAssetMenu(fileName = "Skill_Slash", menuName = "Defense/Skill/Slash (발도)")]
    public class SlashSkill : UnitSkill
    {
        // 피해를 주다 보면 몬스터가 죽어 목록이 바뀌므로, 먼저 대상을 모아 둔다. (스킬 에셋 공용 버퍼)
        static readonly List<IMonster> _targets = new List<IMonster>();

        [Tooltip("발동 확률 (0~1). 1이면 공격할 때마다 건다.")]
        [Range(0f, 1f)][SerializeField] float _chance = 0.2f;
        [Tooltip("대미지 비율. 공격력(스킬 반영 전) 기준")]
        [Min(0f)][SerializeField] float _damageRatio = 1f;

        public override void OnAttack(AttackContext context)
        {
            if (context.Registry == null) return;
            if (_chance < 1f && Random.value >= _chance) return;

            float damage = context.GetSkillDamage(_damageRatio);
            if (damage <= 0f) return;

            // 사거리는 유닛과 같은 기준(사거리 기준점, XZ 평면 거리)으로 판정한다.
            float range = context.Attacker.Stats.AttackRange;
            Vector3 origin = context.Attacker.RangeCenter;
            float sqrRange = range * range;

            _targets.Clear();
            IReadOnlyList<IMonster> monsters = context.Registry.ActiveMonsters;
            for (int i = 0; i < monsters.Count; i++)
            {
                IMonster monster = monsters[i];
                if (monster == context.Target || monster.IsDead) continue;

                float dx = monster.Position.x - origin.x;
                float dz = monster.Position.z - origin.z;
                if (dx * dx + dz * dz <= sqrRange) _targets.Add(monster);
            }
            if (_targets.Count == 0) return;

            // 베는 연출은 맞은 적이 아니라 유닛 주변에서 보이도록 대상 없이 보낸다.
            NotifyVisual(SkillVisualKind.Slash, context.Attacker, null, range);
            for (int i = 0; i < _targets.Count; i++)
            {
                MonsterSkillDamage.Apply(_targets[i], damage, SkillDamageKind.Area, context.Attacker);
            }
            _targets.Clear();
        }
    }
}
