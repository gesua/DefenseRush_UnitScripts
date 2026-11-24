using UnityEngine;

namespace Defense.Units
{
    /// <summary>칸에 쌓인 유닛을 다음 등급의 랜덤 유닛으로 승급시킨다.</summary>
    public class PromotionManager : MonoBehaviour
    {
        [SerializeField] GridField _grid;
        [SerializeField] UnitDatabase _database;

        /// <summary>
        /// 칸이 가득 차 있고(3마리), 최고 등급이 아니며, 다음 등급의 유닛이 등록되어 있으면 승급 가능.
        /// </summary>
        public bool CanPromote(Slot slot) =>
            slot != null && slot.IsFull && slot.Type.Grade.IsMax() == false
            && _database != null && _database.HasUnits(slot.Type.Grade.Next());

        /// <summary>
        /// 유닛 전체를 제거하고 다음 등급의 랜덤 유닛 1마리를 같은 칸에 생성한다.
        /// 같은 종류가 이미 있고 자리가 남은 칸이 있으면, 그 칸에 바로 생성해서 합친다.
        /// </summary>
        public bool Promote(Slot slot)
        {
            if (_grid == null || _database == null || CanPromote(slot) == false) return false;

            UnitData next = _database.GetRandom(slot.Type.Grade.Next());
            if (next == null || next.Prefab == null) return false;

            // 같은 종류가 필드에 있으면 그 칸에, 없으면 승급한 칸에 생성한다.
            Slot target = _grid.FindStackSlot(next) ?? slot;

            _grid.ClearSlot(slot);
            return _grid.SpawnUnit(next, target) != null;
        }
    }
}