namespace Defense.Units
{
    /// <summary>
    /// 지속 피해(출혈)를 받을 수 있는 몬스터가 구현하는 인터페이스. (몬스터 쪽이 IMonster와 함께 구현)
    /// 구현하지 않은 몬스터에게는 출혈 스킬이 아무 일도 하지 않는다.
    /// </summary>
    /// <remarks>
    /// 몬스터는 틱마다 MonsterSkillDamage.Apply(this, 피해, 종류, source)로 피해를 줘야
    /// 지속 피해로 죽었을 때도 source 유닛의 처치 효과(OnKill)가 실행된다.
    /// </remarks>
    public interface IDotReceiver
    {
        /// <param name="source">피해를 건 유닛. 처치 효과를 알릴 대상이다. (중간에 사라져도 피해는 이어진다)</param>
        /// <param name="skill">건 스킬. 같은 (source, skill)이 다시 걸리면 값과 지속 시간만 갱신하고, 서로 다른 출처는 합산한다.</param>
        /// <param name="damagePerSecond">초당 피해(방어력 적용 전)</param>
        /// <param name="duration">지속 시간(초)</param>
        void ApplyDot(Unit source, UnitSkill skill, float damagePerSecond, float duration);
    }
}
