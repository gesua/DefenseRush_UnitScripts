using UnityEngine;

namespace Defense.Units
{
    /// <summary>
    /// 손에 들고 있는 물건(독약병, 화살 등)을 투사체가 나가는 순간 숨긴다.
    /// 투사체가 손을 떠났는데 같은 물건이 손에 계속 붙어 있으면 어색해 보이기 때문이다.
    /// 평소에 보이는지는 <see cref="Mode"/>로 정하고, 공격 모션이 시작되면 보이는 것은 두 방식이 같다.
    /// Unit이 붙은 오브젝트(UnitAnimator와 같은 곳)에 붙이고, 숨길 물건을 연결한다.
    /// </summary>
    [RequireComponent(typeof(Unit))]
    public class UnitHeldItemVisibility : MonoBehaviour
    {
        /// <summary>손에 든 물건이 평소에 보이는지에 따른 방식.</summary>
        public enum HeldItemMode
        {
            /// <summary>평소(Idle)에는 손에 들고 있다. 투사체가 나가면 숨기고, Idle로 돌아오거나 다음 공격 모션이 시작되면 다시 보인다. (독약병)</summary>
            VisibleAtIdle,
            /// <summary>평소에는 보이지 않는다. 공격 모션이 시작되면 보이고, 투사체가 나가거나 공격이 끊기면 다시 숨긴다. (화살)</summary>
            VisibleWhileAttacking,
        }

        // UnitAnimator의 Idle 상태와 같은 이름이다. 컨트롤러의 상태 이름을 바꾸면 여기도 같이 바꾼다.
        static readonly int IdleState = Animator.StringToHash("Idle");

        [Tooltip("투사체가 나갈 때 숨길, 손에 들고 있는 물건. (예: Hand_R 아래의 PoisonFlask)")]
        [SerializeField] GameObject _heldItem;
        [Tooltip("VisibleAtIdle: 평소에 손에 들고 있는 물건 (독약병). VisibleWhileAttacking: 공격 모션 중에만 보이는 물건 (화살).")]
        [SerializeField] HeldItemMode _mode = HeldItemMode.VisibleAtIdle;
        [Tooltip("비워두면 자식에서 찾는다.")]
        [SerializeField] Animator _animator;

        Unit _unit;
        bool _isHeldItemVisible = true;

        bool IsVisibleAtIdle => _mode == HeldItemMode.VisibleAtIdle;

        void Awake()
        {
            TryGetComponent(out _unit);
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
        }

        void OnEnable()
        {
            _unit.Attacked += OnAttacked;
            _unit.Released += OnReleased;
            _unit.AttackCanceled += OnAttackCanceled;
            _unit.MovingChanged += OnMovingChanged;

            // 풀에서 다시 켜질 때 이전 수명의 상태가 남지 않게, 방식에 맞는 평소 상태로 되돌린다.
            SetHeldItemVisible(IsVisibleAtIdle);
        }

        void OnDisable()
        {
            _unit.Attacked -= OnAttacked;
            _unit.Released -= OnReleased;
            _unit.AttackCanceled -= OnAttackCanceled;
            _unit.MovingChanged -= OnMovingChanged;
        }

        void Update()
        {
            // 평소에 보이는 물건만, 숨겨져 있는 동안 Idle로 돌아왔는지 확인한다.
            if (IsVisibleAtIdle == false || _isHeldItemVisible) return;

            if (IsIdle()) SetHeldItemVisible(true);
        }

        // 공격 모션이 시작되면 물건을 보여준다. 평소에 들고 있는 물건은 공격이 잦아 Idle까지 돌아오지 못해도 빈손으로 던지지 않게 된다.
        void OnAttacked(Unit unit, IMonster target) => SetHeldItemVisible(true);

        // 투사체가 나가는 순간 손에 든 물건을 숨긴다.
        void OnReleased(Unit unit, IMonster target) => SetHeldItemVisible(false);

        void OnAttackCanceled(Unit unit) => HideIfOnlyVisibleWhileAttacking();

        void OnMovingChanged(bool isMoving)
        {
            if (isMoving) HideIfOnlyVisibleWhileAttacking();
        }

        // 공격이 취소되거나 이동을 시작하면 공격 모션이 끊겨 투사체가 나가지 않는다. 공격 모션 중에만 보이는 물건은 이때 다시 숨긴다.
        void HideIfOnlyVisibleWhileAttacking()
        {
            if (IsVisibleAtIdle == false) SetHeldItemVisible(false);
        }

        // 전환 중이 아니고 Idle 상태일 때만 Idle로 돌아온 것으로 본다. (Attack → Idle 블렌드가 끝난 뒤)
        bool IsIdle()
        {
            // 애니메이터가 없으면 기다릴 이유가 없으므로 바로 다시 보여준다.
            if (_animator == null) return true;
            if (_animator.IsInTransition(0)) return false;

            return _animator.GetCurrentAnimatorStateInfo(0).shortNameHash == IdleState;
        }

        void SetHeldItemVisible(bool isVisible)
        {
            _isHeldItemVisible = isVisible;

            if (_heldItem != null) _heldItem.SetActive(isVisible);
        }
    }
}