namespace Defense.Units
{
    /// <summary>
    /// 라운드가 시작되는 순간을 알려 주는 쪽(시스템)에서 구현할 인터페이스. (EnemySpawner가 구현)
    /// 라운드가 시작될 때 실행되는 스킬(수확)이 UnitRoundSkillRunner를 통해 이 알림을 받는다.
    /// </summary>
    public interface IRoundSource
    {
        /// <summary>새 라운드가 시작될 때 호출된다. 인자는 시작한 라운드 번호.</summary>
        event System.Action<int> RoundStarted;
    }
}