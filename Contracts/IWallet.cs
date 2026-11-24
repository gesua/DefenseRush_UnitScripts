namespace Defense.Units
{
    /// <summary>골드 차감을 담당하는 쪽(시스템)에서 구현할 인터페이스.</summary>
    public interface IWallet
    {
        bool TrySpend(int amount);
    }
}
