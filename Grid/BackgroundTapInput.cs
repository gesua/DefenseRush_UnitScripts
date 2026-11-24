using UnityEngine;
using UnityEngine.EventSystems;

namespace Defense.Units
{
    /// <summary>
    /// 빈 배경 탭을 GridDragInput에 알려서 유닛 선택을 풀게 한다.
    /// 화면 전체를 덮는 큰 Collider(그리드 Collider보다 아래)가 있는 오브젝트에 붙인다.
    /// 그리드나 유닛을 탭하면 더 가까운 그쪽 Collider가 먼저 받고, UI 위의 탭은 EventSystem이 걸러서 여기까지 오지 않는다.
    /// 필요한 것: 씬의 EventSystem, 카메라의 Physics Raycaster.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class BackgroundTapInput : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] GridDragInput _input;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_input != null) _input.NotifyBackgroundTapped();
        }
    }
}
