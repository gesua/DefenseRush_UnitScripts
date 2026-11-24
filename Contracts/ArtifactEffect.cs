namespace Defense.Units
{
    /// <summary>
    /// 유닛 쪽이 읽는 유물 효과의 종류. 값은 IArtifactEffects.GetBonus로 읽는다.
    /// 비율 효과는 0.1 = +10%처럼 소수로 돌려준다. (재화/몬스터 쪽 효과는 해당 담당 코드에서 따로 다룬다)
    /// </summary>
    public enum ArtifactEffect
    {
        /// <summary>전체 공격력 증가 비율. (검)</summary>
        AttackPower,

        /// <summary>전체 공격속도 증가 비율. (장갑)</summary>
        AttackSpeed,

        /// <summary>유닛 이동속도 증가 비율. (신발)</summary>
        MoveSpeed,

        /// <summary>일반 소환에서 희귀가 나올 확률 증가. 전체 확률의 비율로, 0.02 = 2%p를 일반에서 희귀로 옮긴다.</summary>
        RareSummonChance,

        /// <summary>도박에서 영웅이 나올 확률 증가. 0.05 = 5%p를 기본 확률에 더한다.</summary>
        HeroGambleChance,

        /// <summary>필드에 둘 수 있는 유닛 최대 마릿수 증가량. 3 = 최대치 +3마리. (영웅 수 유물, 정수 값)</summary>
        UnitCapacity,

        /// <summary>도박에서 희귀가 나올 확률 증가. 0.05 = 5%p를 기본 확률에 더한다.</summary>
        RareGambleChance,

        /// <summary>도박에서 전설이 나올 확률 증가. 0.01 = 1%p를 기본 확률에 더한다.</summary>
        LegendGambleChance,

        /// <summary>일반 소환에서 영웅이 나올 확률 증가. RareSummonChance와 같은 방식으로 일반에서 영웅으로 옮긴다.</summary>
        HeroSummonChance,

        /// <summary>일반 소환에서 전설이 나올 확률 증가. RareSummonChance와 같은 방식으로 일반에서 전설로 옮긴다.</summary>
        LegendSummonChance,

        /// <summary>보스에게 주는 대미지 증가 비율. 0.1 = +10%.</summary>
        BossDamage,

        /// <summary>
        /// 스킬 피해 증가 비율. 0.15 = +15%.
        /// 피해를 직접 주는 스킬(폭탄, 연쇄 번개, 충격파, 발도, 멀티샷, 출혈)에만 적용하고,
        /// 일반 공격에 배수를 곱하는 스킬(치명타, 처형, 보스 사냥꾼)에는 적용하지 않는다.
        /// </summary>
        SkillDamage,

        /// <summary>스킬이 발동할 때 코인 1개를 추가로 얻을 확률. 0.05 = 5%.</summary>
        SkillGoldChance,

        /// <summary>유닛 판매가 증가 비율. 0.05 = +5%.</summary>
        SellGold,

        /// <summary>소환 비용 감소 비율. 0.1 = 10% 할인. (소환 비용을 결제하는 BattleEconomy가 읽는다)</summary>
        SummonCostDiscount,

        /// <summary>도박 비용 감소 비율. 0.1 = 10% 할인.</summary>
        GambleCostDiscount,

        /// <summary>인게임 강화(등급 강화, 소환 확률 강화) 비용 감소 비율. 0.1 = 10% 할인.</summary>
        UpgradeCostDiscount,

        /// <summary>도박에 실패했을 때 한 등급 아래 유닛을 대신 얻을 확률. 0.1 = 10%.</summary>
        GambleFailUnitChance,
    }
}