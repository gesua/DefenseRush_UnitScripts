using System.Collections.Generic;
using UnityEngine;

namespace Defense.Units
{
    /// <summary>시체 폭발: 이 유닛이 처치한 적이 죽은 자리에서 폭발해 주변 적에게 범위 피해를 준다.</summary>
    /// <remarks>
    /// 폭발 피해로 죽은 적은 다시 폭발하지 않는다. (연쇄 폭발 방지)
    /// 폭발로 처치한 적도 이 유닛의 처치로 치므로, 광분이나 처치 보상 같은 다른 처치 스킬은 실행된다.
    /// </remarks>
    [CreateAssetMenu(fileName = "Skill_CorpseExplosion", menuName = "Defense/Skill/Corpse Explosion (시체 폭발)")]
    public class CorpseExplosionSkill : UnitSkill
    {
        // 피해를 주다 보면 몬스터가 죽어 목록이 바뀌므로, 먼저 대상을 모아 둔다. (스킬 에셋 공용 버퍼)
        static readonly List<IMonster> _targets = new List<IMonster>();

        // 폭발 피해로 죽은 적의 처치가 또 폭발을 일으키지 않게 막는다.
        static bool _isExploding;

        [Tooltip("범위 피해 대미지 비율. 처치한 유닛의 공격력(스킬 반영 전) 기준")]
        [Min(0f)][SerializeField] float _damageRatio = 0.6f;
        [Tooltip("폭발 반경. 죽은 적 위치 기준 XZ 평면 거리(월드 단위)")]
        [Min(0f)][SerializeField] float _radius = 2f;

        public override void OnKill(Unit unit, IMonster target, Vector3 deathPosition)
        {
            if (_isExploding) return;

            IMonsterRegistry registry = unit.MonsterRegistry;
            float damage = unit.CombatStats.AttackPower * _damageRatio * unit.SkillDamageMultiplier;
            if (registry == null || damage <= 0f || _radius <= 0f) return;

            NotifyVisualAt(SkillVisualKind.Explosion, unit, deathPosition, _radius);

            float sqrRadius = _radius * _radius;
            _targets.Clear();
            IReadOnlyList<IMonster> monsters = registry.ActiveMonsters;
            for (int i = 0; i < monsters.Count; i++)
            {
                IMonster monster = monsters[i];
                if (monster == target || monster.IsDead) continue;

                float dx = monster.Position.x - deathPosition.x;
                float dz = monster.Position.z - deathPosition.z;
                if (dx * dx + dz * dz <= sqrRadius) _targets.Add(monster);
            }

            _isExploding = true;
            try
            {
                for (int i = 0; i < _targets.Count; i++)
                {
                    MonsterSkillDamage.Apply(_targets[i], damage, SkillDamageKind.Area, unit);
                }
            }
            finally
            {
                _isExploding = false;
                _targets.Clear();
            }
        }
    }
}