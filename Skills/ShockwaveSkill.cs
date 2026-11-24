using System.Collections.Generic;
using UnityEngine;

namespace Defense.Units
{
    /// <summary>충격파: 공격할 때 일정 확률로 맞은 적과 그 주변 적에게 범위 피해를 주고 잠시 기절시킨다. (기절은 몬스터가 IStunnable을 구현해야 적용된다)</summary>
    [CreateAssetMenu(fileName = "Skill_Shockwave", menuName = "Defense/Skill/Shockwave (충격파)")]
    public class ShockwaveSkill : UnitSkill
    {
        // 피해를 주다 보면 몬스터가 죽어 목록이 바뀌므로, 먼저 대상을 모아 둔다. (스킬 에셋 공용 버퍼)
        static readonly List<IMonster> _targets = new List<IMonster>();

        [Tooltip("발동 확률 (0~1). 1이면 공격할 때마다 건다.")]
        [Range(0f, 1f)][SerializeField] float _chance = 0.2f;
        [Tooltip("범위 피해 대미지 비율. 공격력(스킬 반영 전) 기준. 0이면 기절만 건다.")]
        [Min(0f)][SerializeField] float _damageRatio = 0.5f;
        [Tooltip("충격파 반경. 맞은 적 위치 기준 XZ 평면 거리(월드 단위). 맞은 적도 포함한다.")]
        [Min(0f)][SerializeField] float _radius = 1.5f;
        [Tooltip("기절 시간(초)")]
        [Min(0f)][SerializeField] float _stunDuration = 0.75f;

        public override void OnAttack(AttackContext context)
        {
            if (context.Registry == null || _radius <= 0f) return;
            if (_chance < 1f && Random.value >= _chance) return;

            float damage = context.GetSkillDamage(_damageRatio);
            if (damage <= 0f && _stunDuration <= 0f) return;

            BuildTargets(context);
            if (_targets.Count == 0) return;

            NotifyVisual(SkillVisualKind.Shockwave, context.Attacker, context.Target, _radius);

            // 죽은 적에게는 기절이 걸리지 않으므로, 피해보다 기절을 먼저 건다.
            if (_stunDuration > 0f)
            {
                for (int i = 0; i < _targets.Count; i++)
                {
                    if (_targets[i] is IStunnable stunnable) stunnable.ApplyStun(_stunDuration);
                }
            }

            if (damage > 0f)
            {
                for (int i = 0; i < _targets.Count; i++)
                {
                    MonsterSkillDamage.Apply(_targets[i], damage, SkillDamageKind.Area, context.Attacker);
                }
            }
            _targets.Clear();
        }

        // 맞은 적과, 그 위치 기준 반경 안의 살아있는 적을 모은다.
        void BuildTargets(AttackContext context)
        {
            _targets.Clear();
            if (context.Target.IsDead == false) _targets.Add(context.Target);

            Vector3 center = context.Target.Position;
            float sqrRadius = _radius * _radius;
            IReadOnlyList<IMonster> monsters = context.Registry.ActiveMonsters;
            for (int i = 0; i < monsters.Count; i++)
            {
                IMonster monster = monsters[i];
                if (monster == context.Target || monster.IsDead) continue;

                float dx = monster.Position.x - center.x;
                float dz = monster.Position.z - center.z;
                if (dx * dx + dz * dz <= sqrRadius) _targets.Add(monster);
            }
        }
    }
}
