namespace Defense.Units
{
    /// <summary>유닛의 최종 스탯을 돌려주는 쪽. (Unit이 공격할 때마다 읽는다)</summary>
    public interface IUnitStatProvider
    {
        UnitStats GetStats(UnitData data);

        /// <summary>칸 이동/재배치 속도에 곱하는 배율. 1이면 기본 속도.</summary>
        float MoveSpeedMultiplier { get; }

        /// <summary>스킬 피해(폭탄, 연쇄 번개 등)에 곱하는 배율. 1이면 그대로.</summary>
        float SkillDamageMultiplier { get; }

        /// <summary>보스에게 주는 대미지에 곱하는 배율. 1이면 그대로.</summary>
        float BossDamageMultiplier { get; }

        /// <summary>스킬이 발동할 때 코인 1개를 추가로 얻을 확률(0~1).</summary>
        float SkillGoldChance { get; }
    }
}