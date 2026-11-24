using System.Collections.Generic;
using UnityEngine;

namespace Defense.Units
{
    /// <summary>폭탄: 공격할 때 맞은 적 주변의 다른 적에게도 공격력의 일부만큼 범위 피해를 준다.</summary>
    [CreateAssetMenu(fileName = "Skill_Bomb", menuName = "Defense/Skill/Bomb (폭탄)")]
    public class BombSkill : UnitSkill
    {
        // 피해를 주다 보면 몬스터가 죽어 목록이 바뀌므로, 먼저 대상을 모아 둔다. (스킬 에셋 공용 버퍼)
        static readonly List<IMonster> _targets = new List<IMonster>();

        [Tooltip("범위 피해 대미지 비율. 공격력(스킬 반영 전) 기준")]
        [Min(0f)][SerializeField] float _damageRatio = 0.8f;
        [Tooltip("폭발 반경. 맞은 적 위치 기준 XZ 평면 거리(월드 단위)")]
        [Min(0f)][SerializeField] float _radius = 2f;

        public override void OnAttack(AttackContext context)
        {
            if (context.Registry == null) return;

            Vector3 center = context.Target.Position;
            float damage = context.GetSkillDamage(_damageRatio);
            float sqrRadius = _radius * _radius;
            if (damage > 0f && _radius > 0f)
                NotifyVisual(SkillVisualKind.Explosion, context.Attacker, context.Target, _radius);

            _targets.Clear();
            IReadOnlyList<IMonster> monsters = context.Registry.ActiveMonsters;
            for (int i = 0; i < monsters.Count; i++)
            {
                IMonster monster = monsters[i];
                if (monster == context.Target || monster.IsDead) continue;

                float dx = monster.Position.x - center.x;
                float dz = monster.Position.z - center.z;
                if (dx * dx + dz * dz <= sqrRadius) _targets.Add(monster);
            }

            for (int i = 0; i < _targets.Count; i++)
            {
                MonsterSkillDamage.Apply(_targets[i], damage, SkillDamageKind.Area, context.Attacker);
            }
            _targets.Clear();
        }
    }
}
