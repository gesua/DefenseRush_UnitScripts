using UnityEngine;

namespace Defense.Units
{
    /// <summary>
    /// 창처럼 양손으로 쥐는 무기를 왼손도 쥐게 한다. 오른손 모션은 그대로 두고, 왼손만 IK로 무기의 손잡이 위치(_leftGripTarget)에 붙인다.
    /// 왼손 손목(손 본)이 _leftGripTarget의 위치와 회전을 따라가므로, 무기 자식 오브젝트를 옮기고 돌려서 쥐는 자세를 맞춘다.
    /// 무기는 오른손으로 쥔 위치(_rightGripPivot)를 축으로 돌아가서, 모션이 정한 왼손 위치 쪽을 향한다. 원본 모션이 두 손으로 같은 무기를 든 모션이라
    /// 자루가 항상 두 손을 지나게 되어, 왼팔이 닿지 않는 일이 크게 줄어든다.
    /// 그래도 팔이 닿지 않는 구간에서는 왼손이 자루를 따라 미끄러져 닿는 곳을 쥔다. 실제 창술에서 앞손이 자루 위를 미끄러지는 것과 같은 모양이다.
    /// OnAnimatorIK는 Animator가 붙은 오브젝트(Model)에서만 받을 수 있어서 Animator와 같은 오브젝트에 둔다.
    /// Animator Controller 레이어의 IK Pass가 켜져 있어야 동작한다.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class UnitHandIK : MonoBehaviour
    {
        [Tooltip("양손으로 쥘 무기. Y축이 창끝 방향이어야 한다. 왼손이 미끄러지는 방향(자루)을 정하는 데 쓴다.")]
        [SerializeField] Transform _weapon;
        [Tooltip("오른손으로 쥔 위치(손바닥 중심). 무기의 자식 오브젝트로 자루 위에 두고, 무기는 이 점을 축으로 돌아간다.")]
        [SerializeField] Transform _rightGripPivot;
        [Tooltip("왼손이 붙을 기준 위치. 무기의 자식 오브젝트로 두고, 왼손 손목(손 본)이 이 위치와 회전을 따라간다.")]
        [SerializeField] Transform _leftGripTarget;
        [Tooltip("모션이 정한 왼손 위치 쪽으로 무기를 돌리는 정도. 0이면 오른손에 쥔 각도 그대로(무기가 돌지 않음), 1이면 자루가 항상 왼손을 향한다.")]
        [Range(0f, 1f)]
        [SerializeField] float _aimWeight = 1f;
        [Tooltip("팔을 최대 길이의 몇 %까지 뻗게 할지. 기준 위치가 이 범위를 벗어나면 자루를 따라 닿는 곳으로 미끄러진다. 낮출수록 팔을 덜 뻗는다.")]
        [Range(0.5f, 1f)]
        [SerializeField] float _reachRatio = 0.95f;
        [Tooltip("왼손이 오른손으로 쥔 위치(_rightGripPivot)보다 창끝 쪽으로 최소 이만큼(월드 m)은 떨어지게 한다. 두 손이 겹치는 것을 막는다.")]
        [SerializeField] float _minGripDistance = 0.2f;
        [Range(0f, 1f)]
        [SerializeField] float _weight = 1f;

        Animator _animator;
        Transform _leftHand;
        Transform _leftUpperArm;
        float _leftArmLength; // 어깨(위팔)에서 손까지 팔을 편 길이
        Vector3 _weaponRestLocalPosition; // 오른손에 쥐여 둔 기본 자세
        Quaternion _weaponRestLocalRotation;

        void Awake()
        {
            TryGetComponent(out _animator);
            if (_weapon != null)
            {
                _weaponRestLocalPosition = _weapon.localPosition;
                _weaponRestLocalRotation = _weapon.localRotation;
            }
            if (_animator == null) return;

            _leftHand = _animator.GetBoneTransform(HumanBodyBones.LeftHand);
            _leftUpperArm = _animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            Transform lowerArm = _animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            if (_leftHand == null || _leftUpperArm == null || lowerArm == null) return;

            _leftArmLength = Vector3.Distance(_leftUpperArm.position, lowerArm.position) + Vector3.Distance(lowerArm.position, _leftHand.position);
        }

        void OnAnimatorIK(int layerIndex)
        {
            if (_animator == null || _leftHand == null || _leftUpperArm == null) return;
            if (_weapon == null || _rightGripPivot == null || _leftGripTarget == null) return;
            if (_leftGripTarget.gameObject.activeInHierarchy == false) return;

            AimWeapon();

            _animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, _weight);
            _animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, _weight);
            _animator.SetIKPosition(AvatarIKGoal.LeftHand, SlideToReach(_leftGripTarget.position));
            _animator.SetIKRotation(AvatarIKGoal.LeftHand, _leftGripTarget.rotation);
        }

        // 오른손으로 쥔 위치를 축으로 무기를 돌려서, 왼손 기준 위치가 모션이 정한 왼손 방향에 오게 한다.
        void AimWeapon()
        {
            // 매 프레임 쥐여 둔 기본 자세에서 시작해서 회전이 쌓이지 않게 한다.
            _weapon.localPosition = _weaponRestLocalPosition;
            _weapon.localRotation = _weaponRestLocalRotation;

            Vector3 pivot = _rightGripPivot.position;
            Vector3 toTarget = _leftGripTarget.position - pivot;
            Vector3 toHand = _leftHand.position - pivot;
            if (toTarget.sqrMagnitude < Mathf.Epsilon || toHand.sqrMagnitude < Mathf.Epsilon) return;

            Quaternion aim = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(toTarget, toHand), _aimWeight);
            _weapon.position = pivot + (aim * (_weapon.position - pivot));
            _weapon.rotation = aim * _weapon.rotation;
        }

        // 기준 위치가 팔이 닿는 범위(_reachRatio * 팔 길이) 안이면 그대로 쓰고,
        // 밖이면 자루 방향으로 가장 조금만 움직여서 닿는 곳을 찾는다.
        Vector3 SlideToReach(Vector3 position)
        {
            Vector3 axis = _weapon.up;
            float reach = _leftArmLength * _reachRatio;
            Vector3 toTarget = position - _leftUpperArm.position;
            float excess = toTarget.sqrMagnitude - (reach * reach);
            if (excess <= 0f) return position;

            // 자루 직선과 어깨 중심 구(반지름 reach)가 만나는 두 지점 중 기준 위치에 가까운 쪽으로 간다.
            // 만나지 않으면(자루가 팔이 닿는 범위 밖) 어깨에서 가장 가까운 지점으로 간다.
            float along = Vector3.Dot(toTarget, axis);
            float discriminant = (along * along) - excess;
            float slide = -along;
            if (discriminant >= 0f)
            {
                float half = Mathf.Sqrt(discriminant);
                float near = -along - half;
                float far = -along + half;
                slide = (Mathf.Abs(near) < Mathf.Abs(far)) ? near : far;
            }

            // 오른손으로 쥔 위치와 너무 가까워지지 않게 한다.
            float current = Vector3.Dot(position - _rightGripPivot.position, axis);
            slide = Mathf.Max(slide, _minGripDistance - current);

            return position + (axis * slide);
        }
    }
}