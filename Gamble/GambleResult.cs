namespace Defense.Units
{
    /// <summary>도박 한 번의 결과 종류.</summary>
    public enum GambleOutcome
    {
        /// <summary>성공. 재화를 내고 해당 등급의 유닛을 얻었다.</summary>
        Success,

        /// <summary>실패. 재화만 사라졌다.</summary>
        Miss,

        /// <summary>재화가 모자라 하지 못했다. (재화 소모 없음)</summary>
        NoFunds,

        /// <summary>빈 칸이 없어 하지 못했다. (재화 소모 없음)</summary>
        NoSpace,

        /// <summary>설정/데이터 문제로 할 수 없다. (재화 소모 없음)</summary>
        Unavailable,

        /// <summary>유닛 총 마릿수가 최대치라 하지 못했다. (재화 소모 없음)</summary>
        UnitLimit
    }

    /// <summary>
    /// 도박 한 번의 결과. 성공일 때만 Data(얻은 유닛)가 채워진다.
    /// 실패(Miss)해도 유물 효과로 한 등급 아래 유닛을 얻었으면 ConsolationData가 채워진다. (Outcome은 그대로 Miss)
    /// </summary>
    public readonly struct GambleResult
    {
        public UnitGrade Grade { get; }
        public GambleOutcome Outcome { get; }
        public UnitData Data { get; }

        /// <summary>실패했지만 유물 효과로 대신 얻은 하위 등급 유닛. 얻지 못했으면 null.</summary>
        public UnitData ConsolationData { get; }

        /// <summary>실패했지만 유물 효과로 하위 등급 유닛을 얻었는지.</summary>
        public bool HasConsolation => ConsolationData != null;

        /// <summary>재화가 실제로 쓰였는지. (성공 또는 실패)</summary>
        public bool IsPaid => Outcome == GambleOutcome.Success || Outcome == GambleOutcome.Miss;

        public GambleResult(UnitGrade grade, GambleOutcome outcome, UnitData data = null, UnitData consolationData = null)
        {
            Grade = grade;
            Outcome = outcome;
            Data = data;
            ConsolationData = consolationData;
        }
    }
}
