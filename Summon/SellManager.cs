using UnityEngine;

namespace Defense.Units
{
    /// <summary>
    /// 칸에 쌓인 유닛을 한 마리씩 팔아 전투 재화로 바꾼다. 판매가는 유닛 한 마리당 등급별로 정한다.
    /// 대금을 먼저 지급한 뒤에 유닛을 없애므로, 대금을 받지 못하면(런 종료 등) 유닛은 사라지지 않는다.
    /// </summary>
    public class SellManager : MonoBehaviour
    {
        [SerializeField] GridField _grid;

        [Tooltip("ICurrencyReceiver를 구현한 컴포넌트(전투 재화 BattleEconomy). 판매 대금을 여기로 지급한다.")]
        [SerializeField] MonoBehaviour _currencySource;

        [Tooltip("IArtifactEffects를 구현한 컴포넌트(로비 유물). 비워두면 GameManager의 유물을, 그것도 없으면 유물 효과가 없다.")]
        [SerializeField] MonoBehaviour _artifactSource;

        [Header("판매가")]
        [Tooltip("유닛 한 마리의 등급별 판매가 (순서: 일반, 희귀, 영웅, 전설). 임시 값이다.\n" +
                 "합성 비용(시작 소환가 10 기준: 희귀 30, 영웅 90, 전설 270)의 약 1/3이고, 승급해도 판매 가치가 그대로 유지되도록 3배씩 올렸다.")]
        [SerializeField] int[] _gradePrices = { 5, 10, 30, 90 };

        ICurrencyReceiver _currency;
        IArtifactEffects _artifacts;

        bool HasReferences => _grid != null && _currency != null;

        // 유물 쪽이 아직 준비되지 않았을 수 있어, 비어 있으면 읽는 시점에 GameManager의 유물을 찾아 본다.
        IArtifactEffects Artifacts => _artifacts ??= GameModelArtifactEffects.TryCreate();

        void Awake()
        {
            if (_grid == null)
                Debug.LogError("[SellManager] grid 참조가 비어 있습니다.", this);

            _currency = _currencySource as ICurrencyReceiver;
            if (_currency == null)
                Debug.LogError("[SellManager] currencySource가 비어 있거나 ICurrencyReceiver를 구현하지 않았습니다.", this);

            _artifacts = _artifactSource as IArtifactEffects;
            if (_artifactSource != null && _artifacts == null)
                Debug.LogError("[SellManager] artifactSource가 IArtifactEffects를 구현하지 않았습니다.", this);
        }

        // 등급이 늘거나 줄어도 표 길이가 맞도록 보정하고, 음수 판매가를 막는다.
        void OnValidate()
        {
            if (_artifactSource != null && (_artifactSource is IArtifactEffects) == false)
                Debug.LogError("[SellManager] artifactSource가 IArtifactEffects를 구현하지 않았습니다.", this);

            if (_gradePrices == null || _gradePrices.Length != UnitGrades.Count)
                System.Array.Resize(ref _gradePrices, UnitGrades.Count);

            for (int i = 0; i < _gradePrices.Length; i++)
                _gradePrices[i] = Mathf.Max(0, _gradePrices[i]);
        }

        /// <summary>해당 등급 유닛 한 마리의 판매가. 표에 없는 등급이면 0. 유물의 판매가 증가가 반영된다.</summary>
        public int GetPrice(UnitGrade grade)
        {
            int index = (int)grade;
            int price = (_gradePrices != null && index >= 0 && index < _gradePrices.Length) ? _gradePrices[index] : 0;

            IArtifactEffects artifacts = Artifacts;
            float bonus = (artifacts != null) ? Mathf.Max(0f, artifacts.GetBonus(ArtifactEffect.SellGold)) : 0f;
            return ArtifactMath.ApplyIncrease(price, bonus);
        }

        /// <summary>칸에서 지금 팔게 되는 유닛 한 마리의 판매가. 빈 칸이면 0.</summary>
        public int GetSellPrice(Slot slot) => (slot != null && slot.IsEmpty == false) ? GetPrice(slot.Type.Grade) : 0;

        /// <summary>칸에 팔 유닛이 있으면 true.</summary>
        public bool CanSell(Slot slot) => HasReferences && slot != null && slot.IsEmpty == false;

        /// <summary>칸에 가장 마지막으로 쌓인 유닛 한 마리를 팔아 대금을 받는다. 팔았으면 true.</summary>
        public bool Sell(Slot slot)
        {
            if (CanSell(slot) == false) return false;

            // 1) 대금을 먼저 받는다. 받지 못하면 유닛을 없애지 않는다. (0원이면 지급할 것이 없다)
            int price = GetSellPrice(slot);
            if (price > 0 && _currency.AddCurrency(price) == false) return false;

            // 2) 한 마리 제거. 남은 유닛은 GridField가 다시 배치한다.
            // 마지막 한 마리를 팔면 칸이 비어 Type이 null이 되므로, 제거 전에 종류를 기억해 둔다.
            UnitData soldType = slot.Type;
            if (_grid.RemoveLastUnit(slot) == false) return false;

            // 3) 같은 종류가 자리를 남긴 채 여러 칸에 나뉘어 있으면 걸어가서 합친다. (3 + 2에서 한 마리를 팔면 2 + 2가 아니라 3 + 1)
            _grid.ConsolidateStacks(soldType, slot);
            return true;
        }
    }
}