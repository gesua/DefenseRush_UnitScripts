using System.Collections.Generic;
using UnityEngine;

namespace Defense.Units
{
    /// <summary>슬로우: 공격한 적의 이동속도를 잠시 줄인다. (몬스터가 ISlowable을 구현해야 적용된다)</summary>
    /// <remarks>확률(_chance)과 범위(_radius)를 주면 "일정 확률로 맞은 적 주변의 적 모두"에게도 쓸 수 있다. 기본값은 기존과 같다.</remarks>
    [CreateAssetMenu(fileName = "Skill_Slow", menuName = "Defense/Skill/Slow (슬로우)")]
    public class SlowSkill : UnitSkill
    {
        [Tooltip("줄일 이동속도(월드 단위/초)")]
        [Min(0f)][SerializeField] float _amount = 1f;
        [Tooltip("지속 시간(초). 다시 맞으면 갱신된다.")]
        [Min(0f)][SerializeField] float _duration = 2f;

        [Tooltip("발동 확률 (0~1). 1이면 공격할 때마다 건다.")]
        [Range(0f, 1f)][SerializeField] float _chance = 1f;
        [Tooltip("0이면 공격한 적에게만, 0보다 크면 맞은 적 주변(XZ 평면 거리, 월드 단위)의 모든 적에게 건다. (맞은 적 포함)")]
        [Min(0f)][SerializeField] float _radius;
        [Tooltip("true면 맞은 적과 상관없이 필드의 살아있는 모든 적에게 건다. (이 유닛이 공격할 때마다 지속시간이 갱신된다) 이 경우 범위(_radius)는 무시한다.")]
        [SerializeField] bool _affectAll;

        public override void OnAttack(AttackContext context)
        {
            if (_chance < 1f && Random.value >= _chance) return;

            if (_affectAll)
            {
                SlowAll(context);
                return;
            }

            Slow(context.Target);
            // 범위 안의 적마다 중복 생성하지 않고 발동 위치에서 한 번만 표시한다.
            if (context.Target is ISlowable && _amount > 0f && _duration > 0f)
                NotifyVisual(SkillVisualKind.Slow, context.Attacker, context.Target, _radius);
            if (_radius <= 0f || context.Registry == null) return;

            // 슬로우는 몬스터를 죽이지 않으므로, 목록을 순회하면서 바로 걸어도 된다.
            Vector3 center = context.Target.Position;
            float sqrRadius = _radius * _radius;
            IReadOnlyList<IMonster> monsters = context.Registry.ActiveMonsters;
            for (int i = 0; i < monsters.Count; i++)
            {
                IMonster monster = monsters[i];
                if (monster == context.Target || monster.IsDead) continue;

                float dx = monster.Position.x - center.x;
                float dz = monster.Position.z - center.z;
                if (dx * dx + dz * dz <= sqrRadius) Slow(monster);
            }
        }

        // 전체 적용: 맞은 적에게 걸고 연출은 한 번만 보낸 뒤, 필드의 나머지 모든 적에게 건다.
        void SlowAll(AttackContext context)
        {
            Slow(context.Target);
            if (context.Target is ISlowable && _amount > 0f && _duration > 0f)
                NotifyVisual(SkillVisualKind.Slow, context.Attacker, context.Target, 0f);
            if (context.Registry == null) return;

            // 슬로우는 몬스터를 죽이지 않으므로, 목록을 순회하면서 바로 걸어도 된다.
            IReadOnlyList<IMonster> monsters = context.Registry.ActiveMonsters;
            for (int i = 0; i < monsters.Count; i++)
            {
                IMonster monster = monsters[i];
                if (monster == context.Target || monster.IsDead) continue;

                Slow(monster);
            }
        }

        void Slow(IMonster monster)
        {
            if (monster is ISlowable slowable) slowable.ApplySlow(_amount, _duration);
        }
    }
}
