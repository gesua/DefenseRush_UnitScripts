using System.Collections.Generic;
using UnityEngine;

namespace Defense.Units
{
    /// <summary>일시 버프: 공격할 때 일정 확률로 인접 칸 아군의 공격력/공격속도를 잠시 올려 준다.</summary>
    /// <remarks>같은 유닛이 같은 스킬로 다시 걸면 지속 시간만 갱신되고, 서로 다른 유닛이 건 버프는 합산된다.</remarks>
    [CreateAssetMenu(fileName = "Skill_TempBuff", menuName = "Defense/Skill/Temp Buff (일시 버프)")]
    public class TempBuffSkill : UnitSkill
    {
        // 인접 칸 목록 (스킬 에셋 공용 버퍼)
        static readonly List<Slot> _slots = new List<Slot>();

        [Tooltip("발동 확률 (0~1). 1이면 공격할 때마다 건다.")]
        [Range(0f, 1f)][SerializeField] float _chance = 0.25f;
        [Tooltip("올려 줄 공격력 비율. 0.3이면 +30%")]
        [Min(0f)][SerializeField] float _attackPowerRatio;
        [Tooltip("올려 줄 공격속도 비율. 0.3이면 +30%")]
        [Min(0f)][SerializeField] float _attackSpeedRatio;
        [Tooltip("지속 시간(초). 다시 걸리면 갱신된다.")]
        [Min(0f)][SerializeField] float _duration = 2f;
        [Tooltip("true면 대각선 칸도 인접으로 본다.")]
        [SerializeField] bool _includeDiagonal;

        [Header("바닥 표시")]
        [Tooltip("평상시 바닥에 영향 범위를 괄호로 표시할지. 확률로 걸리는 효과라 오라(칸 색)와 모양을 달리한다.")]
        [SerializeField] bool _showFloorMark = true;
        [Tooltip("true면 아래 색을 쓰고, false면 올려 주는 스탯에 맞춰 자동으로 정한다. (공격력 주황, 공격속도 하늘색) 투명도는 무시된다.")]
        [SerializeField] bool _useCustomFloorColor;
        [SerializeField] Color _floorColor = Color.white;

        // 에디터에서 값을 바꿀 때, 아무 효과가 없는 설정을 바로 알려 준다.
        void OnValidate()
        {
            if (_attackPowerRatio <= 0f && _attackSpeedRatio <= 0f)
                Debug.LogWarning($"[TempBuffSkill] {name}: 공격력 비율과 공격속도 비율이 모두 0이라 아무 효과가 없습니다.", this);
            if (_duration <= 0f)
                Debug.LogWarning($"[TempBuffSkill] {name}: 지속 시간이 0 이하라 효과가 적용되지 않습니다.", this);
        }

        public override void OnAttack(AttackContext context)
        {
            if (_chance < 1f && Random.value >= _chance) return;
            if (_duration <= 0f || (_attackPowerRatio <= 0f && _attackSpeedRatio <= 0f)) return;

            Unit source = context.Attacker;

            _slots.Clear();
            CollectTargetSlots(source, _slots);

            bool applied = false;
            for (int i = 0; i < _slots.Count; i++)
            {
                IReadOnlyList<Unit> units = _slots[i].Units;
                for (int j = 0; j < units.Count; j++)
                {
                    units[j].Modifiers.SetTimed(source, this, _attackPowerRatio, _attackSpeedRatio, _duration);
                    applied = true;
                }
            }
            _slots.Clear();

            // 버프를 받은 아군이 없으면 연출도 보내지 않는다.
            if (applied) NotifyVisual(SkillVisualKind.Buff, source);
        }

        /// <summary>버프가 닿는 인접 칸을 results에 담는다. OnAttack과 바닥 표시가 같은 범위를 쓴다. (results는 호출한 쪽이 비워서 넘긴다)</summary>
        public void CollectTargetSlots(Unit unit, List<Slot> results)
        {
            Slot slot = unit.Slot;
            if (slot == null || slot.Grid == null) return;

            slot.Grid.GetAdjacentSlots(slot, _includeDiagonal, results);
        }

        public override bool TryGetFloorMark(Unit unit, List<Slot> results, out Color color, out FloorMarkStyle style)
        {
            color = (_useCustomFloorColor) ? _floorColor : GetStatFloorColor(_attackPowerRatio, _attackSpeedRatio);
            style = FloorMarkStyle.Brackets;

            if (_showFloorMark == false) return false;
            if (_duration <= 0f || (_attackPowerRatio <= 0f && _attackSpeedRatio <= 0f)) return false;

            CollectTargetSlots(unit, results);
            return results.Count > 0;
        }
    }
}