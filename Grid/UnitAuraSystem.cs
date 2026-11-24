using System;
using System.Collections.Generic;

namespace Defense.Units
{
    /// <summary>
    /// 오라 스킬(칸 구성이 바뀔 때만 달라지는 상시 효과)을 관리한다.
    /// 소환, 이동, 판매, 승급은 모두 GridField.SlotChanged를 지나므로, 그때마다 모든 유닛의 오라 보정을 지우고 다시 건다.
    /// 그래서 유닛이 빠지거나 옮겨져도 오라가 남지 않고, 평소(프레임마다)에는 계산 비용이 들지 않는다.
    /// </summary>
    public class UnitAuraSystem : IDisposable
    {
        readonly GridField _grid;

        public UnitAuraSystem(GridField grid)
        {
            _grid = grid;
            _grid.SlotChanged += OnSlotChanged;
        }

        public void Dispose()
        {
            if (_grid != null) _grid.SlotChanged -= OnSlotChanged;
        }

        void OnSlotChanged(Slot changed) => Refresh();

        void Refresh()
        {
            IReadOnlyList<Slot> slots = _grid.Slots;

            // 1) 이전에 걸린 오라 보정을 모든 유닛에서 지운다.
            for (int i = 0; i < slots.Count; i++)
            {
                IReadOnlyList<Unit> units = slots[i].Units;
                for (int j = 0; j < units.Count; j++)
                {
                    units[j].Modifiers.ClearAuras();
                }
            }

            // 2) 지금 칸 구성에 맞게 오라 스킬을 다시 건다.
            for (int i = 0; i < slots.Count; i++)
            {
                IReadOnlyList<Unit> units = slots[i].Units;
                for (int j = 0; j < units.Count; j++)
                {
                    Unit unit = units[j];
                    if (unit.Data == null) continue;

                    IReadOnlyList<UnitSkill> skills = unit.Data.Skills;
                    for (int k = 0; k < skills.Count; k++)
                    {
                        if (skills[k] != null) skills[k].ApplyAura(unit);
                    }
                }
            }
        }
    }
}
