using System.Collections.Generic;
using UnityEngine;

namespace Defense.Units
{
    /// <summary>갑옷 파괴: 공격한 적의 방어력을 잠시 깎는다. (몬스터가 IArmorBreakable을 구현해야 적용된다)</summary>
    /// <remarks>
    /// 확률(_chance)과 범위(_radius)를 주면 "일정 확률로 맞은 적 주변의 적 모두"에게도 쓸 수 있고, _affectAll이면 필드의 모든 적에게 건다.
    /// 유물의 방어력 감소와는 별도 채널이라 서로 덮어쓰지 않고, 여러 출처가 겹치면 가장 큰 감소량 하나만 적용된다.
    /// </remarks>
    [CreateAssetMenu(fileName = "Skill_ArmorBreak", menuName = "Defense/Skill/Armor Break (갑옷 파괴)")]
    public class ArmorBreakSkill : UnitSkill
    {
        [Tooltip("발동 확률 (0~1). 1이면 공격할 때마다 건다.")]
        [Range(0f, 1f)][SerializeField] float _chance = 1f;
        [Tooltip("깎을 방어력(고정값)")]
        [Min(0f)][SerializeField] float _amount = 10f;
        [Tooltip("깎을 방어력 비율 (0~1). 0.3이면 30% 감소. 고정값과 함께 쓰면 둘 다 적용된다.")]
        [Range(0f, 1f)][SerializeField] float _percent;
        [Tooltip("지속 시간(초). 다시 맞으면 갱신된다.")]
        [Min(0f)][SerializeField] float _duration = 2f;
        [Tooltip("0이면 공격한 적에게만, 0보다 크면 맞은 적 주변(XZ 평면 거리, 월드 단위)의 모든 적에게 건다. (맞은 적 포함)")]
        [Min(0f)][SerializeField] float _radius;
        [Tooltip("true면 맞은 적과 상관없이 필드의 살아있는 모든 적에게 건다. 이 경우 범위(_radius)는 무시한다.")]
        [SerializeField] bool _affectAll;

        // 에디터에서 값을 바꿀 때, 아무 효과가 없는 설정을 바로 알려 준다.
        void OnValidate()
        {
            if (_amount <= 0f && _percent <= 0f)
                Debug.LogWarning($"[ArmorBreakSkill] {name}: 고정값과 비율이 모두 0이라 방어력이 깎이지 않습니다.", this);
            if (_duration <= 0f)
                Debug.LogWarning($"[ArmorBreakSkill] {name}: 지속 시간이 0 이하라 효과가 적용되지 않습니다.", this);
        }

        public override void OnAttack(AttackContext context)
        {
            if (_chance < 1f && Random.value >= _chance) return;
            if (_duration <= 0f || (_amount <= 0f && _percent <= 0f)) return;

            Break(context.Target, context.Attacker);
            // 범위 안의 적마다 중복 생성하지 않고 발동 위치에서 한 번만 표시한다.
            if (context.Target is IArmorBreakable)
                NotifyVisual(SkillVisualKind.ArmorBreak, context.Attacker, context.Target, (_affectAll) ? 0f : _radius);
            if (context.Registry == null || (_affectAll == false && _radius <= 0f)) return;

            // 방어력 감소는 몬스터를 죽이지 않으므로, 목록을 순회하면서 바로 걸어도 된다.
            Vector3 center = context.Target.Position;
            float sqrRadius = _radius * _radius;
            IReadOnlyList<IMonster> monsters = context.Registry.ActiveMonsters;
            for (int i = 0; i < monsters.Count; i++)
            {
                IMonster monster = monsters[i];
                if (monster == context.Target || monster.IsDead) continue;

                if (_affectAll == false)
                {
                    float dx = monster.Position.x - center.x;
                    float dz = monster.Position.z - center.z;
                    if (dx * dx + dz * dz > sqrRadius) continue;
                }
                Break(monster, context.Attacker);
            }
        }

        void Break(IMonster monster, Unit source)
        {
            if (monster is IArmorBreakable breakable) breakable.ApplyArmorBreak(source, this, _amount, _percent, _duration);
        }
    }
}