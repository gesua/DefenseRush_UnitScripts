namespace Defense.Units
{
    /// <summary>
    /// 몬스터의 체력을 읽을 수 있게 하는 인터페이스. (몬스터 쪽이 IMonster와 함께 구현)
    /// 구현하지 않은 몬스터에게는 체력 조건이 붙은 스킬(처형)이 아무 일도 하지 않는다.
    /// </summary>
    public interface IMonsterHealth
    {
        float CurrentHealth { get; }
        float MaximumHealth { get; }

        /// <summary>현재 체력 / 최대 체력 (0~1). 가득 차 있으면 1.</summary>
        float HealthRatio { get; }
    }
}
