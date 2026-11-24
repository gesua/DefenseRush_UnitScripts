using UnityEngine;

namespace Defense.Units
{
    /// <summary>축복: 이 유닛이 필드에 있는 동안 희귀 이상 소환 확률이 올라간다.</summary>
    /// <remarks>
    /// 스킬 자체는 아무것도 실행하지 않는 표식이고, 소환 확률 계산은 SummonChanceProgress가 한다.
    /// (SummonChanceProgress의 grid에 GridField를 연결해야 반영된다) 축복 유닛이 여럿이면 값이 합산된다.
    /// </remarks>
    [CreateAssetMenu(fileName = "Skill_Blessing", menuName = "Defense/Skill/Blessing (축복)")]
    public class BlessingSkill : UnitSkill, ISummonChanceBonus
    {
        [Tooltip("일반 소환에서 희귀 이상으로 옮길 확률(전체 대비). 0.02면 일반 소환 확률 2%p가 희귀 이상으로 옮겨진다.")]
        [Min(0f)][SerializeField] float _rareOrHigherShift = 0.02f;

        public float RareOrHigherShift => _rareOrHigherShift;
    }
}