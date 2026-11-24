using System;
using UnityEngine;

namespace Defense.Units
{
    /// <summary>유닛 레벨 규칙. 최대 레벨은 15로 고정, 레벨은 1부터 시작한다.</summary>
    public static class UnitLevel
    {
        public const int MAX = 15;

        public static int Clamp(int level) => Mathf.Clamp(level, 1, MAX);
    }

    /// <summary>레벨에 따라 더해지는 추가 능력치. (Inspector에서 표로 입력)</summary>
    [Serializable]
    public struct StatBonus
    {
        public float AttackPower;
        public float AttackSpeed;
        public float AttackRange;
    }

    /// <summary>기본 스탯 + 로비 강화 + 인게임 강화가 모두 반영된 최종 스탯.</summary>
    public readonly struct UnitStats
    {
        public readonly float AttackPower;
        public readonly float AttackSpeed;
        public readonly float AttackRange;

        public UnitStats(float attackPower, float attackSpeed, float attackRange)
        {
            this.AttackPower = attackPower;
            this.AttackSpeed = attackSpeed;
            this.AttackRange = attackRange;
        }

        /// <summary>공격 간격(초). 공격 속도가 0 이하면 사실상 공격하지 않는다.</summary>
        public float AttackInterval => (AttackSpeed > 0f) ? 1f / AttackSpeed : float.MaxValue;

        public static UnitStats operator +(UnitStats stats, StatBonus bonus) => new UnitStats(
            stats.AttackPower + bonus.AttackPower,
            stats.AttackSpeed + bonus.AttackSpeed,
            stats.AttackRange + bonus.AttackRange);
    }
}