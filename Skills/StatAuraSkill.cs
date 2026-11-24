using System.Collections.Generic;
using UnityEngine;

namespace Defense.Units
{
    /// <summary>오라가 닿는 범위. Adjacent는 인접 칸(자기 칸 제외), All은 필드의 모든 아군(자기 자신 제외)이다.</summary>
    public enum AuraScope { Adjacent, All }

    /// <summary>
    /// 스탯 오라: 칸 구성이 바뀔 때마다 범위 안 아군의 공격력/공격속도를 올려 준다. (전의 고취, 사기 증가)
    /// 같은 오라를 가진 유닛이 여럿이면 각각 따로 합산된다.
    /// </summary>
    [CreateAssetMenu(fileName = "Skill_StatAura", menuName = "Defense/Skill/Stat Aura (스탯 오라)")]
    public class StatAuraSkill : UnitSkill
    {
        // 오라가 닿는 칸 목록 (스킬 에셋 공용 버퍼)
        static readonly List<Slot> _slots = new List<Slot>();

        [Tooltip("Adjacent: 인접 칸 아군 / All: 필드의 모든 아군")]
        [SerializeField] AuraScope _scope = AuraScope.Adjacent;
        [Tooltip("true면 대각선 칸도 인접으로 본다. (범위가 Adjacent일 때만)")]
        [SerializeField] bool _includeDiagonal;
        [Tooltip("올려 줄 공격력 비율. 0.2면 +20%")]
        [Min(0f)][SerializeField] float _attackPowerRatio;
        [Tooltip("올려 줄 공격속도 비율. 0.2면 +20%")]
        [Min(0f)][SerializeField] float _attackSpeedRatio;

        [Header("바닥 표시")]
        [Tooltip("평상시 바닥에 영향 범위를 칸 색으로 표시할지. 범위가 All이면 표시하지 않는다.")]
        [SerializeField] bool _showFloorMark = true;
        [Tooltip("true면 아래 색을 쓰고, false면 올려 주는 스탯에 맞춰 자동으로 정한다. (공격력 주황, 공격속도 하늘색) 투명도는 무시된다.")]
        [SerializeField] bool _useCustomFloorColor;
        [SerializeField] Color _floorColor = Color.white;

        // 에디터에서 값을 바꿀 때, 아무 효과가 없는 설정을 바로 알려 준다.
        void OnValidate()
        {
            if (_attackPowerRatio <= 0f && _attackSpeedRatio <= 0f)
                Debug.LogWarning($"[StatAuraSkill] {name}: 공격력 비율과 공격속도 비율이 모두 0이라 아무 효과가 없습니다.", this);
        }

        public override void ApplyAura(Unit unit)
        {
            if (_attackPowerRatio <= 0f && _attackSpeedRatio <= 0f) return;

            _slots.Clear();
            CollectTargetSlots(unit, _slots);
            GiveToSlots(unit, _slots);
            _slots.Clear();
        }

        /// <summary>오라가 닿는 칸을 results에 담는다. ApplyAura와 바닥 표시가 같은 범위를 쓴다. (results는 호출한 쪽이 비워서 넘긴다)</summary>
        public void CollectTargetSlots(Unit unit, List<Slot> results)
        {
            Slot sourceSlot = unit.Slot;
            if (sourceSlot == null || sourceSlot.Grid == null) return;

            if (_scope == AuraScope.All)
            {
                results.AddRange(sourceSlot.Grid.Slots);
                return;
            }

            sourceSlot.Grid.GetAdjacentSlots(sourceSlot, _includeDiagonal, results);
        }

        public override bool TryGetFloorMark(Unit unit, List<Slot> results, out Color color, out FloorMarkStyle style)
        {
            color = (_useCustomFloorColor) ? _floorColor : GetStatFloorColor(_attackPowerRatio, _attackSpeedRatio);
            style = FloorMarkStyle.Tile;

            // 필드 전체에 닿는 오라는 모든 칸이 물들어 의미가 없으므로 표시하지 않는다.
            if (_showFloorMark == false || _scope == AuraScope.All) return false;
            if (_attackPowerRatio <= 0f && _attackSpeedRatio <= 0f) return false;

            CollectTargetSlots(unit, results);
            return results.Count > 0;
        }

        void GiveToSlots(Unit source, IReadOnlyList<Slot> slots)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                IReadOnlyList<Unit> units = slots[i].Units;
                for (int j = 0; j < units.Count; j++)
                {
                    if (units[j] == source) continue;

                    units[j].Modifiers.SetAura(source, this, _attackPowerRatio, _attackSpeedRatio);
                }
            }
        }
    }
}