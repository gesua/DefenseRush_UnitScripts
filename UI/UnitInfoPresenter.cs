using UnityEngine;

namespace Defense.Units
{
    /// <summary>
    /// 칸을 탭해서 선택한 유닛의 정보를 유닛 정보창에 보여준다. UnitField(GridField + GridDragInput이 있는 오브젝트)에 붙인다.
    /// 선택 규칙은 RangeIndicator, SlotPromotePresenter와 같다.
    /// 같은 칸을 다시 탭하거나 빈 칸을 탭하면 선택을 풀고, 유닛을 집어 들면 닫는다.
    /// 오라 재계산이나 일시 버프 만료는 이벤트가 없어서, 선택한 유닛의 버프 증가량은 선택 중에 매 프레임 확인해 정보창에 넘긴다.
    /// 선택한 유닛은 유닛 프리뷰(UnitPreviewController)에도 넘겨 정면 모습을 보여주고, 선택이 풀리면 프리뷰를 닫는다.
    /// </summary>
    [RequireComponent(typeof(GridField), typeof(GridDragInput))]
    public class UnitInfoPresenter : MonoBehaviour
    {
        [Tooltip("유닛 정보창. 비워두면 씬에서 찾는다.")]
        [SerializeField] UnitInfoPanel _panel;
        [Tooltip("인게임 강화 상태. 정보창의 레벨 표시에 쓴다. 비워두면 레벨 1로 표시한다.")]
        [SerializeField] InGameProgress _progress;
        [Tooltip("선택한 유닛을 정면에서 보여주는 프리뷰. 비워두면 씬에서 찾고, 없으면 프리뷰 없이 정보창만 보여준다.")]
        [SerializeField] UnitPreviewController _preview;

        GridField _grid;
        GridDragInput _input;
        Slot _selected;
        Unit _buffUnit;

        void Awake()
        {
            TryGetComponent(out _grid);
            TryGetComponent(out _input);
        }

        // 정보창은 다른 프리팹(UI)에 있을 수 있어서, 모든 Awake가 끝난 Start에서 찾는다.
        void Start()
        {
            if (_panel == null) _panel = FindFirstObjectByType<UnitInfoPanel>(FindObjectsInactive.Include);
            if (_panel == null) Debug.LogWarning("[UnitInfoPresenter] 씬에서 UnitInfoPanel을 찾지 못했습니다.", this);

            if (_preview == null) _preview = FindFirstObjectByType<UnitPreviewController>(FindObjectsInactive.Include);
        }

        void OnEnable()
        {
            _input.SlotTapped += OnSlotTapped;
            _input.DragStarted += OnDragStarted;
            _input.BackgroundTapped += OnBackgroundTapped;
            _grid.SlotChanged += OnSlotChanged;
            if (_progress != null) _progress.LevelChanged += OnLevelChanged;
        }

        void OnDisable()
        {
            _input.SlotTapped -= OnSlotTapped;
            _input.DragStarted -= OnDragStarted;
            _input.BackgroundTapped -= OnBackgroundTapped;
            _grid.SlotChanged -= OnSlotChanged;
            if (_progress != null) _progress.LevelChanged -= OnLevelChanged;
        }

        void Update()
        {
            if (_panel == null || _selected == null || _selected.IsEmpty) return;

            UpdateBuff();
        }

        void OnSlotTapped(Slot slot)
        {
            _selected = (slot == _selected || slot.IsEmpty) ? null : slot;
            Refresh();
        }

        // 빈 배경을 탭하면 선택을 풀어서 정보창을 닫는다.
        void OnBackgroundTapped()
        {
            _selected = null;
            Refresh();
        }

        // 유닛을 집어 드는 순간 선택을 풀어서, 옮기는 도중에 정보창이 남아 있지 않게 한다.
        void OnDragStarted(Slot slot)
        {
            _selected = null;
            Refresh();
        }

        // 선택한 칸의 내용이 바뀌면(소환, 이동, 승급) 다시 그린다. 칸이 비면 선택도 푼다.
        void OnSlotChanged(Slot slot)
        {
            if (slot != _selected) return;

            if (slot.IsEmpty) _selected = null;
            Refresh();
        }

        // 인게임 강화는 즉시 반영되므로, 선택한 유닛의 등급이 강화되면 다시 그린다.
        void OnLevelChanged(UnitGrade grade)
        {
            if (_selected != null && _selected.Type.Grade == grade) Refresh();
        }

        void Refresh()
        {
            if (_panel == null) return;

            if (_selected == null)
            {
                _buffUnit = null;
                _panel.Hide();
                if (_preview != null) _preview.Hide();
                return;
            }

            UnitData data = _selected.Type;
            Unit unit = _selected.Units[0];
            int level = (_progress != null) ? _progress.GetLevel(data.Grade) : 1;
            StatBonus nextLevelIncrease = (_progress != null) ? _progress.GetNextLevelIncrease(data.Grade) : default;
            bool hasNextLevel = (_progress != null) && (_progress.IsMaxLevel(data.Grade) == false);

            // 다른 유닛으로 바뀌면 이전 유닛의 버프 표시를 바로 지운다.
            if (unit != _buffUnit)
            {
                _buffUnit = unit;
                _panel.ClearBuff();
            }

            _panel.Show(data, unit.Stats, level, nextLevelIncrease, hasNextLevel);
            if (_preview != null) _preview.Show(unit);
            UpdateBuff();
        }

        // 버프 증가량 = 보정까지 반영한 전투 스탯 - 보정 전 스탯. 영구 강화(유물, 유닛, 인게임)는 Stats에 이미 들어 있으므로 버프만 남는다.
        void UpdateBuff()
        {
            Unit unit = _selected.Units[0];
            UnitStats stats = unit.Stats;
            UnitStats combat = unit.Modifiers.Apply(stats);
            _panel.SetBuff(combat.AttackPower - stats.AttackPower, combat.AttackSpeed - stats.AttackSpeed);
        }
    }
}