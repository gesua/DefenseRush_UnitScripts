using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Defense.Units
{
    /// <summary>
    /// 강화 탭 카드의 공통 동작. 레벨·비용 표시와 버튼 활성 상태를 갱신하고, 버튼을 누르면 TryEnhance를 호출한다.
    /// 무엇을 강화하는지(등급 강화, 소환 확률 등)는 파생 클래스가 정한다.
    /// </summary>
    public abstract class EnhanceCardBase : MonoBehaviour
    {
        [Tooltip("IBalanceSource를 구현한 컴포넌트(전투 재화). 비워두면 재화가 모자라도 버튼을 막지 않는다.")]
        [SerializeField] MonoBehaviour _balanceSource;

        [Header("UI")]
        [SerializeField] Button _button;
        [SerializeField] TMP_Text _levelLabel;
        [Tooltip("버튼 안의 비용 숫자(CostText). '강화' 글자와 코인 아이콘은 프리팹이 따로 가지고 있으므로 코드는 숫자만 바꾼다. 비워두면 버튼 글자는 건드리지 않는다.")]
        [SerializeField] TMP_Text _buttonLabel;
        [Tooltip("비용 옆의 코인 아이콘(CostCoin). 최고 레벨이거나 비용 표가 없으면 숨긴다. 비워두면 아이콘은 건드리지 않는다.")]
        [SerializeField] GameObject _costIcon;

        IBalanceSource _balance;

        /// <summary>파생 클래스가 필요로 하는 참조(진행 상태 등)가 모두 연결됐는지.</summary>
        protected abstract bool HasReferences { get; }
        protected abstract bool IsMaxLevel { get; }
        protected abstract int Level { get; }

        /// <summary>다음 레벨로 올리는 비용. 최고 레벨이거나 비용 표가 없으면 -1.</summary>
        protected abstract int NextCost { get; }

        /// <summary>한 단계 강화한다. 최고 레벨이거나 재화가 부족하면 false.</summary>
        protected abstract bool TryEnhance();

        /// <summary>레벨이 바뀔 때 Refresh가 호출되도록 이벤트를 구독/해제한다.</summary>
        protected abstract void Subscribe();
        protected abstract void Unsubscribe();

        bool IsReady => HasReferences && _button != null;

        protected virtual void Awake()
        {
            if (IsReady == false)
                Debug.LogError($"[{GetType().Name}] progress / button 참조가 비어 있습니다.", this);

            _balance = _balanceSource as IBalanceSource;
            if (_balanceSource != null && _balance == null)
                Debug.LogError($"[{GetType().Name}] balanceSource가 IBalanceSource를 구현하지 않았습니다.", this);
        }

        void OnEnable()
        {
            if (IsReady == false) return;

            _button.onClick.AddListener(OnClick);
            Subscribe();
            if (_balance != null) _balance.Changed += Refresh;

            Refresh();
        }

        void OnDisable()
        {
            if (IsReady == false) return;

            _button.onClick.RemoveListener(OnClick);
            Unsubscribe();
            if (_balance != null) _balance.Changed -= Refresh;
        }

        // 재화가 모자라면 TryEnhance가 false를 돌려주고 레벨은 그대로다.
        void OnClick() => TryEnhance();

        protected void Refresh()
        {
            bool isMax = IsMaxLevel;
            int cost = NextCost;

            if (_levelLabel != null)
                _levelLabel.text = (isMax) ? "Lv.MAX" : $"Lv.{Level}";

            if (_buttonLabel != null)
                _buttonLabel.text = GetCostText(isMax, cost);

            // 숫자 대신 MAX / - 가 보일 때는 코인 아이콘도 함께 숨긴다.
            if (_costIcon != null)
                _costIcon.SetActive(isMax == false && cost >= 0);

            // 최고 레벨이거나 재화가 모자라면 누를 수 없게 막는다.
            _button.interactable = isMax == false && CanAfford(cost);
        }

        string GetCostText(bool isMax, int cost)
        {
            if (isMax) return "MAX";

            return (cost < 0) ? "-" : cost.ToString();
        }

        // 재화 정보가 없으면 막지 않는다. 비용 표가 없으면(-1) 강화할 수 없다.
        bool CanAfford(int cost)
        {
            if (cost < 0) return false;

            return (_balance == null) || (_balance.Balance >= cost);
        }
    }
}