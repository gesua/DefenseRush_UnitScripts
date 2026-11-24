using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Defense.Units
{
    /// <summary>
    /// 도박 패널의 카드 한 장(희귀/영웅/전설 중 하나). 기본 확률, 유물 보정 확률, 비용을 보여주고
    /// 버튼을 누르면 해당 등급으로 도박한다. 결과 안내는 GambleResultView가 맡는다.
    /// </summary>
    public class GambleCard : MonoBehaviour
    {
        [SerializeField] GambleManager _manager;

        [Tooltip("이 카드가 도박하는 등급")]
        [SerializeField] UnitGrade _grade = UnitGrade.Rare;

        [Tooltip("IBalanceSource를 구현한 컴포넌트(전투 재화 BattleEconomy). 비워두면 재화가 모자라도 버튼을 막지 않는다.")]
        [SerializeField] MonoBehaviour _balanceSource;

        [Header("UI")]
        [SerializeField] Button _button;

        [Tooltip("확률 글자. 유물 보정이 있으면 '20 +5.5%', 없으면 '20%'로 표시한다.")]
        [SerializeField] TMP_Text _chanceLabel;

        [Tooltip("비용 글자. 보통 버튼 안의 글자. 비워두면 표시하지 않는다.")]
        [SerializeField] TMP_Text _costLabel;

        [Header("표시")]
        [Tooltip("유물 보정 확률(+5.5% 등)의 글자색")]
        [SerializeField] Color _bonusColor = new Color(0.4f, 0.85f, 0.25f);

        IBalanceSource _balance;

        bool HasReferences => _manager != null && _button != null;

        void Awake()
        {
            if (HasReferences == false)
                Debug.LogError("[GambleCard] manager / button 참조가 비어 있습니다.", this);

            _balance = _balanceSource as IBalanceSource;
            if (_balanceSource != null && _balance == null)
                Debug.LogError("[GambleCard] balanceSource가 IBalanceSource를 구현하지 않았습니다.", this);
        }

        void OnEnable()
        {
            if (HasReferences == false) return;

            _button.onClick.AddListener(OnClick);
            _manager.Gambled += OnGambled;
            if (_balance != null) _balance.Changed += Refresh;

            Refresh();
        }

        void OnDisable()
        {
            if (HasReferences == false) return;

            _button.onClick.RemoveListener(OnClick);
            _manager.Gambled -= OnGambled;
            if (_balance != null) _balance.Changed -= Refresh;
        }

        // 결과(성공/실패/막힘)는 매니저가 이벤트로 알리고, 재화가 바뀌면 Refresh가 다시 불린다.
        void OnClick() => _manager.TryGamble(_grade);

        // 성공하면 비용이 오르는데, 결제 시점의 재화 변경 알림에는 아직 반영되지 않으므로 결과가 나온 뒤에 다시 그린다.
        void OnGambled(GambleResult result) => Refresh();

        void Refresh()
        {
            int cost = _manager.GetCost(_grade);

            if (_chanceLabel != null)
                _chanceLabel.text = FormatChance(_manager.GetBaseChance(_grade), _manager.GetBonusChance(_grade));

            if (_costLabel != null)
                _costLabel.text = (cost < 0) ? "-" : cost.ToString();

            _button.interactable = CanAfford(cost);
        }

        // 재화 정보가 없으면 막지 않는다. 설정에 없는 등급(-1)은 도박할 수 없다.
        bool CanAfford(int cost)
        {
            if (cost < 0) return false;

            return (_balance == null) || (_balance.Balance >= cost);
        }

        // 기본 확률에 유물 보정이 있으면 보정만 초록색으로 덧붙인다. 예) "20 +5.5%"
        string FormatChance(float baseChance, float bonusChance)
        {
            string baseText = baseChance.ToString("0.##", CultureInfo.InvariantCulture);
            if (bonusChance <= 0f) return $"{baseText}%";

            string bonusText = bonusChance.ToString("0.##", CultureInfo.InvariantCulture);
            return $"{baseText} <color=#{ColorUtility.ToHtmlStringRGB(_bonusColor)}>+{bonusText}%</color>";
        }
    }
}