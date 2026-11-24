namespace Defense.Units
{
    /// <summary>
    /// 이동속도를 늦출 수 있는 몬스터가 구현하는 인터페이스. (몬스터 쪽이 IMonster와 함께 구현)
    /// 구현하지 않은 몬스터에게는 슬로우 스킬이 아무 일도 하지 않는다.
    /// </summary>
    public interface ISlowable
    {
        /// <param name="amount">줄일 이동속도(월드 단위/초)</param>
        /// <param name="duration">유지 시간(초). 다시 맞으면 갱신한다.</param>
        void ApplySlow(float amount, float duration);
    }
}
