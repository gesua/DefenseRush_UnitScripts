using UnityEngine;

namespace Defense.Units
{
    /// <summary>
    /// 강화 탭의 소환 확률 카드. 카드 루트에 붙이고, 카드 안의 버튼/라벨을 연결한다.
    /// 버튼을 누르면 SummonChanceProgress.TryUpgrade를 호출하고, 레벨·비용 표시와 버튼 활성 상태를 갱신한다.
    /// </summary>
    public class SummonChanceCard : EnhanceCardBase
    {
        [SerializeField] SummonChanceProgress _progress;

        protected override bool HasReferences => _progress != null;
        protected override bool IsMaxLevel => _progress.IsMaxLevel;
        protected override int Level => _progress.Level;
        protected override int NextCost => _progress.NextCost;

        protected override bool TryEnhance() => _progress.TryUpgrade();

        protected override void Subscribe() => _progress.LevelChanged += Refresh;

        protected override void Unsubscribe() => _progress.LevelChanged -= Refresh;
    }
}
