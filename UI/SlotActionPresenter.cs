using UnityEngine;
using UnityEngine.UI;

namespace Defense.Units
{
    /// <summary>
    /// 칸을 탭했을 때 그 칸 위에 띄우는 버튼(승급, 판매 등)의 공통 부분.
    /// 탭한 칸을 선택하고, 그 칸에서 쓸 수 있을 때(IsAvailable)만 버튼을 보여주며, 누르면 Execute를 실행한다.
    /// 어떤 칸에서 쓸 수 있는지, 눌렀을 때 무엇을 하는지만 상속한 클래스가 정한다.
    ///
    /// 버튼을 여러 개 띄울 때는 각 버튼의 Screen Offset으로 나란히 벌린다.
    /// 화면 가장자리에서는 오프셋을 포함한 범위로 기준점을 보정해서, 버튼끼리 겹치지 않는다.
    /// </summary>
    public abstract class SlotActionPresenter : MonoBehaviour
    {
        [SerializeField] GridDragInput _input;
        [SerializeField] GridField _grid;
        [Tooltip("칸을 탭했을 때, 이 버튼을 쓸 수 있는 칸일 때만 켜진다.")]
        [SerializeField] Button _button;

        [Header("위치")]
        [Tooltip("비워두면 Camera.main을 쓴다.")]
        [SerializeField] Camera _camera;
        [Tooltip("칸 중심에서 버튼을 띄울 월드 오프셋. (기본: 유닛 머리 위쪽)")]
        [SerializeField] Vector3 _worldOffset = new Vector3(0f, 2f, 0f);
        [Tooltip("화면에 투영한 위치에서 추가로 옮길 거리(캔버스 단위). 버튼을 여러 개 띄울 때 좌우로 벌리는 데 쓴다.")]
        [SerializeField] Vector2 _screenOffset;
        [Tooltip("버튼이 화면 가장자리에서 떨어질 최소 거리(픽셀)")]
        [SerializeField] float _screenMargin = 20f;

        Slot _selected;
        RectTransform _buttonRect;

        /// <summary>상속한 클래스가 따로 필요로 하는 참조(매니저 등)가 모두 연결돼 있는지.</summary>
        protected abstract bool HasExtraReferences { get; }

        /// <summary>선택한 칸에서 이 버튼을 쓸 수 있는지. (null 칸이면 false여야 한다)</summary>
        protected abstract bool IsAvailable(Slot slot);

        /// <summary>버튼을 눌렀을 때 선택한 칸에 대해 할 일.</summary>
        protected abstract void Execute(Slot slot);

        /// <summary>버튼을 보여줄 때마다 호출된다. 칸에 따라 달라지는 글자 등을 갱신하는 데 쓴다.</summary>
        protected virtual void OnShown(Slot slot) { }

        bool HasReferences => _input != null && _grid != null && _button != null && HasExtraReferences;

        protected virtual void Awake()
        {
            if (HasReferences == false)
            {
                Debug.LogError($"[{GetType().Name}] input / grid / button 또는 이 버튼의 매니저 참조가 비어 있습니다.", this);
                return;
            }

            if (_button.TryGetComponent(out _buttonRect) == false)
                Debug.LogError($"[{GetType().Name}] 버튼에 RectTransform이 없습니다.", this);
        }

        protected virtual void OnEnable()
        {
            if (HasReferences == false) return;

            _input.SlotTapped += OnSlotTapped;
            _input.DragStarted += OnDragStarted;
            _input.BackgroundTapped += OnBackgroundTapped;
            _grid.SlotChanged += OnSlotChanged;
            _button.onClick.AddListener(OnClick);

            _selected = null;
            Refresh();
        }

        protected virtual void OnDisable()
        {
            if (HasReferences == false) return;

            _input.SlotTapped -= OnSlotTapped;
            _input.DragStarted -= OnDragStarted;
            _input.BackgroundTapped -= OnBackgroundTapped;
            _grid.SlotChanged -= OnSlotChanged;
            _button.onClick.RemoveListener(OnClick);
        }

        // 선택 규칙은 RangeIndicator와 같다. 같은 칸을 다시 탭하거나 빈 칸을 탭하면 선택을 푼다.
        // (공격범위가 떠 있는 칸에서만 승급 버튼이 뜨도록 둘을 맞춘다)
        void OnSlotTapped(Slot slot)
        {
            _selected = (slot == _selected || slot.IsEmpty) ? null : slot;
            Refresh();
        }

        // 빈 배경을 탭하면 선택을 풀어서 버튼을 닫는다.
        void OnBackgroundTapped()
        {
            _selected = null;
            Refresh();
        }

        // 다른 유닛을 집어 드는 순간 선택을 풀어서, 옮기는 도중에 버튼이 남아 있지 않게 한다.
        void OnDragStarted(Slot slot)
        {
            _selected = null;
            Refresh();
        }

        // 선택한 칸의 내용이 바뀌면(소환, 이동, 승급) 버튼 표시를 다시 판단한다.
        // 칸이 비면 선택도 풀어서, 나중에 그 칸이 다시 3마리가 돼도 버튼이 저절로 뜨지 않게 한다.
        void OnSlotChanged(Slot slot)
        {
            if (slot != _selected) return;

            if (slot.IsEmpty) _selected = null;
            Refresh();
        }

        void OnClick()
        {
            Execute(_selected);
            Refresh();
        }

        // 쓸 수 있는 칸일 때만 버튼을 켜고, 켜질 때마다 칸 위로 옮긴다.
        void Refresh()
        {
            bool available = IsAvailable(_selected);
            _button.gameObject.SetActive(available);

            if (available == false) return;

            OnShown(_selected);
            PlaceButton(_selected);
        }

        // 칸 위치를 화면 좌표로 바꿔 버튼을 옮긴다. (Overlay 캔버스는 화면 좌표가 곧 버튼 position)
        void PlaceButton(Slot slot)
        {
            Camera cam = (_camera != null) ? _camera : Camera.main;
            if (cam == null || _buttonRect == null) return;

            Vector3 screen = cam.WorldToScreenPoint(slot.Center + _worldOffset);
            _buttonRect.position = ClampToScreen(screen);
        }

        // 피벗 기준으로 버튼 전체가 화면 안에 들어오게 보정한다. (맨 윗줄 칸 대비)
        // 오프셋은 보정한 기준점에 더한다. 보정 범위는 오프셋 크기만큼 안쪽으로 잡으므로, 좌우로 벌린 버튼들도 같은 기준점을 쓴다.
        Vector3 ClampToScreen(Vector3 screen)
        {
            Vector2 size = Vector2.Scale(_buttonRect.rect.size, _buttonRect.lossyScale);
            Vector2 offset = Vector2.Scale(_screenOffset, _buttonRect.lossyScale); // 캔버스 단위 -> 화면 픽셀
            Vector2 pivot = _buttonRect.pivot;

            float minX = _screenMargin + size.x * pivot.x + Mathf.Abs(offset.x);
            float maxX = Screen.width - _screenMargin - size.x * (1f - pivot.x) - Mathf.Abs(offset.x);
            float minY = _screenMargin + size.y * pivot.y + Mathf.Abs(offset.y);
            float maxY = Screen.height - _screenMargin - size.y * (1f - pivot.y) - Mathf.Abs(offset.y);

            screen.x = Mathf.Clamp(screen.x, minX, Mathf.Max(minX, maxX)) + offset.x;
            screen.y = Mathf.Clamp(screen.y, minY, Mathf.Max(minY, maxY)) + offset.y;
            return screen;
        }
    }
}
