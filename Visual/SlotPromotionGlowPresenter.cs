using System.Collections.Generic;
using UnityEngine;

namespace Defense.Units
{
    /// <summary>
    /// 승급 가능한 칸(PromotionManager.CanPromote)에 네모 테두리 발광을 표시한다.
    /// UnitField(GridField가 있는 오브젝트)에 붙인다.
    /// GridField의 SlotChanged를 구독해서 칸이 바뀔 때마다 그 칸의 테두리를 켜고 끈다.
    /// </summary>
    [RequireComponent(typeof(GridField))]
    public class SlotPromotionGlowPresenter : MonoBehaviour
    {
        [Header("발광 설정")]
        [SerializeField] Color _glowColor = new Color(0.08f, 1f, 0.72f, 0.28f);
        [SerializeField] Color _lineColor = new Color(0.12f, 1f, 0.78f, 1f);
        [SerializeField, Min(0.01f)] float _glowWidth = 0.16f;
        [SerializeField, Min(0.01f)] float _lineWidth = 0.045f;
        [Tooltip("테두리용 Unlit 머티리얼. 색상은 이 스크립트가 복사본에 지정한다. (RangeIndicator에 쓰는 Sprite-Unlit-Default도 가능)")]
        [SerializeField] Material _material;

        [Header("승급 가능 표시")]
        [Tooltip("승급 가능한 슬롯 사각 테두리 바깥쪽 여유 공간. 0이면 발광의 바깥쪽 끝이 칸 경계에 딱 맞는다.")]
        [SerializeField, Min(0f)] float _padding = 0f;
        [Tooltip("테두리를 유닛 발밑에서 띄울 높이")]
        [SerializeField] float _height = 0.04f;
        [Tooltip("승급 가능 여부를 확인할 PromotionManager. 비어 있으면 씬에서 자동으로 찾습니다.")]
        [SerializeField] PromotionManager _promotionManager;

        readonly Dictionary<Slot, SlotPromotionGlow> _glows = new Dictionary<Slot, SlotPromotionGlow>();
        GridField _grid;
        Material _glowMaterial;
        Material _lineMaterial;

        void Awake()
        {
            TryGetComponent(out _grid);
        }

        void OnEnable()
        {
            _grid.SlotChanged += OnSlotChanged;

            // 비활성화된 동안 바뀐 칸이 있을 수 있으므로 다시 켤 때 전체를 갱신한다.
            UpdateAllGlows();
        }

        void OnDisable()
        {
            _grid.SlotChanged -= OnSlotChanged;
        }

        // GridField.Awake에서 칸이 만들어지므로 모든 Awake가 끝난 Start에서 테두리를 만든다.
        void Start()
        {
            if (_promotionManager == null)
                _promotionManager = FindFirstObjectByType<PromotionManager>();

            if (_promotionManager == null)
                Debug.LogWarning("[SlotPromotionGlowPresenter] PromotionManager를 찾을 수 없어 승급 테두리를 표시하지 않습니다.", this);

            if (CreateMaterials() == false) return;

            CreateGlows();
            UpdateAllGlows();
        }

        void OnDestroy()
        {
            if (_glowMaterial != null)
                Destroy(_glowMaterial);
            if (_lineMaterial != null)
                Destroy(_lineMaterial);
        }

        bool CreateMaterials()
        {
            if (_material == null)
            {
                Debug.LogWarning("[SlotPromotionGlowPresenter] 테두리용 머티리얼이 비어 있어 승급 테두리를 표시하지 않습니다.", this);
                return false;
            }

            // 색상이 다른 두 선이 공유 머티리얼 에셋에 덮어쓰이지 않도록 복사본을 만든다. (칸마다 만들지 않고 전체가 공유)
            _glowMaterial = CreateColorMaterial("Runtime_SlotPromotionGlow_Glow", _glowColor);
            _lineMaterial = CreateColorMaterial("Runtime_SlotPromotionGlow_Line", _lineColor);
            return true;
        }

        Material CreateColorMaterial(string materialName, Color color)
        {
            Material materialInstance = new Material(_material)
            {
                name = materialName
            };

            // URP Unlit은 _BaseColor, 기본 Unlit/Sprite 셰이더는 _Color를 사용하는 경우가 있어 둘 다 설정한다.
            if (materialInstance.HasProperty("_BaseColor"))
                materialInstance.SetColor("_BaseColor", color);
            if (materialInstance.HasProperty("_Color"))
                materialInstance.SetColor("_Color", color);

            // URP Unlit 셰이더에서도 바깥쪽 발광 선의 투명도가 적용되도록 설정한다.
            if (materialInstance.HasProperty("_Surface"))
                materialInstance.SetFloat("_Surface", 1f);
            if (materialInstance.HasProperty("_SrcBlend"))
                materialInstance.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (materialInstance.HasProperty("_DstBlend"))
                materialInstance.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (materialInstance.HasProperty("_ZWrite"))
                materialInstance.SetFloat("_ZWrite", 0f);
            materialInstance.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            return materialInstance;
        }

        void CreateGlows()
        {
            _glows.Clear();
            for (int i = 0; i < _grid.Slots.Count; i++)
            {
                Slot slot = _grid.Slots[i];

                GameObject glowObject = new GameObject($"SlotPromotionGlow_{slot.Row}_{slot.Col}");
                glowObject.transform.SetParent(transform, false);

                SlotPromotionGlow glow = glowObject.AddComponent<SlotPromotionGlow>();
                glow.Initialize(BuildCorners(slot), _glowMaterial, _glowWidth, _lineMaterial, _lineWidth);
                _glows.Add(slot, glow);
            }
        }

        // 칸 중심에서 padding만큼 넓힌 사각형의 네 모서리. 그리드가 회전해 있어도 맞도록 그리드 회전을 적용한다.
        // 선은 모서리를 중심으로 굵기의 절반씩 양쪽으로 퍼지므로, 발광 굵기의 절반만큼 안쪽으로 당겨서 옆칸을 침범하지 않게 한다.
        Vector3[] BuildCorners(Slot slot)
        {
            Vector3 center = slot.Center + _grid.transform.up * _height;
            Quaternion rotation = _grid.transform.rotation;
            float glowHalf = _glowWidth * 0.5f;
            float halfWidth = Mathf.Max(0f, _grid.CellSize.x * 0.5f - glowHalf + _padding);
            float halfDepth = Mathf.Max(0f, _grid.CellSize.y * 0.5f - glowHalf + _padding);

            return new Vector3[]
            {
                center + rotation * new Vector3(-halfWidth, 0f, -halfDepth),
                center + rotation * new Vector3(-halfWidth, 0f, halfDepth),
                center + rotation * new Vector3(halfWidth, 0f, halfDepth),
                center + rotation * new Vector3(halfWidth, 0f, -halfDepth)
            };
        }

        void OnSlotChanged(Slot slot)
        {
            UpdateGlow(slot);
        }

        void UpdateAllGlows()
        {
            foreach (Slot slot in _glows.Keys)
                UpdateGlow(slot);
        }

        void UpdateGlow(Slot slot)
        {
            if (_glows.TryGetValue(slot, out SlotPromotionGlow glow) == false) return;

            bool canPromote = _promotionManager != null && _promotionManager.CanPromote(slot);
            glow.SetVisible(canPromote);
        }
    }
}