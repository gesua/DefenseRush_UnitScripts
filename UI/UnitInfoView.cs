using UnityEngine;

namespace Defense.Units
{
    /// <summary>
    /// 유닛 정보창(UnitInfoPanel)과 유닛 프리뷰 프레임(UnitPreviewInteraction)을 한 묶음으로 다루는 컨테이너의 진입점.
    /// 프리뷰 프레임은 정보창 영역 밖(캔버스 기준 위치)에 놓이므로, 둘을 합친 입력 통과 판정(ContainsScreenPoint)을 여기서 한 곳에 모아 제공한다.
    /// 정보창과 프리뷰를 보이고 숨기는 일은 각자(UnitInfoPanel, UnitPreviewInteraction)가 맡고, 이 컴포넌트는 판정만 모은다.
    /// 컨테이너 루트(화면 전체 stretch)에 붙이며, 루트에는 입력을 막는 Graphic을 두지 않는다.
    /// </summary>
    public sealed class UnitInfoView : MonoBehaviour
    {
        [Tooltip("유닛 정보창. 비워두면 자식에서 찾는다.")]
        [SerializeField] UnitInfoPanel _panel;
        [Tooltip("유닛 프리뷰 프레임의 상호작용. 비워두면 자식에서 찾는다.")]
        [SerializeField] UnitPreviewInteraction _previewInteraction;

        void Awake()
        {
            if (_panel == null) _panel = GetComponentInChildren<UnitInfoPanel>(true);
            if (_previewInteraction == null) _previewInteraction = GetComponentInChildren<UnitPreviewInteraction>(true);

            if (_panel == null) Debug.LogWarning("[UnitInfoView] UnitInfoPanel을 찾지 못했습니다.", this);
            if (_previewInteraction == null) Debug.LogWarning("[UnitInfoView] UnitPreviewInteraction을 찾지 못했습니다.", this);
        }

        /// <summary>
        /// 정보창이나 유닛 프리뷰 프레임이 보이는 상태에서 화면 좌표가 그 영역 안인지 알려준다.
        /// 강화·도박 패널의 GridRaycastPassThrough가 이 영역의 입력(스킬 아이콘, 프리뷰 확대와 회전 등)을 통과시킬지 정할 때 쓴다.
        /// 숨겨진 동안에는 항상 false라서, 보이지 않는 자리는 기존처럼 입력을 막는다.
        /// 유닛 프리뷰 프레임은 정보창 영역 밖에 있을 수 있어서 따로 확인한다. (확대된 크기도 반영된다)
        /// </summary>
        public bool ContainsScreenPoint(Vector2 screenPoint, Camera eventCamera)
        {
            if (_panel != null && _panel.ContainsScreenPoint(screenPoint, eventCamera)) return true;

            return (_previewInteraction != null) && _previewInteraction.ContainsScreenPoint(screenPoint, eventCamera);
        }
    }
}
