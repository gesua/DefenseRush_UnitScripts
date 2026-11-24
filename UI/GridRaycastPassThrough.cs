using UnityEngine;

namespace Defense.Units
{
    /// <summary>
    /// 강화·도박 패널의 전체 화면 배경이 그리드 입력을 가로막지 않도록 합니다.
    /// 그리드가 투영된 화면 영역과, 열려 있는 유닛 정보창과 유닛 프리뷰(UnitInfoView) 영역에서는 패널의 UI 레이캐스트를 통과시키고,
    /// 나머지 영역에서는 기존처럼 뒤쪽 UI 입력을 차단합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GridRaycastPassThrough : MonoBehaviour, ICanvasRaycastFilter
    {
        [SerializeField, Tooltip("비워 두면 씬에서 GridField를 자동으로 찾습니다.")]
        GridField _gridField;

        [SerializeField, Tooltip("비워 두면 씬에서 UnitInfoView를 자동으로 찾습니다. (다른 프리팹에 있어서 직접 연결하기 어렵습니다)")]
        UnitInfoView _unitInfoView;

        // 레이캐스트 판정은 포인터가 움직일 때마다 그래픽 수만큼 불리므로, 배열을 매번 만들지 않고 재사용한다.
        readonly Vector3[] _worldCorners = new Vector3[4];
        readonly Vector2[] _screenCorners = new Vector2[4];

        // 패널은 비활성 상태로 시작하므로 처음 열릴 때 찾고, 못 찾았다면 다시 열릴 때 한 번 더 찾는다.
        void OnEnable()
        {
            if (_gridField == null)
                _gridField = FindFirstObjectByType<GridField>();

            if (_unitInfoView == null)
                _unitInfoView = FindFirstObjectByType<UnitInfoView>(FindObjectsInactive.Include);
        }

        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            // 정보창이 열려 있으면 그 영역은 통과시켜서, 스킬 아이콘이나 초상화 버튼을 누를 수 있게 합니다.
            if (_unitInfoView != null && _unitInfoView.ContainsScreenPoint(screenPoint, eventCamera))
                return false;

            // 그리드를 찾지 못한 경우에는 안전하게 기존 입력 차단 동작을 유지합니다.
            if (_gridField == null)
                return true;

            Camera raycastCamera = (eventCamera != null) ? eventCamera : Camera.main;
            if (raycastCamera == null)
                return true;

            Vector2 fieldSize = _gridField.FieldSize;
            Transform fieldTransform = _gridField.transform;
            float halfWidth = fieldSize.x * 0.5f;
            float halfDepth = fieldSize.y * 0.5f;

            _worldCorners[0] = fieldTransform.TransformPoint(new Vector3(-halfWidth, 0f, -halfDepth));
            _worldCorners[1] = fieldTransform.TransformPoint(new Vector3(halfWidth, 0f, -halfDepth));
            _worldCorners[2] = fieldTransform.TransformPoint(new Vector3(halfWidth, 0f, halfDepth));
            _worldCorners[3] = fieldTransform.TransformPoint(new Vector3(-halfWidth, 0f, halfDepth));

            for (int i = 0; i < _worldCorners.Length; i++)
            {
                Vector3 screenCorner = raycastCamera.WorldToScreenPoint(_worldCorners[i]);
                if (screenCorner.z <= 0f)
                    return true;

                _screenCorners[i] = new Vector2(screenCorner.x, screenCorner.y);
            }

            // ICanvasRaycastFilter에서 false를 반환하면 해당 위치의 패널 그래픽이
            // UI 레이캐스트 결과에서 제외되어 PhysicsRaycaster가 그리드를 감지할 수 있습니다.
            return PointInPolygon(screenPoint, _screenCorners) == false;
        }

        static bool PointInPolygon(Vector2 point, Vector2[] polygon)
        {
            bool isInside = false;
            int previous = polygon.Length - 1;

            for (int current = 0; current < polygon.Length; current++)
            {
                Vector2 currentPoint = polygon[current];
                Vector2 previousPoint = polygon[previous];
                bool crossesY = (currentPoint.y > point.y) != (previousPoint.y > point.y);

                if (crossesY)
                {
                    float intersectionX = (previousPoint.x - currentPoint.x)
                        * (point.y - currentPoint.y)
                        / (previousPoint.y - currentPoint.y)
                        + currentPoint.x;

                    if (point.x < intersectionX)
                        isInside = isInside == false;
                }

                previous = current;
            }

            return isInside;
        }
    }
}