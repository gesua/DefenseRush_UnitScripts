using System.Collections.Generic;
using UnityEngine;

namespace Defense.Units
{
    /// <summary>
    /// 드래그하는 동안 모든 칸을 보여주고, 집은 칸(고리)에서 놓을 칸(괄호)까지 점선으로 이어준다.
    /// UnitField(GridField + GridDragInput이 있는 오브젝트)에 붙인다.
    /// 유닛은 드래그 중에 움직이지 않고, 놓는 순간 칸으로 이동한다.
    /// 그리드는 바닥과 평행하고 스케일이 1이어야 한다.
    /// </summary>
    [RequireComponent(typeof(GridField), typeof(GridDragInput))]
    public class DragGuide : MonoBehaviour
    {
        [Header("색상")]
        [Tooltip("모든 칸에 깔리는 타일 색. 알파로 진하기를 조절한다.")]
        [SerializeField] Color _tileColor = new Color(1f, 1f, 1f, 0.6f);
        [Tooltip("집은 칸의 고리, 놓을 칸의 괄호, 점선 색")]
        [SerializeField] Color _highlightColor = new Color(1f, 0.85f, 0.1f, 1f);

        [Header("모양")]
        [Tooltip("칸 하나 중 타일이 차지하는 비율. 남는 부분이 칸 사이 간격이 된다.")]
        [SerializeField, Range(0.5f, 1f)] float _tileFill = 0.94f;
        [Tooltip("집은 칸에 그리는 고리의 크기. (칸 크기 대비)")]
        [SerializeField, Range(0.2f, 1f)] float _ringScale = 0.6f;
        [Tooltip("바닥과 겹쳐 깜빡이지 않도록 그리드 위로 띄우는 높이")]
        [SerializeField] float _height = 0.04f;
        [Tooltip("비워두면 기본 스프라이트 머티리얼을 쓴다. 안 보이면 Sprite-Unlit-Default를 넣어 볼 것.")]
        [SerializeField] Material _material;

        GridField _grid;
        GridDragInput _input;
        GameObject _root;

        readonly List<SpriteRenderer> _tiles = new List<SpriteRenderer>();
        SpriteRenderer _ring;
        SpriteRenderer _brackets;
        SpriteRenderer _line;

        Sprite _tileSprite;
        Sprite _ringSprite;
        Sprite _bracketSprite;
        Sprite _dashSprite;

        void Awake()
        {
            TryGetComponent(out _grid);
            TryGetComponent(out _input);

            _root = new GameObject("DragGuide");
            _root.transform.SetParent(transform, false);

            _tileSprite = GuideSprites.CreateTile();
            _ringSprite = GuideSprites.CreateRing();
            _bracketSprite = GuideSprites.CreateBrackets();
            _dashSprite = GuideSprites.CreateDash();

            // 겹칠 때 보이는 순서: 타일 < 고리 < 점선 < 괄호
            _ring = CreateRenderer("Ring", _ringSprite, _highlightColor, 1);
            _line = CreateRenderer("Line", _dashSprite, _highlightColor, 2);
            _line.drawMode = SpriteDrawMode.Tiled;
            _line.tileMode = SpriteTileMode.Continuous;
            _brackets = CreateRenderer("Brackets", _bracketSprite, _highlightColor, 3);

            SetGuideVisible(false);
        }

        // 칸(Slot)은 GridField.Awake에서 만들어지므로 Start에서 타일을 깐다.
        void Start()
        {
            Vector2 cell = _grid.CellSize;
            IReadOnlyList<Slot> slots = _grid.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                SpriteRenderer tile = CreateRenderer("Tile_" + slots[i].Row + "_" + slots[i].Col, _tileSprite, _tileColor, -1);
                tile.transform.SetPositionAndRotation(Raised(slots[i].Center, 0f), Flat());
                tile.transform.localScale = new Vector3(cell.x * _tileFill, cell.y * _tileFill, 1f);
                tile.gameObject.SetActive(false);
                _tiles.Add(tile);
            }
        }

        void OnEnable()
        {
            _input.DragStarted += OnDragStarted;
            _input.DragMoved += OnDragMoved;
            _input.DragEnded += OnDragEnded;
        }

        void OnDisable()
        {
            _input.DragStarted -= OnDragStarted;
            _input.DragMoved -= OnDragMoved;
            _input.DragEnded -= OnDragEnded;
        }

        void OnDestroy()
        {
            GuideSprites.DestroySprite(_tileSprite);
            GuideSprites.DestroySprite(_ringSprite);
            GuideSprites.DestroySprite(_bracketSprite);
            GuideSprites.DestroySprite(_dashSprite);
        }

        // 드래그를 시작하면 모든 칸을 보여주고, 집은 칸에 고리를 둔다.
        void OnDragStarted(Slot source)
        {
            foreach (SpriteRenderer tile in _tiles) tile.gameObject.SetActive(true);

            float diameter = Mathf.Min(_grid.CellSize.x, _grid.CellSize.y) * _ringScale;
            _ring.transform.SetPositionAndRotation(Raised(source.Center, 0.01f), Flat());
            _ring.transform.localScale = new Vector3(diameter, diameter, 1f);
            _ring.gameObject.SetActive(true);

            HideTarget();
        }

        // 포인터가 다른 칸 위에 있으면 그 칸에 괄호를 씌우고 집은 칸과 점선으로 잇는다.
        void OnDragMoved(Slot source, Slot hover)
        {
            if (hover == null || hover == source)
            {
                HideTarget();
                return;
            }

            Vector2 cell = _grid.CellSize;
            _brackets.transform.SetPositionAndRotation(Raised(hover.Center, 0.03f), Flat());
            _brackets.transform.localScale = new Vector3(cell.x * _tileFill, cell.y * _tileFill, 1f);
            _brackets.gameObject.SetActive(true);

            UpdateLine(source.Center, hover.Center);
        }

        void OnDragEnded() => SetGuideVisible(false);

        // 점선은 고리 가장자리에서 시작해서 괄호 가장자리에서 끝난다.
        void UpdateLine(Vector3 from, Vector3 to)
        {
            Vector3 local = transform.InverseTransformVector(to - from);
            local.y = 0f;
            float distance = local.magnitude;
            if (distance <= 0.0001f)
            {
                _line.gameObject.SetActive(false);
                return;
            }

            float cellMin = Mathf.Min(_grid.CellSize.x, _grid.CellSize.y);
            float startTrim = cellMin * _ringScale * 0.5f;
            float endTrim = cellMin * _tileFill * 0.5f;
            float length = distance - startTrim - endTrim;
            if (length <= 0.05f)
            {
                _line.gameObject.SetActive(false);
                return;
            }

            Vector3 direction = transform.TransformDirection(local / distance);
            Vector3 middle = from + direction * (startTrim + length * 0.5f);
            float angle = Mathf.Atan2(-local.z, local.x) * Mathf.Rad2Deg;   // 스프라이트의 가로축을 진행 방향에 맞춘다

            _line.transform.SetPositionAndRotation(
                Raised(middle, 0.02f),
                transform.rotation * Quaternion.Euler(0f, angle, 0f) * Quaternion.Euler(90f, 0f, 0f));
            _line.size = new Vector2(length, GuideSprites.DASH_HEIGHT);
            _line.gameObject.SetActive(true);
        }

        void HideTarget()
        {
            _brackets.gameObject.SetActive(false);
            _line.gameObject.SetActive(false);
        }

        void SetGuideVisible(bool visible)
        {
            foreach (SpriteRenderer tile in _tiles) tile.gameObject.SetActive(visible);

            _ring.gameObject.SetActive(visible);
            _brackets.gameObject.SetActive(visible);
            _line.gameObject.SetActive(visible);
        }

        SpriteRenderer CreateRenderer(string objectName, Sprite sprite, Color color, int sortingOrder)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(_root.transform, false);

            var spriteRenderer = go.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            spriteRenderer.color = color;
            spriteRenderer.sortingOrder = sortingOrder;
            if (_material != null) spriteRenderer.sharedMaterial = _material;
            return spriteRenderer;
        }

        // 스프라이트를 바닥에 눕히는 회전
        Quaternion Flat() => transform.rotation * Quaternion.Euler(90f, 0f, 0f);

        // 바닥 위로 살짝 띄운 위치
        Vector3 Raised(Vector3 point, float extra) => point + transform.up * (_height + extra);
    }
}
