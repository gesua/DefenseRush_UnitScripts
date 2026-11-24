using UnityEngine;

namespace Defense.Units
{
    /// <summary>
    /// 공격 애니메이션의 StringPull 값(0~1)에 따라 활 시위(WB.string)를 원위치와 당김 위치 사이에서 움직인다.
    /// 0이면 시위가 원위치에 있고, 1이면 시위가 오른손(BowStringPullTarget)에 붙는다.
    /// </summary>
    public class BowStringController : MonoBehaviour
    {
        static readonly int StringPullParam = Animator.StringToHash("StringPull");

        [Tooltip("시위 본(WB.string)")]
        [SerializeField] Transform _string;
        [Tooltip("당길 때 시위가 따라갈 위치. 오른손(Hand_R) 하위에 둔다.")]
        [SerializeField] Transform _pullTarget;
        [Tooltip("비워두면 자식에서 찾는다. 활(Wooden Bow) 자체의 Animator가 아니라 캐릭터의 Animator여야 한다.")]
        [SerializeField] Animator _animator;

        Vector3 _restLocalPosition; // 시위의 원위치 (부모 기준 로컬)

        void Awake()
        {
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
            if (_string != null) _restLocalPosition = _string.localPosition;
        }

        // 애니메이션이 갱신된 뒤에 위치를 덮어써야 하므로 LateUpdate에서 처리한다.
        void LateUpdate()
        {
            if (_animator == null || _string == null || _pullTarget == null) return;

            float pull = Mathf.Clamp01(_animator.GetFloat(StringPullParam));
            Vector3 restPosition = _string.parent.TransformPoint(_restLocalPosition);
            _string.position = Vector3.Lerp(restPosition, _pullTarget.position, pull);
        }
    }
}