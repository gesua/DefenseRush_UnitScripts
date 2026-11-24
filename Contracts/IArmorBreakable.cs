namespace Defense.Units
{
    /// <summary>
    /// 방어력을 깎을 수 있는 몬스터가 구현하는 인터페이스. (몬스터 쪽이 IMonster와 함께 구현)
    /// 유물의 방어력 감소(Damageable.SetArmorReduction)와는 별도 채널이라 서로 덮어쓰지 않는다.
    /// 구현하지 않은 몬스터에게는 갑옷 파괴 스킬이 아무 일도 하지 않는다.
    /// </summary>
    public interface IArmorBreakable
    {
        /// <param name="source">건 유닛</param>
        /// <param name="skill">건 스킬. 같은 (source, skill)이 다시 걸리면 값과 지속 시간을 갱신한다.</param>
        /// <param name="flat">깎을 방어력(고정값)</param>
        /// <param name="percent">깎을 방어력 비율 (0~1). 0.3이면 30% 감소</param>
        /// <param name="duration">지속 시간(초)</param>
        /// <remarks>여러 출처가 동시에 걸려 있으면 고정값과 비율 각각 가장 큰 값 하나만 적용한다. (슬로우와 같은 방식)</remarks>
        void ApplyArmorBreak(Unit source, UnitSkill skill, float flat, float percent, float duration);
    }
}
