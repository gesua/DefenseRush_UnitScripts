namespace Defense.Units
{
    /// <summary>
    /// 지금의 소환 비용을 알려주는 쪽. (전투 재화를 관리하는 BattleEconomy가 구현)
    /// 소환 비용에 비례해서 코인을 주는 스킬(약탈)이 읽는다. 구현하지 않으면 그 스킬은 오류를 남기고 코인을 주지 않는다.
    /// </summary>
    public interface ISummonCostSource
    {
        int CurrentSummonCost { get; }
    }
}