using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Defense.Units
{
    /// <summary>Unit의 공격 이벤트를 받아 Animator 트리거를 건다. 이동 상태도 받아 IsMoving 값을 걸고, 공격이 취소되면 재생 중인 공격 모션을 끊는다.</summary>
    [RequireComponent(typeof(Unit))]
    public class UnitAnimator : MonoBehaviour
    {
        static readonly int AttackTrigger = Animator.StringToHash("Attack");
        static readonly int AttackSpeedParam = Animator.StringToHash("AttackSpeed");
        static readonly int IsMovingParam = Animator.StringToHash("IsMoving");
        static readonly int IdleState = Animator.StringToHash("Idle");
        static readonly int AttackState = Animator.StringToHash("Attack");

        // UnitBase.controller의 Attack → Idle 전환 설정과 같은 값이다. 컨트롤러의 그 전환을 바꾸면 여기도 같이 바꾼다.
        const float ATTACK_EXIT_TIME = 0.7115385f;
        const float ATTACK_EXIT_BLEND_TIME = 0.25f;
        // 공격 속도가 아주 빨라 블렌드 시간만으로 공격 간격이 모자랄 때도, 모션이 공격 간격의 이 비율만큼은 재생되게 한다.
        const float MIN_ATTACK_PLAY_RATIO = 0.5f;

        static readonly List<AnimatorClipInfo> ClipInfoBuffer = new List<AnimatorClipInfo>();

        [SerializeField] Animator _animator;
        Unit _unit;
        float _attackClipLength;
        Coroutine _readClipRoutine;

        void Awake()
        {
            TryGetComponent(out _unit);
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
        }

        void OnEnable()
        {
            _unit.Attacked += OnAttacked;
            _unit.AttackCanceled += OnAttackCanceled;
            _unit.MovingChanged += OnMovingChanged;
            ResetParameters();
        }

        void OnDisable()
        {
            _unit.Attacked -= OnAttacked;
            _unit.AttackCanceled -= OnAttackCanceled;
            _unit.MovingChanged -= OnMovingChanged;
            _readClipRoutine = null;
        }

        // 풀에서 다시 켜질 때 이전 수명의 이동 상태와 대기 중인 공격 트리거가 남지 않게 한다.
        void ResetParameters()
        {
            if (_animator == null) return;

            _animator.SetBool(IsMovingParam, false);
            _animator.ResetTrigger(AttackTrigger);
        }

        void OnMovingChanged(bool isMoving)
        {
            if (_animator == null) return;
            _animator.SetBool(IsMovingParam, isMoving);
        }

        void OnAttacked(Unit u, IMonster target)
        {
            if (_animator == null) return;
            // 클립이 길어서 공격 간격 안에 모션이 끝나지 않는 유닛은 재생 속도를 올린다. 배율은 클립 길이에서 자동으로 계산한다.
            ApplyAttackSpeed(u);
            _animator.SetTrigger(AttackTrigger);

            // 클립 길이는 Attack 상태에서 실제로 재생되는 클립에서 읽는다. 아직 못 읽었다면 모션이 시작된 뒤에 읽는다.
            if (_attackClipLength <= 0f && _readClipRoutine == null)
                _readClipRoutine = StartCoroutine(ReadAttackClipLength(u));
        }

        void ApplyAttackSpeed(Unit u)
        {
            _animator.SetFloat(AttackSpeedParam, u.CombatStats.AttackSpeed * GetAttackAnimationSpeedScale(u));
        }

        // 컨트롤러는 Attack을 ATTACK_EXIT_TIME(정규화 시간)에 Idle로 내보내고, 그 뒤 ATTACK_EXIT_BLEND_TIME 동안 블렌드한다.
        // 이 구간이 공격 간격 안에 끝나야 다음 공격 모션이 밀리지 않으므로, 그에 필요한 배율을 구한다.
        float GetAttackAnimationSpeedScale(Unit u)
        {
            float attackSpeed = u.CombatStats.AttackSpeed;
            if (_attackClipLength <= 0f || attackSpeed <= 0f) return 1f;

            float interval = u.CombatStats.AttackInterval;
            float playTime = Mathf.Max(interval - ATTACK_EXIT_BLEND_TIME, interval * MIN_ATTACK_PLAY_RATIO);
            float autoScale = (ATTACK_EXIT_TIME * _attackClipLength) / (attackSpeed * playTime);
            return Mathf.Max(1f, autoScale);
        }

        // Attack 상태로 들어가면 재생되는 클립의 길이를 한 번만 읽어 둔다. 오버라이드 컨트롤러로 바뀐 클립도 그대로 읽힌다.
        IEnumerator ReadAttackClipLength(Unit u)
        {
            while (TryReadAttackClipLength(out _attackClipLength) == false)
                yield return null;

            // 속도 파라미터는 재생 중에도 매 프레임 반영되므로, 이번 공격에도 바로 적용한다.
            ApplyAttackSpeed(u);
            _readClipRoutine = null;
        }

        bool TryReadAttackClipLength(out float length)
        {
            length = 0f;

            // 전환 중에는 Attack이 도착 상태이므로 Next 쪽 클립 정보를 본다.
            bool isNextState = _animator.IsInTransition(0) && (_animator.GetNextAnimatorStateInfo(0).shortNameHash == AttackState);
            if (isNextState == false && _animator.GetCurrentAnimatorStateInfo(0).shortNameHash != AttackState) return false;

            if (isNextState) _animator.GetNextAnimatorClipInfo(0, ClipInfoBuffer);
            else _animator.GetCurrentAnimatorClipInfo(0, ClipInfoBuffer);
            if (ClipInfoBuffer.Count == 0) return false;

            length = ClipInfoBuffer[0].clip.length;
            return length > 0f;
        }

        // 발사하기 전에 공격할 적이 없어지면 남은 공격 모션을 끝까지 재생하지 않고 Idle로 돌아간다.
        void OnAttackCanceled(Unit u)
        {
            if (_animator == null) return;
            _animator.ResetTrigger(AttackTrigger);
            _animator.CrossFadeInFixedTime(IdleState, Unit.ATTACK_CANCEL_BLEND_TIME);
        }
    }
}