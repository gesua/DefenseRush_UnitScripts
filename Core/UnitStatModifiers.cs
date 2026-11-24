using System.Collections.Generic;
using UnityEngine;

namespace Defense.Units
{
    /// <summary>
    /// 유닛 하나에 걸린 스킬 보정(공격력, 공격속도 비율)을 모아 둔다.
    /// 같은 유닛이 같은 스킬로 다시 걸면 값과 지속 시간만 갱신하고, 서로 다른 출처는 합산한다.
    /// 일시 보정은 지속 시간이 지나면 사라지고, 오라 보정은 UnitAuraSystem이 지우고 다시 건다.
    /// </summary>
    public class UnitStatModifiers
    {
        class Modifier
        {
            public Unit Source;
            public UnitSkill Skill;
            public float AttackPowerRatio;
            public float AttackSpeedRatio;
            public float ExpireTime;    // 오라는 사용하지 않는다
            public bool IsAura;
        }

        readonly List<Modifier> _modifiers = new List<Modifier>();

        /// <summary>일정 시간 동안만 유지되는 보정을 건다. 같은 (source, skill)이 이미 있으면 값과 지속 시간을 갱신한다.</summary>
        /// <param name="attackPowerRatio">올려 줄 공격력 비율. 0.3이면 +30%</param>
        /// <param name="attackSpeedRatio">올려 줄 공격속도 비율. 0.3이면 +30%</param>
        public void SetTimed(Unit source, UnitSkill skill, float attackPowerRatio, float attackSpeedRatio, float duration)
        {
            if (duration <= 0f) return;

            Modifier modifier = FindOrAdd(source, skill);
            modifier.AttackPowerRatio = attackPowerRatio;
            modifier.AttackSpeedRatio = attackSpeedRatio;
            modifier.ExpireTime = Time.time + duration;
            modifier.IsAura = false;
        }

        /// <summary>오라 보정을 건다. 시간 제한은 없고, UnitAuraSystem이 칸 구성이 바뀔 때 ClearAuras로 지우고 다시 건다.</summary>
        public void SetAura(Unit source, UnitSkill skill, float attackPowerRatio, float attackSpeedRatio)
        {
            Modifier modifier = FindOrAdd(source, skill);
            modifier.AttackPowerRatio = attackPowerRatio;
            modifier.AttackSpeedRatio = attackSpeedRatio;
            modifier.IsAura = true;
        }

        /// <summary>오라 보정만 모두 지운다. (일시 보정은 그대로)</summary>
        public void ClearAuras()
        {
            for (int i = _modifiers.Count - 1; i >= 0; i--)
            {
                if (_modifiers[i].IsAura) _modifiers.RemoveAt(i);
            }
        }

        /// <summary>모든 보정(오라, 일시)을 지운다. 유닛이 풀로 돌아갈 때 쓴다.</summary>
        public void Clear() => _modifiers.Clear();

        /// <summary>스탯에 현재 유효한 보정을 모두 합산해 반영한다. 지속 시간이 끝난 일시 보정은 여기서 정리한다.</summary>
        public UnitStats Apply(UnitStats stats)
        {
            float time = Time.time;
            float attackPowerRatio = 0f;
            float attackSpeedRatio = 0f;

            for (int i = _modifiers.Count - 1; i >= 0; i--)
            {
                Modifier modifier = _modifiers[i];
                if (modifier.IsAura == false && time >= modifier.ExpireTime)
                {
                    _modifiers.RemoveAt(i);
                    continue;
                }

                attackPowerRatio += modifier.AttackPowerRatio;
                attackSpeedRatio += modifier.AttackSpeedRatio;
            }

            if (attackPowerRatio == 0f && attackSpeedRatio == 0f) return stats;

            return new UnitStats(
                stats.AttackPower * Mathf.Max(0f, 1f + attackPowerRatio),
                stats.AttackSpeed * Mathf.Max(0f, 1f + attackSpeedRatio),
                stats.AttackRange);
        }

        Modifier FindOrAdd(Unit source, UnitSkill skill)
        {
            for (int i = 0; i < _modifiers.Count; i++)
            {
                if (_modifiers[i].Source == source && _modifiers[i].Skill == skill) return _modifiers[i];
            }

            Modifier modifier = new Modifier { Source = source, Skill = skill };
            _modifiers.Add(modifier);
            return modifier;
        }
    }
}