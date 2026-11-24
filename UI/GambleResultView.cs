using UnityEngine;

namespace Defense.Units
{
    /// <summary>
    /// 도박 결과(성공/실패/막힘)를 글자로 잠깐 보여준다. 일정 시간이 지나면 저절로 사라지고, 패널이 꺼지면 같이 사라진다.
    /// 도박 패널의 아무 오브젝트에 붙이되, 프레임(_frame)은 이 컴포넌트가 붙은 오브젝트나 그 부모가 아니어야 하고 글자(_label)는 프레임 안에 있어야 한다.
    /// </summary>
    public class GambleResultView : ToastView
    {
        [SerializeField] GambleManager _manager;

        [Header("문구")]
        [Tooltip("등급 순서(일반, 희귀, 영웅, 전설)대로의 이름")]
        [SerializeField] string[] _gradeNames = { "일반", "희귀", "영웅", "전설" };

        [Tooltip("도박 실패 글자색")]
        [SerializeField] Color _missColor = new Color(0.75f, 0.75f, 0.75f);

        bool HasReferences => _manager != null && HasView;

        void Awake()
        {
            if (HasReferences == false)
                Debug.LogError("[GambleResultView] manager / frame / label 참조가 비어 있거나, frame이 이 오브젝트(또는 부모)이거나, label이 frame 안에 있지 않습니다.", this);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (HasReferences == false) return;

            _manager.Gambled += OnGambled;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (HasReferences == false) return;

            _manager.Gambled -= OnGambled;
        }

        void OnGambled(GambleResult result)
        {
            switch (result.Outcome)
            {
                case GambleOutcome.Success:
                    Show($"{GetGradeName(result.Grade)} 도박 성공!\n{result.Data.DisplayName}", UnitGradeColors.Get(result.Grade));
                    break;

                case GambleOutcome.Miss:
                    // 유물 효과로 하위 등급 유닛을 대신 얻었으면 그 유닛을 함께 알린다.
                    if (result.HasConsolation)
                        Show($"{GetGradeName(result.Grade)} 도박 실패...\n유물 효과로 {result.ConsolationData.DisplayName} 획득!", UnitGradeColors.Get(result.ConsolationData.Grade));
                    else
                        Show($"{GetGradeName(result.Grade)} 도박 실패...", _missColor);
                    break;

                case GambleOutcome.NoFunds:
                    ShowWarning("재화가 부족합니다");
                    break;

                case GambleOutcome.NoSpace:
                    ShowNoSpace();
                    break;

                case GambleOutcome.UnitLimit:
                    ShowUnitLimit();
                    break;

                    // Unavailable은 설정/데이터 문제라 매니저가 이미 로그를 남긴다. 화면에는 띄우지 않는다.
            }
        }

        string GetGradeName(UnitGrade grade)
        {
            int index = (int)grade;
            return (_gradeNames != null && index < _gradeNames.Length) ? _gradeNames[index] : grade.ToString();
        }
    }
}