using UnityEngine;

namespace Defense.Units
{
    /// <summary>등급을 나타내는 기본 색. 유닛 발밑 원, 유닛 정보창 등이 같이 쓴다.</summary>
    public static class UnitGradeColors
    {
        static readonly Color[] _colors =
        {
            Color.white,                  // 일반
            new Color(0.25f, 0.55f, 1f),  // 희귀
            new Color(0.7f, 0.35f, 1f),   // 영웅
            new Color(1f, 0.6f, 0.1f)     // 전설
        };

        /// <summary>등급의 기본 색. 색이 정해지지 않은 등급은 흰색.</summary>
        public static Color Get(UnitGrade grade)
        {
            int index = (int)grade;
            return (index < _colors.Length) ? _colors[index] : Color.white;
        }
    }
}
