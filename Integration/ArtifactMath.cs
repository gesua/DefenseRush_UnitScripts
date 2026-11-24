using UnityEngine;

namespace Defense.Units
{
    /// <summary>
    /// 유물의 비율 효과를 정수 값(비용, 판매가)에 적용하는 계산 모음.
    /// 올림/반올림 규칙을 한 곳에서만 정하도록 모았다.
    /// </summary>
    public static class ArtifactMath
    {
        // 부동소수 오차(45.000004 같은 값)가 올림으로 1 커지는 것을 막는 여유값.
        const double EPSILON = 0.0001;

        /// <summary>
        /// 비용에 감소 비율을 적용한다. 0.1 = 10% 할인. 소수점은 올리고, 할인해도 최소 1이다.
        /// 0 이하(무료, 또는 '없음'을 뜻하는 -1)는 그대로 돌려준다.
        /// </summary>
        public static int ApplyDiscount(int cost, float rate)
        {
            if (cost <= 0 || rate <= 0f) return cost;

            double discounted = cost * (1d - Mathf.Clamp01(rate));
            return Mathf.Max(1, (int)System.Math.Ceiling(discounted - EPSILON));
        }

        /// <summary>값에 증가 비율을 적용한다. 0.05 = +5%. 소수점은 반올림한다. 0 이하는 그대로 돌려준다.</summary>
        public static int ApplyIncrease(int value, float rate)
        {
            if (value <= 0 || rate <= 0f) return value;

            return (int)System.Math.Floor(value * (1d + rate) + 0.5d);
        }
    }
}
