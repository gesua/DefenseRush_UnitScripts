using TMPro;
using UnityEngine;

namespace Defense.Units
{
    /// <summary>
    /// 잠깐 보였다가 저절로 사라지는 안내 글자의 공통 부분.
    /// 상속한 클래스는 어떤 이벤트에 어떤 문구를 띄울지만 정한다.
    /// 이 컴포넌트가 프레임(_frame)을 껐다 켜므로, 프레임은 이 컴포넌트가 붙은 오브젝트나 그 부모가 아니어야 하고 처음엔 꺼져 있어도 된다.
    /// 글자(_label)는 프레임 안에 있어야 프레임과 함께 보이고 사라진다.
    /// </summary>
    public abstract class ToastView : MonoBehaviour
    {
        [Tooltip("반투명 배경 프레임. 안내가 보일 때 이 오브젝트를 켜고, 사라질 때 끈다. 글자(_label)가 이 안에 있어야 한다.")]
        [SerializeField] GameObject _frame;
        [SerializeField] TMP_Text _label;

        [Header("안내 표시")]
        [Tooltip("안내가 보이는 시간(초). 배속과 상관없이 실제 시간으로 센다.")]
        [SerializeField, Min(0.2f)] float _showSeconds = 1.5f;

        [Tooltip("재화 부족 / 빈 칸 없음 같은 경고 안내의 글자색")]
        [SerializeField] Color _warningColor = new Color(1f, 0.45f, 0.4f);

        float _hideAt;

        /// <summary>프레임과 글자가 연결돼 있고, 글자가 프레임 안에 있으며, 프레임이 이 컴포넌트가 붙은 오브젝트(또는 그 부모)가 아닌지.</summary>
        protected bool HasView =>
            _frame != null && _label != null
            && transform.IsChildOf(_frame.transform) == false
            && _label.transform.IsChildOf(_frame.transform);

        protected virtual void OnEnable() => Hide();

        protected virtual void OnDisable() => Hide();

        void Update()
        {
            // 닫는 시간이 정해진 동안에만 확인한다.
            if (_hideAt <= 0f || Time.unscaledTime < _hideAt) return;

            Hide();
        }

        protected void Show(string text, Color color)
        {
            _label.text = text;
            _label.color = color;
            _frame.SetActive(true);

            _hideAt = Time.unscaledTime + _showSeconds;
        }

        protected void ShowWarning(string text) => Show(text, _warningColor);

        // 도박과 일반 소환이 같은 문구를 쓰도록 여기서 한 번만 정한다.
        protected void ShowNoSpace() => ShowWarning("빈 칸이 없습니다");
        protected void ShowUnitLimit() => ShowWarning("유닛이 가득 찼습니다");

        void Hide()
        {
            _hideAt = 0f;
            if (HasView) _frame.SetActive(false);
        }
    }
}