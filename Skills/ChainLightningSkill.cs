using System.Collections.Generic;
using UnityEngine;

namespace Defense.Units
{
    /// <summary>연쇄 번개: 공격할 때 일정 확률로 맞은 적에서 가까운 적으로 번개가 차례로 이어져 범위 피해를 준다.</summary>
    [CreateAssetMenu(fileName = "Skill_ChainLightning", menuName = "Defense/Skill/Chain Lightning (연쇄 번개)")]
    public class ChainLightningSkill : UnitSkill
    {
        // 피해를 주다 보면 몬스터가 죽어 목록이 바뀌므로, 먼저 번개가 이어질 대상을 모아 둔다. (스킬 에셋 공용 버퍼)
        static readonly List<IMonster> _chain = new List<IMonster>();

        [Tooltip("발동 확률 (0~1). 1이면 공격할 때마다 건다.")]
        [Range(0f, 1f)][SerializeField] float _chance = 0.25f;
        [Tooltip("맞은 적 말고 추가로 번개가 이어질 최대 적 수")]
        [Min(1)][SerializeField] int _maxTargets = 3;
        [Tooltip("번개 한 번의 대미지 비율. 공격력(스킬 반영 전) 기준")]
        [Min(0f)][SerializeField] float _damageRatio = 0.6f;
        [Tooltip("한 번 이어질 수 있는 최대 거리. 직전에 맞은 적 위치 기준 XZ 평면 거리(월드 단위)")]
        [Min(0f)][SerializeField] float _radius = 3f;

        public override void OnAttack(AttackContext context)
        {
            if (context.Registry == null) return;
            if (_chance < 1f && Random.value >= _chance) return;

            float damage = context.GetSkillDamage(_damageRatio);
            if (damage <= 0f || _radius <= 0f) return;

            BuildChain(context);
            if (_chain.Count == 0) return;

            NotifyVisual(SkillVisualKind.Lightning, context.Attacker, context.Target, _radius);
            for (int i = 0; i < _chain.Count; i++)
            {
                MonsterSkillDamage.Apply(_chain[i], damage, SkillDamageKind.Area, context.Attacker);
            }
            _chain.Clear();
        }

        // 맞은 적에서 시작해, 매번 직전 적과 가장 가까운(반경 안의) 아직 안 맞은 적을 _maxTargets명까지 모은다.
        void BuildChain(AttackContext context)
        {
            _chain.Clear();

            IReadOnlyList<IMonster> monsters = context.Registry.ActiveMonsters;
            float sqrRadius = _radius * _radius;
            Vector3 from = context.Target.Position;

            for (int hop = 0; hop < _maxTargets; hop++)
            {
                IMonster next = null;
                float bestSqrDistance = sqrRadius;

                for (int i = 0; i < monsters.Count; i++)
                {
                    IMonster monster = monsters[i];
                    if (monster == context.Target || monster.IsDead || _chain.Contains(monster)) continue;

                    float dx = monster.Position.x - from.x;
                    float dz = monster.Position.z - from.z;
                    float sqrDistance = dx * dx + dz * dz;
                    if (sqrDistance <= bestSqrDistance)
                    {
                        next = monster;
                        bestSqrDistance = sqrDistance;
                    }
                }

                if (next == null) break;

                _chain.Add(next);
                from = next.Position;
            }
        }
    }
}
