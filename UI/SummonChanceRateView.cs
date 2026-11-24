using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Defense.Units
{
    /// <summary>
    /// 소환 확률 카드의 등급 아이콘(Button)을 누르면 현재 등급별 소환 확률을 팝업으로 보여준다.
    /// 아이콘을 다시 누르거나 팝업 바깥 아무 곳이나 누르거나 일정 시간이 지나면 닫히고, 카드가 꺼지면(강화 탭을 닫으면) 같이 닫힌다.
    /// 팝업 바깥을 누르면 그 탭은 닫는 데만 쓰이고 뒤의 버튼에는 전달되지 않는다. (팝업 바로 뒤에 놓은 투명한 전체 화면 막이(_blocker)가 받는다)
    /// Canvas 하이어라키 맨 아래쪽에 [... 다른 UI, 막이, 팝업] 순서로 미리 배치해 둔다.
    /// 아이콘 오브젝트에 붙이고, 팝업(_panel)은 카드 밖(Canvas 바로 아래)에 두어 다른 카드에 가려지지 않게 한다.
    /// Canvas는 Screen Space - Overlay여야 한다.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class SummonChanceRateView : MonoBehaviour
    {
        [SerializeField] SummonChanceProgress _progress;
        [Tooltip("확률을 보여줄 팝업. 처음엔 꺼져 있어도 된다. 피벗은 아래 중앙을 권장.")]
        [SerializeField] RectTransform _panel;
        [Tooltip("팝업 바깥 탭을 받는 투명한 전체 화면 버튼. 팝업과 같은 부모에서 팝업 바로 위쪽 형제(팝업보다 먼저)에 둔다. 처음엔 꺼져 있어도 된다.")]
        [SerializeField] Button _blocker;
        [SerializeField] TMP_Text _rateLabel;

        [Header("표시")]
        [Tooltip("첫 줄 제목. 비워두면 표시하지 않는다.")]
        [SerializeField] string _title = "현재 소환 확률";
        [Tooltip("등급 순서(일반, 희귀, 영웅)대로의 이름")]
        [SerializeField] string[] _gradeNames = { "일반", "희귀", "영웅", "전설" };
        [Tooltip("팝업이 저절로 닫히는 시간(초). 0이면 아이콘을 다시 누를 때까지 유지한다.")]
        [SerializeField, Min(0f)] float _showSeconds = 3f;

        [Header("위치")]
        [Tooltip("아이콘 위쪽으로 띄울 여백(픽셀)")]
        [SerializeField] float _gap = 12f;
        [Tooltip("팝업이 화면 가장자리에서 떨어질 최소 거리(픽셀)")]
        [SerializeField] float _screenMargin = 20f;

        readonly Vector3[] _corners = new Vector3[4];
        readonly StringBuilder _text = new StringBuilder();

        Button _button;
        RectTransform _iconRect;
        float _hideAt;

        bool HasReferences => _progress != null && _panel != null && _blocker != null && _rateLabel != null;

        void Awake()
        {
            TryGetComponent(out _button);
            TryGetComponent(out _iconRect);

            if (HasReferences == false)
                Debug.LogError("[SummonChanceRateView] progress / panel / blocker / rateLabel 참조가 비어 있습니다.", this);
        }

        void OnEnable()
        {
            if (HasReferences == false) return;

            _button.onClick.AddListener(OnIconClicked);
            _blocker.onClick.AddListener(Hide);
            _progress.WeightsChanged += OnWeightsChanged;
            Hide();
        }

        void OnDisable()
        {
            if (HasReferences == false) return;

            _button.onClick.RemoveListener(OnIconClicked);
            _blocker.onClick.RemoveListener(Hide);
            _progress.WeightsChanged -= OnWeightsChanged;
            Hide();
        }

        void Update()
        {
            // 닫는 시간이 정해져 있을 때만 확인한다. (0이면 타이머 없음)
            if (_hideAt <= 0f || Time.unscaledTime < _hideAt) return;

            Hide();
        }

        void OnIconClicked()
        {
            if (_panel.gameObject.activeSelf) Hide();
            else Show();
        }

        // 열려 있는 동안 소환 확률이 바뀌면(레벨, 축복) 숫자도 바로 갱신한다.
        void OnWeightsChanged()
        {
            if (_panel.gameObject.activeSelf) Refresh();
        }

        void Show()
        {
            Refresh();
            Place();

            _blocker.gameObject.SetActive(true);
            _panel.gameObject.SetActive(true);
            _hideAt = (_showSeconds > 0f) ? Time.unscaledTime + _showSeconds : 0f;
        }

        void Hide()
        {
            _hideAt = 0f;
            if (_blocker != null) _blocker.gameObject.SetActive(false);
            if (_panel != null) _panel.gameObject.SetActive(false);
        }

        // 가중치를 합으로 나눠 퍼센트로 보여준다. (합이 100이면 가중치가 곧 퍼센트)
        void Refresh()
        {
            IReadOnlyList<float> weights = _progress.Weights;
            if (weights == null) return;

            float total = 0f;
            for (int i = 0; i < weights.Count; i++) total += Mathf.Max(0f, weights[i]);
            if (total <= 0f) return;

            _text.Clear();
            if (string.IsNullOrEmpty(_title) == false) _text.Append(_title).Append('\n');

            for (int i = 0; i < weights.Count; i++)
            {
                float percent = Mathf.Max(0f, weights[i]) / total * 100f;

                // <pos=45%>: 이름과 숫자의 시작 위치를 줄마다 맞춘다
                _text.Append(GetGradeName(i)).Append("<pos=45%>")
                     .Append(percent.ToString("0.00", CultureInfo.InvariantCulture)).Append('%');
                if (i < weights.Count - 1) _text.Append('\n');
            }

            _rateLabel.text = _text.ToString();
        }

        string GetGradeName(int index) =>
            (_gradeNames != null && index < _gradeNames.Length) ? _gradeNames[index] : ((UnitGrade)index).ToString();

        // 아이콘 위쪽 가운데에 팝업을 놓는다. (Overlay 캔버스는 화면 좌표가 곧 position)
        void Place()
        {
            _iconRect.GetWorldCorners(_corners);
            Vector3 topCenter = (_corners[1] + _corners[2]) * 0.5f;

            _panel.position = ClampToScreen(topCenter + Vector3.up * (_gap * _iconRect.lossyScale.y));
        }

        // 피벗 기준으로 팝업 전체가 화면 안에 들어오게 보정한다.
        Vector3 ClampToScreen(Vector3 screen)
        {
            Vector2 size = Vector2.Scale(_panel.rect.size, _panel.lossyScale);
            Vector2 pivot = _panel.pivot;

            float minX = _screenMargin + size.x * pivot.x;
            float maxX = Screen.width - _screenMargin - size.x * (1f - pivot.x);
            float minY = _screenMargin + size.y * pivot.y;
            float maxY = Screen.height - _screenMargin - size.y * (1f - pivot.y);

            screen.x = Mathf.Clamp(screen.x, minX, Mathf.Max(minX, maxX));
            screen.y = Mathf.Clamp(screen.y, minY, Mathf.Max(minY, maxY));
            return screen;
        }
    }
}