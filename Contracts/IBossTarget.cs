namespace Defense.Units
{
    /// <summary>
    /// 보스인지 알려 주는 몬스터가 구현하는 인터페이스. (몬스터 쪽이 IMonster와 함께 구현)
    /// 구현하지 않은 몬스터는 보스가 아닌 것으로 본다. 보스 사냥꾼 스킬이 사용한다.
    /// </summary>
    public interface IBossTarget
    {
        bool IsBoss { get; }
    }
}
