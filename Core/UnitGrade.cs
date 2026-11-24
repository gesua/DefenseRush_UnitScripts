namespace Defense.Units
{
    /// <summary>유닛 등급. 숫자가 클수록 높은 등급이며, 0부터 연속된 값이어야 한다.</summary>
    public enum UnitGrade
    {
        Common = 0,   // 일반
        Rare = 1,     // 희귀
        Hero = 2,     // 영웅
        Legendary = 3 // 전설
    }

    /// <summary>등급 전체에 대한 값. 등급을 추가해도 여기서 자동으로 맞춰진다.</summary>
    public static class UnitGrades
    {
        /// <summary>등급 개수.</summary>
        public static readonly int Count = System.Enum.GetValues(typeof(UnitGrade)).Length;

        /// <summary>가장 높은 등급의 번호.</summary>
        public static readonly int MaxIndex = Count - 1;
    }

    public static class UnitGradeExtensions
    {
        public static bool IsMax(this UnitGrade grade) => (int)grade >= UnitGrades.MaxIndex;

        public static bool IsMin(this UnitGrade grade) => (int)grade <= 0;

        /// <summary>등급별로 가질 수 있는 스킬 개수. 일반/희귀 1개, 영웅/전설 2개.</summary>
        public static int SkillSlotCount(this UnitGrade grade) => (grade >= UnitGrade.Hero) ? 2 : 1;

        /// <summary>다음 등급. 이미 최고 등급이면 그대로 반환한다.</summary>
        public static UnitGrade Next(this UnitGrade grade) => (grade.IsMax()) ? grade : grade + 1;

        /// <summary>이전(한 단계 아래) 등급. 이미 최저 등급이면 그대로 반환한다.</summary>
        public static UnitGrade Previous(this UnitGrade grade) => (grade.IsMin()) ? grade : grade - 1;
    }
}