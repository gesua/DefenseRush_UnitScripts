using UnityEngine;

namespace Defense.Units
{
    /// <summary>
    /// 유닛 발밑에 등급 색 원을 깐다. (기본: 일반 흰색 / 희귀 파랑 / 영웅 보라 / 전설 주황)
    /// 등급은 Unit.Init이 끝난 뒤에 읽어야 하므로 Start에서 만든다. (소환 직후 같은 프레임에 만들어진다)
    /// 스프라이트를 비워두면 코드로 만든 원을 쓰고, 모든 유닛이 그 스프라이트 하나를 같이 쓴다.
    /// Unit_Base 프리팹에 붙이면 모든 유닛(바리언트 포함)에 적용된다.
    /// </summary>
    [RequireComponent(typeof(Unit))]
    public class UnitGradeRing : MonoBehaviour
    {
        // 기본 원 스프라이트. 유닛마다 만들지 않도록 하나만 만들어 공유한다.
        static Sprite _sharedSprite;

        [Tooltip("비워두면 코드로 만든 원을 쓴다. 직접 만든 이미지를 넣으면 색만 입혀서 쓴다. (흰색 이미지 권장)")]
        [SerializeField] Sprite _sprite;
        [Tooltip("원의 지름(월드 단위)")]
        [SerializeField, Min(0.1f)] float _diameter = 1.2f;
        [Tooltip("유닛 발 위치에서 띄울 높이. 바닥과 겹쳐 깜빡이면(z-fighting) 조금 키운다.")]
        [SerializeField] float _height = 0.03f;
        [SerializeField] int _sortingOrder = 0;
        [Tooltip("등급 순서(일반, 희귀, 영웅, 전설)대로의 색")]
        [SerializeField] Color[] _gradeColors = DefaultColors();

        Unit _unit;

        // 기본 색은 UnitGradeColors에서 가져온다. (정보창 등과 같은 색을 쓰도록 한 곳에서 관리)
        static Color[] DefaultColors()
        {
            var colors = new Color[UnitGrades.Count];
            for (int i = 0; i < colors.Length; i++) colors[i] = UnitGradeColors.Get((UnitGrade)i);
            return colors;
        }

        void Awake()
        {
            TryGetComponent(out _unit);
        }

        void Start()
        {
            if (_unit == null || _unit.Data == null)
            {
                Debug.LogWarning("[UnitGradeRing] Unit.Init이 호출되지 않아 등급 원을 만들지 못했습니다.", this);
                return;
            }

            CreateRing(_unit.Data.Grade);
        }

        void CreateRing(UnitGrade grade)
        {
            Sprite sprite = (_sprite != null) ? _sprite : GetSharedSprite();

            var go = new GameObject("GradeRing");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, _height, 0f);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // 바닥에 눕힌다

            // 스프라이트 크기와 상관없이 지름이 _diameter가 되도록 맞춘다
            float scale = _diameter / sprite.bounds.size.x;
            go.transform.localScale = new Vector3(scale, scale, 1f);

            SpriteRenderer ring = go.AddComponent<SpriteRenderer>();
            ring.sprite = sprite;
            ring.color = GetColor(grade);
            ring.sortingOrder = _sortingOrder;
        }

        // 프리팹에 저장된 색 배열이 등급 수보다 짧으면(등급 추가 직후) 빠진 등급은 기본 색을 쓴다.
        Color GetColor(UnitGrade grade)
        {
            int index = (int)grade;
            if (_gradeColors != null && index < _gradeColors.Length) return _gradeColors[index];

            return GetDefaultColor(index);
        }

        static Color GetDefaultColor(int index) => UnitGradeColors.Get((UnitGrade)index);

        static Sprite GetSharedSprite()
        {
            // 플레이를 껐다 켜면 Unity가 만든 텍스처를 정리하므로, 사라졌으면 다시 만든다
            if (_sharedSprite == null) _sharedSprite = GuideSprites.CreateDisc();
            return _sharedSprite;
        }

        // 등급이 추가되면 색 배열이 자동으로 늘어난다. (새 칸은 기본 색, 기본 색이 없으면 흰색)
        void OnValidate()
        {
            int count = UnitGrades.Count;
            if (_gradeColors != null && _gradeColors.Length == count) return;

            Color[] resized = new Color[count];
            for (int i = 0; i < count; i++)
                resized[i] = (_gradeColors != null && i < _gradeColors.Length) ? _gradeColors[i] : GetDefaultColor(i);

            _gradeColors = resized;
        }
    }
}