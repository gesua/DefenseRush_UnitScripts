namespace Defense.Units
{
    /// <summary>
    /// 기절시킬 수 있는 몬스터가 구현하는 인터페이스. (몬스터 쪽이 IMonster와 함께 구현)
    /// 기절한 동안은 이동을 멈춘다. 구현하지 않은 몬스터에게는 충격파의 기절이 아무 일도 하지 않는다.
    /// </summary>
    public interface IStunnable
    {
        /// <param name="duration">기절 시간(초). 이미 기절 중이면 더 긴 쪽이 남는다. (중첩으로 늘어나지 않는다)</param>
        void ApplyStun(float duration);
    }
}
