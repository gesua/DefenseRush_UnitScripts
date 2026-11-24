using UnityEngine;
using UnityEngine.Pool;

namespace Defense.Units
{
    /// <summary>
    /// 유닛이 쏘는 투사체. 대상을 따라가며 날아가고, 도착하면 대미지를 준 뒤 풀로 돌아간다.
    /// 프리팹에는 이 컴포넌트와 모델만 있으면 된다. (이동은 이 스크립트가 처리하므로 Rigidbody/Collider는 필요 없다)
    /// 모델의 앞쪽(+Z)이 날아가는 방향을 향하도록 만들어야 한다.
    /// 기본은 직선 비행이고, 프리팹에서 곡선 높이와 회전 속도를 지정하면 포물선으로 돌면서 날아간다. (독약병 등 던지는 투사체용)
    /// </summary>
    public class Projectile : MonoBehaviour
    {
        [Tooltip("날아가는 속도 (월드 단위/초)")]
        [Min(0.1f)][SerializeField] float _speed = 20f;
        [Tooltip("몬스터 위치(발밑)에서 이만큼 위를 겨냥한다. 적중 이펙트 높이와 맞추면 자연스럽다.")]
        [SerializeField] Vector3 _aimOffset = new Vector3(0f, 0.65f, 0f);

        [Header("곡선 비행 (기본값이면 직선)")]
        [Tooltip("발사 지점~목표 거리에 대한 포물선 꼭대기 높이 비율. 0이면 직선으로 날아간다. 0.3~0.4 정도면 자연스럽게 던지는 느낌이 난다.")]
        [Min(0f)][SerializeField] float _arcHeightRatio = 0f;
        [Tooltip("포물선 꼭대기 높이의 상한 (월드 단위). 먼 거리에서 너무 높이 솟지 않게 제한한다.")]
        [Min(0f)][SerializeField] float _maxArcHeight = 2.5f;

        [Header("회전")]
        [Tooltip("날아가는 동안 도는 속도 (도/초, 진행 방향 기준 로컬 X/Y/Z 축). 모두 0이면 돌지 않는다. 병은 X축으로 돌리면 앞구르기처럼 돈다.")]
        [SerializeField] Vector3 _spinSpeed = Vector3.zero;

        Unit _owner;
        int _ownerLease;
        IMonster _target;
        float _damage;
        SkillDamageKind _damageKind;
        Vector3 _aimPoint;
        bool _targetLost;
        bool _flying;

        Vector3 _startPosition; // 발사 지점. 곡선 비행의 기준선이 시작되는 곳이다.
        float _traveled;        // 곡선 비행에서 지금까지 나아간 거리
        float _progress;        // 곡선 비행 진행도 (0~1)
        float _flightTime;      // 발사 후 경과 시간. 회전 각도를 계산하는 데 쓴다.
        Quaternion _lookRotation = Quaternion.identity; // 진행 방향을 바라보는 회전 (회전 연출을 곱하기 전)

        TrailRenderer[] _trails;
        IObjectPool<Projectile> _pool;

        /// <summary>쏜 유닛이 속한 씬. 씬이 내려갈 때 그 씬의 투사체를 회수하는 기준이다.</summary>
        internal int SceneHandle { get; private set; }

        void Awake()
        {
            _trails = GetComponentsInChildren<TrailRenderer>(true);
        }

        /// <summary>돌아갈 풀을 지정한다. 풀이 만들 때 한 번만 호출한다.</summary>
        internal void SetPool(IObjectPool<Projectile> pool) => _pool = pool;

        /// <summary>해당 위치에서 대상을 향해 날아가기 시작한다.</summary>
        internal void Launch(Vector3 position, Unit owner, IMonster target, float damage, SkillDamageKind damageKind)
        {
            _owner = owner;
            _ownerLease = owner.Lease;
            _target = target;
            _damage = damage;
            _damageKind = damageKind;
            _targetLost = false;
            _aimPoint = target.Position + _aimOffset;
            _flying = true;
            SceneHandle = owner.SourceSceneHandle;

            // 재사용되는 투사체이므로 이전 비행의 진행 상태를 비운다.
            _startPosition = position;
            _traveled = 0f;
            _progress = 0f;
            _flightTime = 0f;
            _lookRotation = Quaternion.identity;

            transform.position = position;

            // 켜기 전에 첫 진행 방향을 미리 바라보게 한다.
            // 풀에서 꺼낸 투사체는 지난 비행이 끝날 때의 회전을, 새로 만든 투사체는 프리팹의 회전을 그대로 갖고 있어서
            // 이대로 켜면 첫 Update가 돌기 전 한 프레임 동안 엉뚱한 방향(하늘, 뒤쪽)으로 그려진다.
            // (애니메이션 이벤트에서 쏘면 그 프레임의 Update 이후에 켜지므로 특히 눈에 띈다)
            ApplyRotation(GetLaunchDirection());
            gameObject.SetActive(true);

            // 재사용할 때 이전 위치에서 새 위치까지 꼬리 선이 길게 늘어나지 않게 한다.
            for (int i = 0; i < _trails.Length; i++)
            {
                _trails[i].Clear();
            }
        }

        void Update()
        {
            if (_flying == false) return;

            // 대상이 죽으면 더 따라가지 않고, 마지막으로 본 위치까지만 날아간다.
            if (_targetLost == false && _target.IsDead) _targetLost = true;
            if (_targetLost == false) _aimPoint = _target.Position + _aimOffset;

            _flightTime += Time.deltaTime;

            if (_arcHeightRatio > 0f) MoveArc();
            else MoveStraight();
        }

        // 조준점을 향해 일직선으로 날아간다. (화살 등)
        void MoveStraight()
        {
            ApplyRotation(_aimPoint - transform.position);

            transform.position = Vector3.MoveTowards(transform.position, _aimPoint, _speed * Time.deltaTime);
            if ((transform.position - _aimPoint).sqrMagnitude < 0.0001f) Arrive();
        }

        // 발사 지점에서 조준점까지 포물선을 그리며 날아간다. 대상이 움직이면 조준점이 바뀌므로 매 프레임 곡선을 다시 계산한다.
        void MoveArc()
        {
            float distance = Mathf.Max(0.01f, Vector3.Distance(_startPosition, _aimPoint));

            // 대상이 멀어져 전체 거리가 늘어나도 투사체가 뒤로 밀려나지 않도록 진행도는 줄어들지 않게 한다.
            _traveled += _speed * Time.deltaTime;
            _progress = Mathf.Max(_progress, Mathf.Clamp01(_traveled / distance));

            // 시작과 끝에서 0, 가운데에서 최대가 되는 포물선 높이
            float height = GetArcHeight(distance);
            float arcOffset = height * 4f * _progress * (1f - _progress);
            Vector3 next = Vector3.Lerp(_startPosition, _aimPoint, _progress) + Vector3.up * arcOffset;

            // 곡선의 접선 방향(실제로 이동하는 방향)을 바라보게 한다.
            ApplyRotation(next - transform.position);
            transform.position = next;

            if (_progress >= 1f) Arrive();
        }

        // 포물선 꼭대기 높이. 거리에 비례하되 상한을 넘지 않는다.
        float GetArcHeight(float distance) => Mathf.Min(distance * _arcHeightRatio, _maxArcHeight);

        // 발사 직후의 진행 방향. 직선이면 조준점 방향, 곡선이면 포물선 시작점의 접선 방향이다.
        Vector3 GetLaunchDirection()
        {
            Vector3 toAim = _aimPoint - _startPosition;
            if (_arcHeightRatio <= 0f) return toAim;

            // MoveArc의 높이 식(height * 4 * p * (1 - p))을 진행도 p로 미분하면 시작점(p = 0)에서 위쪽 기울기가 height * 4가 된다.
            float distance = Mathf.Max(0.01f, toAim.magnitude);
            return toAim + Vector3.up * (GetArcHeight(distance) * 4f);
        }

        // 진행 방향을 바라본 뒤 그 위에 회전 연출을 더한다. 방향이 거의 없으면 직전 방향을 유지한다.
        void ApplyRotation(Vector3 direction)
        {
            if (direction.sqrMagnitude > 0.0001f) _lookRotation = Quaternion.LookRotation(direction);

            transform.rotation = _lookRotation * Quaternion.Euler(_spinSpeed * _flightTime);
        }

        void Arrive()
        {
            _flying = false;

            // 도착하기 전에 죽은 대상에게는 대미지도 적중 연출도 없다.
            // 쏜 유닛이 그 사이에 팔리거나 합쳐져도 이미 날아간 투사체의 대미지는 들어간다.
            if (_targetLost == false)
            {
                // 쏜 유닛이 그 사이 풀로 돌아갔거나 다른 유닛으로 재사용됐으면 (판매, 승급) 대미지만 주고 처치 효과는 없다.
                if (_owner.Lease == _ownerLease) _owner.ApplyHit(_target, _damage, _damageKind);
                else MonsterSkillDamage.Apply(_target, _damage, _damageKind);
            }

            Release();
        }

        /// <summary>날아가는 중에 대미지 없이 풀로 돌려보낸다. (씬이 내려갈 때)</summary>
        internal void Cancel()
        {
            _flying = false;
            Release();
        }

        // 풀에 보관되는 동안 유닛/몬스터 참조를 붙잡고 있지 않도록 비우고 돌려보낸다.
        void Release()
        {
            _owner = null;
            _target = null;

            if (_pool != null) _pool.Release(this);
            else Destroy(gameObject);
        }
    }
}