using UnityEngine;

namespace Defense.Units
{
    /// <summary>
    /// 칸을 탭하면 그 칸 유닛의 공격범위를 바닥에 원으로 보여준다.
    /// UnitField(GridField + GridDragInput이 있는 오브젝트)에 붙인다.
    /// 같은 칸을 다시 탭하거나 빈 칸을 탭하면 사라진다.
    /// 드래그하는 동안에는 집은 칸의 공격범위를 보여준다.
    /// </summary>
    [RequireComponent(typeof(GridField), typeof(GridDragInput))]
    public class RangeIndicator : MonoBehaviour
    {
        [SerializeField] Color _color = new Color(1f, 1f, 1f, 0.7f);
        [Tooltip("바닥과 겹쳐 깜빡이지 않도록 그리드 위로 띄우는 높이")]
        [SerializeField] float _height = 0.05f;
        [Tooltip("비워두면 기본 스프라이트 머티리얼을 쓴다. 원이 안 보이면 Sprite-Unlit-Default를 넣어 볼 것.")]
        [SerializeField] Material _material;

        GridField _grid;
        GridDragInput _input;
        GameObject _circleObject;
        SpriteRenderer _circle;
        Slot _selected;

        void Awake()
        {
            TryGetComponent(out _grid);
            TryGetComponent(out _input);

            _circleObject = new GameObject("RangeIndicator");
            _circleObject.transform.SetParent(transform, false);

            _circle = _circleObject.AddComponent<SpriteRenderer>();
            _circle.sprite = GuideSprites.CreateCircle();
            _circle.color = _color;
            if (_material != null) _circle.sharedMaterial = _material;

            _circleObject.SetActive(false);
        }

        void OnEnable()
        {
            _input.SlotTapped += OnSlotTapped;
            _input.DragStarted += OnDragStarted;
            _input.DragEnded += OnDragEnded;
            _input.BackgroundTapped += Hide; // 빈 배경을 탭하면 닫는다
            _grid.SlotChanged += OnSlotChanged;
        }

        void OnDisable()
        {
            _input.SlotTapped -= OnSlotTapped;
            _input.DragStarted -= OnDragStarted;
            _input.DragEnded -= OnDragEnded;
            _input.BackgroundTapped -= Hide;
            _grid.SlotChanged -= OnSlotChanged;
        }

        void OnDestroy()
        {
            if (_circle != null) GuideSprites.DestroySprite(_circle.sprite);
        }

        void OnSlotTapped(Slot slot)
        {
            // 같은 칸을 다시 탭하거나 빈 칸이면 닫는다.
            if (slot == _selected || slot.IsEmpty)
            {
                Hide();
                return;
            }

            Select(slot);
        }

        // 드래그를 시작하면 집은 칸의 공격범위를 보여주고, 놓으면 닫는다.
        void OnDragStarted(Slot source) => Select(source);

        void OnDragEnded() => Hide();

        // 칸 위에 원을 놓고 그 칸 유닛의 공격범위에 맞춘다.
        void Select(Slot slot)
        {
            _selected = slot;
            _circleObject.transform.SetPositionAndRotation(
                slot.Center + transform.up * _height,
                transform.rotation * Quaternion.Euler(90f, 0f, 0f)); // 스프라이트를 바닥에 눕힌다
            _circleObject.SetActive(true);
            ApplyRange();
        }

        // 선택한 칸이 이동/교환/업그레이드로 바뀌면 갱신하거나 닫는다.
        void OnSlotChanged(Slot slot)
        {
            if (slot != _selected) return;

            if (slot.IsEmpty) Hide();
            else ApplyRange();
        }

        // 인게임 강화는 즉시 반영되므로 표시 중에는 매 프레임 사거리를 다시 읽는다.
        void Update()
        {
            if (_selected == null) return;

            if (_selected.IsEmpty) Hide();
            else ApplyRange();
        }

        void ApplyRange()
        {
            float range = _selected.Units[0].Stats.AttackRange;
            float diameter = Mathf.Max(0f, range) * 2f; // 스프라이트가 1유닛 크기라 지름이 곧 스케일
            _circleObject.transform.localScale = new Vector3(diameter, diameter, 1f);
        }

        void Hide()
        {
            _selected = null;
            _circleObject.SetActive(false);
        }
    }
}