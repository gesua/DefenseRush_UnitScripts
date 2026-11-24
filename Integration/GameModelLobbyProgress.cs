namespace Defense.Units
{
    /// <summary>
    /// 로비에서 올린 유닛 레벨(GameManager의 UnitModel에 저장됨)을 전투 쪽에서 읽는 어댑터.
    /// GameManager는 로비를 거쳐 들어왔을 때만 있으므로, 없으면 TryCreate가 null을 돌려준다.
    /// </summary>
    public class GameModelLobbyProgress : ILobbyProgress
    {
        /// <summary>GameManager가 있으면 어댑터를, 없으면(전투 씬만 실행) null을 돌려준다.</summary>
        public static ILobbyProgress TryCreate() => (GameManager.Instance != null) ? new GameModelLobbyProgress() : null;

        public int GetLevel(UnitData data)
        {
            GameManager manager = GameManager.Instance;
            if (manager == null || manager.GameModel == null || manager.GameModel.UnitModel == null) return 1;

            // UnitModel은 저장 데이터가 없는 유닛에 0을 돌려주므로 1~최대 레벨로 보정한다.
            return UnitLevel.Clamp(manager.GameModel.UnitModel.GetUpgradeLevel(data.Id));
        }
    }
}