using System.Collections.Generic;
using UnityEngine;

namespace Defense.Units
{
    /// <summary>
    /// 라운드가 시작될 때 필드에 있는 모든 유닛의 스킬에 OnRoundStart를 알린다. (수확)
    /// 씬에 하나만 두고, grid에 GridField를, roundSource에 IRoundSource를 구현한 컴포넌트(EnemySpawner)를 연결한다.
    /// </summary>
    public class UnitRoundSkillRunner : MonoBehaviour
    {
        [Tooltip("유닛이 서 있는 그리드.")]
        [SerializeField] GridField _grid;

        [Tooltip("IRoundSource를 구현한 컴포넌트(EnemySpawner).")]
        [SerializeField] MonoBehaviour _roundSource;

        IRoundSource _source;

        void Awake()
        {
            _source = _roundSource as IRoundSource;
            if (_source == null)
                Debug.LogError("[UnitRoundSkillRunner] roundSource가 비어 있거나 IRoundSource를 구현하지 않았습니다. 수확 같은 라운드 시작 스킬이 작동하지 않습니다.", this);

            if (_grid == null)
                Debug.LogError("[UnitRoundSkillRunner] grid가 비어 있습니다. 수확 같은 라운드 시작 스킬이 작동하지 않습니다.", this);
        }

        void OnEnable()
        {
            if (_source != null) _source.RoundStarted += OnRoundStarted;
        }

        void OnDisable()
        {
            if (_source != null) _source.RoundStarted -= OnRoundStarted;
        }

        void OnRoundStarted(int round)
        {
            if (_grid == null) return;

            IReadOnlyList<Slot> slots = _grid.Slots;
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
                        if (skills[k] != null) skills[k].OnRoundStart(unit, round);
                    }
                }
            }
        }
    }
}