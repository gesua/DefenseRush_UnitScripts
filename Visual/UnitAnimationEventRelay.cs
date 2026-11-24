using UnityEngine;

namespace Defense.Units
{
    /// <summary>
    /// Animator가 붙은 오브젝트(보통 모델)에 붙여서, 공격 클립의 애니메이션 이벤트를 Unit에 전달한다.
    /// 애니메이션 이벤트는 Animator와 같은 오브젝트의 컴포넌트에서만 받을 수 있어서 Unit이 직접 받지 못한다.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class UnitAnimationEventRelay : MonoBehaviour
    {
        Unit _unit;

        void Awake()
        {
            _unit = GetComponentInParent<Unit>();
        }

        /// <summary>공격 클립의 발사 프레임에 넣는 이벤트. 클립 이벤트의 Function에 이 이름을 그대로 적는다.</summary>
        public void OnAttackRelease()
        {
            if (_unit != null) _unit.ReleaseAttack();
        }
    }
}
