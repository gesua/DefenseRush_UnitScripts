using TMPro;
using UnityEngine;

namespace Defense.Units
{
    /// <summary>
    /// 탭한 칸에 유닛이 있으면 판매 버튼을 보여주고, 누르면 그 칸의 유닛을 한 마리 팔아 전투 재화로 바꾼다.
    /// 승급 버튼(SlotPromotePresenter)과 같은 방식으로 칸 위에 뜨며, 두 버튼은 Screen Offset으로 좌우로 벌려 나란히 띄운다.
    ///
    /// 이 스크립트는 항상 켜져 있는 오브젝트에 붙이고, 켜고 끌 버튼은 _button으로 따로 연결한다.
    /// </summary>
    public class SlotSellPresenter : SlotActionPresenter
    {
        [SerializeField] SellManager _sellManager;

        [Tooltip("판매 버튼의 글자. '판매' 아래에 이번에 팔 유닛 한 마리의 판매가를 보여준다. 비워두면 글자는 그대로 둔다.")]
        [SerializeField] TMP_Text _priceLabel;

        protected override bool HasExtraReferences => _sellManager != null;

        // 팔 유닛이 있는 칸이면 쓸 수 있다. (null 칸이면 false)
        protected override bool IsAvailable(Slot slot) => _sellManager.CanSell(slot);

        protected override void Execute(Slot slot) => _sellManager.Sell(slot);

        // 칸마다 등급이 달라 판매가도 달라지므로, 버튼이 뜰 때마다 글자를 갱신한다.
        protected override void OnShown(Slot slot)
        {
            if (_priceLabel != null) _priceLabel.text = $"판매\n{_sellManager.GetSellPrice(slot)}";
        }
    }
}
