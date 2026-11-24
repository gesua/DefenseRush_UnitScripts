using System.Collections.Generic;
using UnityEngine;

namespace Defense.Units
{
    /// <summary>
    /// 인접 칸 아군에게 버프를 주는 스킬의 영향 범위를 평상시 바닥에 표시한다.
    /// UnitField(GridField가 있는 오브젝트)에 붙인다.
    /// - Tile(상시 오라: 투지, 고무): 영향받는 칸을 색으로 채운다.
    /// - Brackets(확률 발동 일시 버프): 영향받는 칸의 모서리에 괄호를 그린다.
    /// 칸 구성이 바뀔 때(소환, 이동, 판매, 승급)만 갱신하고, 같은 프레임에 여러 번 바뀌어도 LateUpdate에서 한 번만 다시 그린다.
    /// 한 칸에 서로 다른 색의 표시가 겹치면 Tile은 칸을 가로로 나눠 색마다 띠로, Brackets는 안쪽으로 겹쳐서 그린다.
    /// 승급 가능 칸의 청록 테두리(SlotPromotionGlowPresenter)와 겹쳐도 구분되도록 채움/괄호 모양으로 표시한다.
    /// 표시 스프라이트는 코드로 만들어 모든 칸이 같이 쓰고, UnitField의 스케일은 1이라고 가정한다.
    /// </summary>
    [RequireComponent(typeof(GridField))]
    public class SlotAuraFloorPresenter : MonoBehaviour
    {
        [Header("표시 설정")]
        [Tooltip("Tile 표시의 투명도. 스프라이트 자체가 테두리는 진하고 안쪽은 옅게 만들어져 있다.")]
        [SerializeField, Range(0f, 1f)] float _tileAlpha = 0.6f;
        [Tooltip("Brackets 표시의 투명도")]
        [SerializeField, Range(0f, 1f)] float _bracketAlpha = 0.9f;
        [Tooltip("표시를 바닥에서 띄울 높이. 바닥과 겹쳐 깜빡이면(z-fighting) 조금 키운다.")]
        [SerializeField] float _height = 0.02f;
        [Tooltip("칸 가장자리에서 안쪽으로 줄일 여백. 옆 칸의 표시와 붙어 보이지 않게 한다.")]
        [SerializeField, Min(0f)] float _padding = 0.05f;
        [Tooltip("괄호가 여러 겹일 때 겹 사이의 간격")]
        [SerializeField, Min(0.01f)] float _bracketStep = 0.12f;
        [SerializeField] int _tileSortingOrder = -1;
        [SerializeField] int _bracketSortingOrder = 1;

        // 칸 하나의 표시 상태. 색 목록은 갱신할 때마다 새로 채우고, 렌더러는 한 번 만들면 재사용한다.
        class SlotView
        {
            public readonly Slot Slot;
            public readonly List<Color> TileColors = new List<Color>();
            public readonly List<Color> BracketColors = new List<Color>();
            public readonly List<SpriteRenderer> TileRenderers = new List<SpriteRenderer>();
            public readonly List<SpriteRenderer> BracketRenderers = new List<SpriteRenderer>();

            public SlotView(Slot slot)
            {
                Slot = slot;
            }

            public void ClearColors()
            {
                TileColors.Clear();
                BracketColors.Clear();
            }

            // 같은 색은 한 번만 담는다. (같은 칸에 같은 유닛이 3마리 쌓여도 표시는 하나)
            public void AddColor(FloorMarkStyle style, Color color)
            {
                List<Color> colors = (style == FloorMarkStyle.Tile) ? TileColors : BracketColors;
                if (colors.Contains(color) == false) colors.Add(color);
            }
        }

        // 스프라이트는 바닥에 눕혀 쓴다. 눕히면 스프라이트의 x는 그리드의 x, y는 그리드의 z와 맞는다.
        static readonly Quaternion _flatRotation = Quaternion.Euler(90f, 0f, 0f);

        readonly Dictionary<Slot, SlotView> _views = new Dictionary<Slot, SlotView>();
        // 스킬마다 영향받는 칸을 받아 오는 임시 버퍼
        readonly List<Slot> _targets = new List<Slot>();

        GridField _grid;
        Sprite _tileSprite;
        Sprite _bracketSprite;
        bool _isInitialized;
        bool _isDirty;

        void Awake()
        {
            TryGetComponent(out _grid);
        }

        void OnEnable()
        {
            _grid.SlotChanged += OnSlotChanged;

            // 비활성화된 동안 바뀐 칸이 있을 수 있으므로 다시 켤 때 전체를 갱신한다.
            _isDirty = true;
        }

        void OnDisable()
        {
            _grid.SlotChanged -= OnSlotChanged;
        }

        // GridField.Awake에서 칸이 만들어지므로 모든 Awake가 끝난 Start에서 표시를 준비한다.
        void Start()
        {
            _tileSprite = GuideSprites.CreateTile();
            _bracketSprite = GuideSprites.CreateBrackets();

            CreateViews();

            _isInitialized = true;
            _isDirty = true;
        }

        void OnDestroy()
        {
            GuideSprites.DestroySprite(_tileSprite);
            GuideSprites.DestroySprite(_bracketSprite);
        }

        void LateUpdate()
        {
            if (_isInitialized == false || _isDirty == false) return;

            _isDirty = false;
            Refresh();
        }

        // 이동은 두 칸에서 SlotChanged가 오므로, 바로 그리지 않고 표시만 해 두었다가 LateUpdate에서 한 번에 그린다.
        void OnSlotChanged(Slot slot)
        {
            _isDirty = true;
        }

        void CreateViews()
        {
            _views.Clear();
            for (int i = 0; i < _grid.Slots.Count; i++)
            {
                Slot slot = _grid.Slots[i];
                _views.Add(slot, new SlotView(slot));
            }
        }

        void Refresh()
        {
            foreach (SlotView view in _views.Values)
                view.ClearColors();

            CollectMarks();

            foreach (SlotView view in _views.Values)
            {
                ApplyTiles(view);
                ApplyBrackets(view);
            }
        }

        // 필드의 모든 유닛 스킬에서 바닥 표시를 받아 영향받는 칸의 표시 목록에 담는다.
        void CollectMarks()
        {
            IReadOnlyList<Slot> slots = _grid.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                IReadOnlyList<Unit> units = slots[i].Units;
                for (int j = 0; j < units.Count; j++)
                {
                    Unit unit = units[j];
                    if (unit.Data == null) continue;

                    IReadOnlyList<UnitSkill> skills = unit.Data.Skills;
                    for (int k = 0; k < skills.Count; k++)
                    {
                        if (skills[k] == null) continue;

                        _targets.Clear();
                        if (skills[k].TryGetFloorMark(unit, _targets, out Color color, out FloorMarkStyle style) == false) continue;

                        for (int t = 0; t < _targets.Count; t++)
                        {
                            if (_views.TryGetValue(_targets[t], out SlotView view)) view.AddColor(style, color);
                        }
                    }
                }
            }

            _targets.Clear();
        }

        // 색 하나당 칸을 가로로 나눈 띠 하나. 색이 하나면 칸 전체를 채운다.
        void ApplyTiles(SlotView view)
        {
            int count = view.TileColors.Count;
            Vector2 cellSize = GetInnerCellSize(0f);
            float stripWidth = cellSize.x / Mathf.Max(1, count);

            for (int i = 0; i < count; i++)
            {
                SpriteRenderer renderer = GetRenderer(view, view.TileRenderers, _tileSprite, _tileSortingOrder, "Tile", i);
                float offsetX = -cellSize.x * 0.5f + stripWidth * (i + 0.5f);

                Place(renderer, view.Slot, new Vector3(offsetX, 0f, 0f), new Vector2(stripWidth, cellSize.y));
                renderer.color = WithAlpha(view.TileColors[i], _tileAlpha);
                renderer.enabled = true;
            }

            HideRenderersFrom(view.TileRenderers, count);
        }

        // 색 하나당 괄호 한 겹. 두 번째 색부터는 안쪽으로 한 겹씩 들어간다.
        void ApplyBrackets(SlotView view)
        {
            int count = view.BracketColors.Count;

            for (int i = 0; i < count; i++)
            {
                SpriteRenderer renderer = GetRenderer(view, view.BracketRenderers, _bracketSprite, _bracketSortingOrder, "Brackets", i);

                Place(renderer, view.Slot, Vector3.zero, GetInnerCellSize(i * _bracketStep));
                renderer.color = WithAlpha(view.BracketColors[i], _bracketAlpha);
                renderer.enabled = true;
            }

            HideRenderersFrom(view.BracketRenderers, count);
        }

        // 칸 크기에서 여백과 추가 안쪽 간격(양쪽)을 뺀 크기. 너무 작아지지 않게 최소값을 둔다.
        Vector2 GetInnerCellSize(float extraInset)
        {
            float inset = (_padding + extraInset) * 2f;
            return new Vector2(Mathf.Max(0.2f, _grid.CellSize.x - inset), Mathf.Max(0.2f, _grid.CellSize.y - inset));
        }

        // 칸 중심에서 그리드 기준 localOffset만큼 떨어진 곳에 눕혀 놓는다. 그리드가 회전해 있어도 맞도록 그리드 회전을 적용한다.
        void Place(SpriteRenderer renderer, Slot slot, Vector3 localOffset, Vector2 size)
        {
            Transform gridTransform = _grid.transform;
            Transform rendererTransform = renderer.transform;

            rendererTransform.position = slot.Center + gridTransform.rotation * localOffset + gridTransform.up * _height;
            rendererTransform.rotation = gridTransform.rotation * _flatRotation;
            rendererTransform.localScale = new Vector3(size.x, size.y, 1f);
        }

        // 필요한 개수보다 렌더러가 모자라면 그때 만든다. (표시가 겹치는 칸만 여러 개가 필요하다)
        SpriteRenderer GetRenderer(SlotView view, List<SpriteRenderer> pool, Sprite sprite, int sortingOrder, string kind, int index)
        {
            while (pool.Count <= index)
            {
                var go = new GameObject($"AuraFloor_{kind}_{view.Slot.Row}_{view.Slot.Col}_{pool.Count}");
                go.transform.SetParent(transform, false);

                SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sortingOrder = sortingOrder;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                pool.Add(renderer);
            }

            return pool[index];
        }

        static void HideRenderersFrom(List<SpriteRenderer> pool, int startIndex)
        {
            for (int i = startIndex; i < pool.Count; i++)
                pool[i].enabled = false;
        }

        // 스킬이 준 색의 투명도는 쓰지 않고 표시 종류별 투명도로 통일한다.
        static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }
    }
}
