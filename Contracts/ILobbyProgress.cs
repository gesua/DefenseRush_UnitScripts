namespace Defense.Units
{
    /// <summary>로비에서 올린 유닛 종류별 강화 레벨을 읽는 계약. (저장/로비 UI 쪽이 구현)</summary>
    public interface ILobbyProgress
    {
        /// <summary>해당 유닛 종류의 로비 레벨(1~15). 강화한 적 없으면 1.</summary>
        int GetLevel(UnitData data);
    }
}
