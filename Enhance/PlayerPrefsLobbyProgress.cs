using UnityEngine;

namespace Defense.Units
{
    /// <summary>
    /// [임시] PlayerPrefs에 저장하는 기본 구현.
    /// 세이브 시스템이 정해지면 ILobbyProgress를 새로 구현해서 교체한다.
    /// </summary>
    public class PlayerPrefsLobbyProgress : ILobbyProgress
    {
        const string KEY_PREFIX = "lobby_unit_level_";

        public int GetLevel(UnitData data) =>
            UnitLevel.Clamp(PlayerPrefs.GetInt(KEY_PREFIX + data.Id, 1));

        public void SetLevel(UnitData data, int level)
        {
            PlayerPrefs.SetInt(KEY_PREFIX + data.Id, UnitLevel.Clamp(level));
            PlayerPrefs.Save();
        }
    }
}