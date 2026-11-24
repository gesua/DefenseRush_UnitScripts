using TMPro;
using UnityEngine;

namespace Defense.Units
{
    /// <summary>
    /// 필드에 있는 유닛 수와 최대치를 "9 / 20" 형태로 표시한다.
    /// 최대치에 도달하면 글자색을 바꾼다.
    /// </summary>
    public class UnitCountHud : MonoBehaviour
    {
        [SerializeField] GridField _grid;
        [SerializeField] TMP_Text _label;

        [Header("표시")]
        [SerializeField] Color _normalColor = Color.white;
        [Tooltip("최대치에 도달했을 때의 글자색")]
        [SerializeField] Color _fullColor = new Color(1f, 0.45f, 0.4f);

        bool HasReferences => _grid != null && _label != null;

        void Awake()
        {
            if (HasReferences == false)
                Debug.LogError("[UnitCountHud] grid / label 참조가 비어 있습니다.", this);
        }

        void OnEnable()
        {
            if (HasReferences == false) return;

            _grid.SlotChanged += OnSlotChanged;
            Refresh();
        }

        void OnDisable()
        {
            if (_grid != null) _grid.SlotChanged -= OnSlotChanged;
        }

        void OnSlotChanged(Slot slot) => Refresh();

        void Refresh()
        {
            _label.text = $"{_grid.TotalUnitCount} / {_grid.MaxTotalUnits}";
            _label.color = (_grid.IsUnitLimitReached) ? _fullColor : _normalColor;
        }
    }
}