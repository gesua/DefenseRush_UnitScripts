namespace Defense.Units
{
    /// <summary>전투 재화를 지급받는 쪽(시스템)에서 구현할 인터페이스. 약탈 같은 스킬이 사용한다.</summary>
    public interface ICurrencyReceiver
    {
        /// <summary>amount만큼 재화를 지급한다. 지급하지 못했으면 false.</summary>
        bool AddCurrency(int amount);
    }
}
