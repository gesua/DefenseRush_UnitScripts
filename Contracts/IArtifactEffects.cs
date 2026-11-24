namespace Defense.Units
{
    /// <summary>
    /// 로비에서 장착/강화한 유물의 효과를 읽는 계약. (유물/저장 쪽이 구현)
    /// 유물이 없거나 해당 효과가 없으면 0을 돌려준다. 자주 불리므로 가볍게 구현할 것.
    /// </summary>
    public interface IArtifactEffects
    {
        /// <summary>해당 효과의 현재 합계. 기본 값 + 레벨당 증가분이 모두 반영된 값.</summary>
        float GetBonus(ArtifactEffect effect);
    }
}
