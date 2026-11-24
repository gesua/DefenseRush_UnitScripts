namespace Defense.Units
{
    /// <summary>
    /// 로비에서 강화한 유물 레벨(GameManager의 ArtifactModel)을 전투 쪽 효과 종류로 바꿔 읽는 어댑터.
    /// ArtifactModel은 비율 유물을 이미 소수(0.1 = 10%)로 돌려주므로 그대로 쓴다.
    /// GameManager는 로비를 거쳐 들어왔을 때만 있으므로, 없으면 TryCreate가 null을 돌려준다.
    /// </summary>
    public class GameModelArtifactEffects : IArtifactEffects
    {
        /// <summary>GameManager가 있으면 어댑터를, 없으면(전투 씬만 실행) null을 돌려준다.</summary>
        public static IArtifactEffects TryCreate() => (GameManager.Instance != null) ? new GameModelArtifactEffects() : null;

        public float GetBonus(ArtifactEffect effect)
        {
            GameManager manager = GameManager.Instance;
            if (manager == null || manager.GameModel == null || manager.GameModel.ArtifactModel == null) return 0f;

            ArtifactModel model = manager.GameModel.ArtifactModel;
            switch (effect)
            {
                case ArtifactEffect.AttackPower: return model.GetArtifactValue(ArtifactId.UnitDamage_P);
                case ArtifactEffect.AttackSpeed: return model.GetArtifactValue(ArtifactId.UnitAttackSpeed_P);
                case ArtifactEffect.MoveSpeed: return model.GetArtifactValue(ArtifactId.UnitMoveSpeed_P);
                case ArtifactEffect.RareSummonChance: return model.GetArtifactValue(ArtifactId.RareSummonChance_P);
                case ArtifactEffect.HeroSummonChance: return model.GetArtifactValue(ArtifactId.HeroSummonChance_P);
                case ArtifactEffect.LegendSummonChance: return model.GetArtifactValue(ArtifactId.LegendSummonChance_P);
                case ArtifactEffect.RareGambleChance: return model.GetArtifactValue(ArtifactId.RareGachaChance_P);
                case ArtifactEffect.HeroGambleChance: return model.GetArtifactValue(ArtifactId.HeroGachaChance_P);
                case ArtifactEffect.LegendGambleChance: return model.GetArtifactValue(ArtifactId.LegendGachaChance_P);
                case ArtifactEffect.UnitCapacity: return model.GetArtifactValue(ArtifactId.UnitLimitCount_F);
                case ArtifactEffect.BossDamage: return model.GetArtifactValue(ArtifactId.UnitBossDamage_P);
                case ArtifactEffect.SkillDamage: return model.GetArtifactValue(ArtifactId.UnitSkillDamage_P);
                case ArtifactEffect.SkillGoldChance: return model.GetArtifactValue(ArtifactId.UnitSkillGoldChance_P);
                case ArtifactEffect.SellGold: return model.GetArtifactValue(ArtifactId.SellUnitGold_P);
                case ArtifactEffect.SummonCostDiscount: return model.GetArtifactValue(ArtifactId.SummonCost_P);
                case ArtifactEffect.GambleCostDiscount: return model.GetArtifactValue(ArtifactId.GachaCost_P);
                case ArtifactEffect.UpgradeCostDiscount: return model.GetArtifactValue(ArtifactId.UpgradeCost_P);
                case ArtifactEffect.GambleFailUnitChance: return model.GetArtifactValue(ArtifactId.SpawnLowerGradeUnitOnGachaFailure_P);
                default: return 0f;
            }
        }
    }
}