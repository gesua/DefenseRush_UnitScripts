using UnityEngine;

namespace Defense.Units
{
    /// <summary>
    /// 승급 가능한 슬롯의 네모 칸 테두리를 고정 발광으로 표시한다.
    /// SlotPromotionGlowPresenter가 슬롯 변경 시 표시 여부를 갱신한다.
    /// 테두리 위치는 Initialize에서 한 번만 정해지므로 이후에는 표시 여부만 바꾼다.
    /// </summary>
    public class SlotPromotionGlow : MonoBehaviour
    {
        const int GLOW_SORTING_ORDER = 0;
        const int LINE_SORTING_ORDER = 1;

        LineRenderer _glowRenderer;
        LineRenderer _lineRenderer;
        bool _isInitialized;

        /// <summary>테두리 네 모서리(순서대로 이어진다)와 바깥 발광/안쪽 선의 머티리얼·굵기로 렌더러를 만든다.</summary>
        public void Initialize(Vector3[] corners, Material glowMaterial, float glowWidth, Material lineMaterial, float lineWidth)
        {
            _glowRenderer = CreateLineRenderer("Glow", corners, glowMaterial, glowWidth, GLOW_SORTING_ORDER);
            _lineRenderer = CreateLineRenderer("Line", corners, lineMaterial, lineWidth, LINE_SORTING_ORDER);

            _isInitialized = true;
            SetVisible(false);
        }

        LineRenderer CreateLineRenderer(string objectName, Vector3[] corners, Material material, float width, int sortingOrder)
        {
            GameObject lineObject = new GameObject(objectName);
            lineObject.transform.SetParent(transform, false);

            LineRenderer lineRenderer = lineObject.AddComponent<LineRenderer>();
            lineRenderer.useWorldSpace = true;
            lineRenderer.loop = true;
            lineRenderer.positionCount = corners.Length;
            lineRenderer.SetPositions(corners);
            lineRenderer.startWidth = width;
            lineRenderer.endWidth = width;
            lineRenderer.numCapVertices = 4;
            lineRenderer.numCornerVertices = 4;
            lineRenderer.alignment = LineAlignment.View;
            lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lineRenderer.receiveShadows = false;
            lineRenderer.sharedMaterial = material;
            // 색상은 개별 머티리얼에 지정하고 정점 색상은 흰색으로 두어 중복 곱셈을 방지한다.
            lineRenderer.startColor = Color.white;
            lineRenderer.endColor = Color.white;
            lineRenderer.sortingOrder = sortingOrder;
            return lineRenderer;
        }

        public void SetVisible(bool visible)
        {
            if (_isInitialized == false) return;

            _glowRenderer.enabled = visible;
            _lineRenderer.enabled = visible;
        }
    }
}