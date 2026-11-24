using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Defense.Units
{
    /// <summary>
    /// 유닛 프리뷰 프레임(PreviewFrame)의 터치 상호작용. 한 번 터치하면 프레임이 커지고, 다시 터치하면 원래 크기로 돌아온다.
    /// 드래그하면 프리뷰 카메라가 유닛 둘레를 돈다. (실제 회전은 UnitPreviewController.Orbit이 한다)
    /// 확대할 때는 기준점(기본은 왼쪽 아래 모서리)을 고정해 오른쪽 위로 커지고, 화면(캔버스) 밖으로 나가지 않도록 위치도 함께 옮기며, 프리뷰가 닫히면 바로 원래 크기로 되돌린다.
    /// 터치를 받으려면 Graphic(Image)의 Raycast Target이 켜져 있어야 하므로, PreviewFrame처럼 Image가 있는 프레임 오브젝트에 붙인다.
    /// 안쪽의 RawImage(Preview)는 입력을 막지 않아서, 터치는 이 프레임이 받는다.
    /// 확대는 localScale로 하므로 안쪽의 Preview도 비율 그대로 함께 커진다.
    /// 프레임은 유닛 정보창과 형제로 놓이므로, 정보창과 상관없이 프리뷰가 열리고 닫힐 때(Shown, Closed) 이 프레임의 CanvasGroup으로 직접 보이고 숨긴다.
    /// 숨겨진 동안에는 알파 0, 터치도 막지 않아서 뒤의 입력을 가리지 않는다.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    [DisallowMultipleComponent]
    public class UnitPreviewInteraction : MonoBehaviour, IPointerClickHandler, IDragHandler
    {
        [Header("연결")]
        [Tooltip("카메라를 돌릴 유닛 프리뷰. 비워두면 자식에서 찾는다.")]
        [SerializeField] UnitPreviewController _preview;
        [Tooltip("확대하고 줄일 프레임. 비워두면 이 오브젝트를 쓴다.")]
        [SerializeField] RectTransform _frame;
        [Tooltip("확대한 프레임이 벗어나지 않게 할 화면 영역. 비워두면 최상위 Canvas를 쓴다.")]
        [SerializeField] RectTransform _bounds;

        [Header("확대")]
        [Tooltip("확대했을 때의 배율. 렌더 텍스처 해상도보다 크게 키우면 흐려진다.")]
        [SerializeField, Min(1f)] float _enlargedScale = 3f;
        [Tooltip("확대하고 줄이는 데 걸리는 시간(초)")]
        [SerializeField, Min(0f)] float _duration = 0.2f;
        [Tooltip("확대해도 제자리에 두는 기준점. 프레임 안의 비율 좌표이다. (0, 0)은 왼쪽 아래 모서리를 고정해 오른쪽 위로 커지고, (1, 0)은 오른쪽 아래를 고정해 왼쪽 위로 커진다.")]
        [SerializeField] Vector2 _enlargeAnchor = Vector2.zero;
        [Tooltip("확대한 프레임과 화면 가장자리 사이에 둘 최소 여백(캔버스 단위)")]
        [SerializeField, Min(0f)] float _screenMargin = 20f;

        // 경계 계산용 임시 버퍼
        readonly Vector3[] _corners = new Vector3[4];

        CanvasGroup _group;
        bool _isEnlarged;
        bool _hasNormalPose;
        float _progress; // 0이면 원래 크기, 1이면 완전히 확대
        Vector3 _normalScale;
        Vector3 _normalPosition;
        Vector3 _enlargedPosition;

        void Awake()
        {
            TryGetComponent(out _group);
            SetVisible(false);

            if (_frame == null) TryGetComponent(out _frame);
            if (_preview == null) _preview = GetComponentInChildren<UnitPreviewController>(true);

            if (_frame == null)
            {
                Debug.LogError("[UnitPreviewInteraction] 확대할 RectTransform을 찾지 못했습니다.", this);
                enabled = false;
                return;
            }

            if (_preview == null) Debug.LogWarning("[UnitPreviewInteraction] UnitPreviewController를 찾지 못해 드래그 회전은 동작하지 않습니다.", this);

            if (TryGetComponent(out Graphic graphic) == false)
            {
                Debug.LogWarning("[UnitPreviewInteraction] 터치를 받을 Graphic(Image)이 없어 입력이 동작하지 않습니다.", this);
                return;
            }

            graphic.raycastTarget = true;
        }

        void OnEnable()
        {
            if (_preview == null) return;

            _preview.Shown += OnPreviewShown;
            _preview.Closed += OnPreviewClosed;
        }

        void OnDisable()
        {
            if (_preview != null)
            {
                _preview.Shown -= OnPreviewShown;
                _preview.Closed -= OnPreviewClosed;
            }

            SetEnlarged(false, true);
        }

        void Update()
        {
            float target = (_isEnlarged) ? 1f : 0f;
            if (Mathf.Approximately(_progress, target)) return;

            // 게임이 멈춰 있어도(timeScale 0) 움직이도록 보정되지 않은 시간을 쓴다.
            float step = (_duration > 0f) ? Time.unscaledDeltaTime / _duration : 1f;
            _progress = Mathf.MoveTowards(_progress, target, step);
            ApplyPose();
        }

        // 드래그로 끝난 터치는 클릭으로 치지 않는다. (EventSystem의 드래그 판정 거리 안에서 뗀 터치만 클릭이 된다)
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (eventData.dragging) return;

            SetEnlarged(_isEnlarged == false, false);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (_preview == null) return;

            _preview.Orbit(eventData.delta);
        }

        void OnPreviewShown()
        {
            SetVisible(true);
        }

        // 프리뷰가 닫히면 확대도 풀어서, 다음에 열 때 항상 원래 크기로 시작한다.
        void OnPreviewClosed()
        {
            SetEnlarged(false, true);
            SetVisible(false);
        }

        /// <summary>
        /// 프리뷰가 보이는 상태에서 화면 좌표가 프레임 안인지 알려준다. (확대된 크기도 반영된다)
        /// 숨겨진 동안에는 항상 false라서, 보이지 않는 프레임 자리는 입력을 통과시키지 않는다.
        /// </summary>
        public bool ContainsScreenPoint(Vector2 screenPoint, Camera eventCamera)
        {
            if (_frame == null || _group == null || _group.blocksRaycasts == false) return false;

            return RectTransformUtility.RectangleContainsScreenPoint(_frame, screenPoint, eventCamera);
        }

        // 숨겨진 동안에는 알파 0, 터치도 막지 않아서 뒤의 입력을 가리지 않는다.
        void SetVisible(bool visible)
        {
            if (_group == null) return;

            _group.alpha = (visible) ? 1f : 0f;
            _group.interactable = visible;
            _group.blocksRaycasts = visible;
        }

        void SetEnlarged(bool enlarged, bool immediate)
        {
            if (_frame == null) return;

            if (enlarged && _isEnlarged == false)
            {
                // 원래 크기에서 시작할 때만 현재 자세를 원래 자세로 기억한다. (줄어드는 도중에 다시 키우면 기억해 둔 값을 그대로 쓴다)
                if (_progress <= 0f) CaptureNormalPose();
                _enlargedPosition = CalculateEnlargedPosition();
            }

            _isEnlarged = enlarged;
            if (immediate == false) return;

            _progress = (enlarged) ? 1f : 0f;
            ApplyPose();
        }

        void CaptureNormalPose()
        {
            _normalScale = _frame.localScale;
            _normalPosition = _frame.localPosition;
            _hasNormalPose = true;
        }

        void ApplyPose()
        {
            if (_hasNormalPose == false) return;

            float eased = Mathf.SmoothStep(0f, 1f, _progress);
            _frame.localScale = Vector3.Lerp(_normalScale, _normalScale * _enlargedScale, eased);
            _frame.localPosition = Vector3.Lerp(_normalPosition, _enlargedPosition, eased);
        }

        // 확대한 프레임의 위치를 구한다. (부모 로컬 좌표)
        // _enlargeAnchor 지점을 고정한 채 커지도록 확대 후의 사각형을 먼저 구하고, 화면 영역을 벗어난 만큼만 옮긴다.
        Vector3 CalculateEnlargedPosition()
        {
            Vector3 position = _normalPosition;

            RectTransform bounds = ResolveBounds();
            RectTransform parent = _frame.parent as RectTransform;
            if (bounds == null || parent == null) return position;

            // 화면 영역을 부모 좌표로 옮기고 여백만큼 줄인다.
            bounds.GetWorldCorners(_corners);
            Vector2 boundsMin = (Vector2)parent.InverseTransformPoint(_corners[0]) + Vector2.one * _screenMargin;
            Vector2 boundsMax = (Vector2)parent.InverseTransformPoint(_corners[2]) - Vector2.one * _screenMargin;

            // 원래 사각형에서 기준점의 위치를 구하고, 확대한 사각형도 그 점이 제자리에 오도록 놓는다.
            Vector2 normalSize = Vector2.Scale(_frame.rect.size, (Vector2)_normalScale);
            Vector2 size = normalSize * _enlargedScale;
            Vector2 normalMin = (Vector2)_normalPosition - Vector2.Scale(_frame.pivot, normalSize);
            Vector2 fixedPoint = normalMin + Vector2.Scale(_enlargeAnchor, normalSize);
            Vector2 min = fixedPoint - Vector2.Scale(_enlargeAnchor, size);
            Vector2 max = min + size;

            // 피벗 위치(localPosition)를 확대한 사각형의 위치에 맞춘다.
            position = (Vector3)(min + Vector2.Scale(_frame.pivot, size)) + new Vector3(0f, 0f, _normalPosition.z);

            position.x += CalculateShift(min.x, max.x, boundsMin.x, boundsMax.x);
            position.y += CalculateShift(min.y, max.y, boundsMin.y, boundsMax.y);
            return position;
        }

        // 구간 [min, max]를 [boundsMin, boundsMax] 안으로 넣기 위해 옮겨야 하는 거리. 이미 안에 있으면 0이다.
        static float CalculateShift(float min, float max, float boundsMin, float boundsMax)
        {
            if (min < boundsMin) return boundsMin - min;
            if (max > boundsMax) return boundsMax - max;

            return 0f;
        }

        RectTransform ResolveBounds()
        {
            if (_bounds != null) return _bounds;

            // Canvas는 부모가 정해진 뒤에야 찾을 수 있으므로, 처음 필요할 때 찾는다.
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null) canvas.rootCanvas.TryGetComponent(out _bounds);

            return _bounds;
        }
    }
}