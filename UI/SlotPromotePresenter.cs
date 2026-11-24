using UnityEngine;

namespace Defense.Units
{
    /// <summary>
    /// 탭한 칸이 승급 가능(3마리, 최고 등급 아님)할 때만 버튼을 보여주고,
    /// 누르면 그 칸의 유닛을 다음 등급의 랜덤 유닛으로 바꾼다.
    /// 버튼은 탭한 칸 위(화면상 위치)에 띄우고, Canvas는 Screen Space - Overlay여야 한다.
    ///
    /// 이 스크립트는 항상 켜져 있는 오브젝트에 붙이고, 켜고 끌 버튼은 _button으로 따로 연결한다.
    /// (버튼 자신에 붙이면 버튼이 꺼진 동안 이벤트를 받지 못해서 다시 켜지지 않는다)
    /// </summary>
    public class SlotPromotePresenter : SlotActionPresenter
    {
        [SerializeField] PromotionManager _promotionManager;

        protected override bool HasExtraReferences => _promotionManager != null;

        // 승급 가능 여부는 PromotionManager가 판단한다. (null 칸이면 false)
        protected override bool IsAvailable(Slot slot) => _promotionManager.CanPromote(slot);

        protected override void Execute(Slot slot) => _promotionManager.Promote(slot);
    }
}