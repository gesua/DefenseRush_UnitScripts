using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Defense.Units
{
    /// <summary>
    /// 그리드 위의 드래그(칸 단위 이동)와 탭(칸 선택) 입력을 처리한다.
    ///
    /// EventSystem 기반이라 기존 Input / 새 Input System 어느 쪽이든 동작하고,
    /// UI 위에서 시작한 터치는 자동으로 걸러진다.
    /// 필요한 것: 씬의 EventSystem, 카메라의 Physics Raycaster, 이 오브젝트의 Collider(그리드 영역).
    /// 유닛은 이 오브젝트의 자식이라 유닛 콜라이더에 닿은 이벤트도 여기로 전달된다.
    /// 몬스터 콜라이더가 입력을 가로막으면 Physics Raycaster의 Event Mask에서 제외할 것.
    ///
    /// 드래그 중에 유닛은 움직이지 않고, DragStarted / DragMoved / DragEnded 이벤트로 연출(DragGuide 등)에 알린다.
    /// </summary>
    [RequireComponent(typeof(GridField))]
    public class GridDragInput : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] GridField _grid;
        Slot _dragSource;

        /// <summary>칸을 탭했을 때 호출된다. (UI가 구독해서 업그레이드 버튼 등을 표시)</summary>
        public event Action<Slot> SlotTapped;

        /// <summary>유닛이 있는 칸을 집어서 드래그를 시작할 때 호출된다. (인자: 집은 칸)</summary>
        public event Action<Slot> DragStarted;

        /// <summary>드래그하는 동안 포인터가 움직일 때마다 호출된다. (인자: 집은 칸, 포인터가 있는 칸. UI 위나 그리드 밖이면 null)</summary>
        public event Action<Slot, Slot> DragMoved;

        /// <summary>드래그가 끝나면(놓으면) 이동 처리 전에 호출된다.</summary>
        public event Action DragEnded;

        /// <summary>그리드 밖의 빈 배경을 탭했을 때 호출된다. (선택 해제용. UI 위의 탭은 오지 않는다)</summary>
        public event Action BackgroundTapped;

        /// <summary>BackgroundTapInput이 빈 배경 탭을 알릴 때 호출한다.</summary>
        public void NotifyBackgroundTapped() => BackgroundTapped?.Invoke();

        public void OnPointerClick(PointerEventData eventData)
        {
            Slot slot = _grid.WorldToSlot(ScreenToGround(eventData, eventData.position));
            if (slot != null) SlotTapped?.Invoke(slot);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            // 드래그 임계값을 넘은 현재 위치가 아니라, 처음 누른 위치의 칸을 집는다.
            Slot slot = _grid.WorldToSlot(ScreenToGround(eventData, eventData.pressPosition));
            _dragSource = (slot != null && slot.IsEmpty == false) ? slot : null;

            if (_dragSource != null) DragStarted?.Invoke(_dragSource);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_dragSource == null) return;

            // 유닛은 움직이지 않고, 포인터가 놓인 칸만 알려준다.
            DragMoved?.Invoke(_dragSource, GetHoverSlot(eventData));
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_dragSource == null) return;

            Slot from = _dragSource;
            _dragSource = null;
            DragEnded?.Invoke();

            // UI 위나 그리드 밖에서 놓으면 이동하지 않고 원위치로 돌아간다.
            Slot to = GetHoverSlot(eventData);

            if (_grid.TryMove(from, to) == false)
                from.Relayout();
        }

        bool IsOverGrid(PointerEventData eventData)
        {
            GameObject hit = eventData.pointerCurrentRaycast.gameObject;
            return hit != null && hit.transform.IsChildOf(transform);
        }

        // 포인터가 놓인 칸. UI 위나 그리드 밖이면 null.
        Slot GetHoverSlot(PointerEventData eventData) =>
            (IsOverGrid(eventData)) ? _grid.WorldToSlot(ScreenToGround(eventData, eventData.position)) : null;

        // 화면 좌표 → 그리드 바닥 평면 위의 월드 좌표
        Vector3 ScreenToGround(PointerEventData eventData, Vector2 screenPos)
        {
            Camera cam = (eventData.pressEventCamera != null) ? eventData.pressEventCamera : Camera.main;
            if (cam == null) return transform.position;

            Ray ray = cam.ScreenPointToRay(screenPos);
            var plane = new Plane(transform.up, transform.position);
            return (plane.Raycast(ray, out float distance)) ? ray.GetPoint(distance) : transform.position;
        }

        // 컴포넌트를 처음 붙일 때 그리드 크기에 맞는 BoxCollider를 자동으로 추가한다.
        void Reset() => FitCollider();

        [ContextMenu("Fit Collider To Grid")]
        void FitCollider()
        {
            if (TryGetComponent(out GridField field) == false) return;
            if (TryGetComponent(out BoxCollider box) == false) box = gameObject.AddComponent<BoxCollider>();

            Vector2 size = field.FieldSize;
            box.size = new Vector3(size.x, 0.2f, size.y);
            box.center = Vector3.zero;
        }
    }
}