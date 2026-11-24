using UnityEngine;

namespace Defense.Units
{
    /// <summary>광분: 적을 처치하면 잠시 이 유닛의 공격속도가 올라간다. 지속 중에 또 처치하면 지속 시간만 갱신된다.</summary>
    [CreateAssetMenu(fileName = "Skill_Frenzy", menuName = "Defense/Skill/Frenzy (광분)")]
    public class FrenzySkill : UnitSkill
    {
        [Tooltip("올려 줄 공격속도 비율. 0.3이면 +30%")]
        [Min(0f)][SerializeField] float _attackSpeedRatio = 0.3f;
        [Tooltip("지속 시간(초). 다시 처치하면 갱신된다.")]
        [Min(0f)][SerializeField] float _duration = 5f;

        public override void OnKill(Unit unit, IMonster target)
        {
            if (_attackSpeedRatio <= 0f || _duration <= 0f) return;

            unit.Modifiers.SetTimed(unit, this, 0f, _attackSpeedRatio, _duration);
            NotifyVisual(SkillVisualKind.Frenzy, unit);
        }
    }
}
