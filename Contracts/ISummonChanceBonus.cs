namespace Defense.Units
{
    /// <summary>
    /// 필드에 있는 동안 희귀 이상 소환 확률을 높이는 스킬이 구현하는 인터페이스. (축복)
    /// SummonChanceProgress가 필드에 있는 유닛의 스킬 중 이것을 구현한 것의 값을 모두 더해 소환 확률에 반영한다.
    /// </summary>
    public interface ISummonChanceBonus
    {
        /// <summary>전체 소환 가중치 대비, 일반에서 희귀 이상으로 옮길 비율. 0.02면 일반 소환 확률 2%p가 희귀 이상으로 옮겨진다.</summary>
        float RareOrHigherShift { get; }
    }
}