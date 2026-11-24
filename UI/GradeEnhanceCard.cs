using UnityEngine;

namespace Defense.Units
{
    /// <summary>
    /// 강화 탭의 등급 카드 하나. 카드 루트에 붙이고, 카드 안의 버튼/라벨을 연결한다.
    /// 버튼을 누르면 InGameProgress.TryUpgrade(등급)을 호출하고, 레벨·비용 표시와 버튼 활성 상태를 갱신한다.
    /// </summary>
    public class GradeEnhanceCard : EnhanceCardBase
    {
        [SerializeField] InGameProgress _progress;
        [Tooltip("이 카드가 강화할 등급. 카드마다 다르게 지정한다. (오브젝트 이름이 아니라 카드에 표시된 등급 기준)")]
        [SerializeField] UnitGrade _grade;

        protected override bool HasReferences => _progress != null;
        protected override bool IsMaxLevel => _progress.IsMaxLevel(_grade);
        protected override int Level => _progress.GetLevel(_grade);
        protected override int NextCost => _progress.GetNextCost(_grade);

        protected override bool TryEnhance() => _progress.TryUpgrade(_grade);

        protected override void Subscribe() => _progress.LevelChanged += OnLevelChanged;

        protected override void Unsubscribe() => _progress.LevelChanged -= OnLevelChanged;

        void OnLevelChanged(UnitGrade grade)
        {
            if (grade == _grade) Refresh();
        }
    }
}