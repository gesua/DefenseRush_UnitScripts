using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace Defense.Units
{
    /// <summary>
    /// 필드 위의 플레이어 유닛 1마리.
    /// 칸 이동/재배치와, 사거리 안의 몬스터를 자동으로 공격하는 것을 담당한다.
    /// </summary>
    public class Unit : MonoBehaviour
    {
        [SerializeField] float _moveSpeed = 12f;          // 칸 이동/재배치 속도 (월드 단위/초)
        [SerializeField] float _retargetInterval = 0.15f; // 타겟을 다시 고르는 주기(초)
        [SerializeField] float _moveAnimMinDistance = 1f; // 이 거리 이상 이동할 때만 이동 애니메이션을 켠다 (칸 안에서 자리 잡는 짧은 이동은 제외)
        [SerializeField] Transform _firePoint;            // 투사체가 나가는 위치(활/석궁 끝 등). 비워두면 유닛 위치에서 나간다
        [Min(1f)][SerializeField] float _turnSpeed = 540f; // 타겟 쪽으로 몸을 돌리는 속도 (도/초). 타겟이 바뀌어도 순간 회전하지 않고 이 속도로 부드럽게 돈다

        public UnitData Data { get; private set; }

        /// <summary>전투 재화를 지급하는 쪽. 없으면 null이고, 재화를 주는 스킬(약탈)은 아무 일도 하지 않는다.</summary>
        public ICurrencyReceiver CurrencyReceiver { get; private set; }

        /// <summary>이 유닛이 공격할 적을 찾는 몬스터 목록. 없으면 null이고, 주변 적이 필요한 처치 스킬(시체 폭발)은 아무 일도 하지 않는다.</summary>
        public IMonsterRegistry MonsterRegistry => _monsters;

        /// <summary>기본 스탯 + 로비/인게임 강화가 반영된 현재 스탯. 인게임 강화는 즉시 반영된다.</summary>
        public UnitStats Stats => (_statProvider != null) ? _statProvider.GetStats(Data) : Data.BaseStats;

        /// <summary>Stats에 스킬이 건 보정(오라, 일시 버프, 광분)까지 반영한 실제 전투 스탯. 공격 간격과 공격 애니메이션 속도에 쓴다.</summary>
        public UnitStats CombatStats => Modifiers.Apply(Stats);

        /// <summary>이 유닛이 지금 놓여 있는 칸. 칸에 들어갈 때 Slot이 채우고, 빠지면 null이 된다.</summary>
        public Slot Slot { get; internal set; }

        /// <summary>이 유닛을 소환한 그리드가 속한 씬의 핸들. 유닛은 DontDestroyOnLoad 아래의 풀에 있어서 gameObject.scene으로는 출처 씬을 알 수 없다.</summary>
        public int SourceSceneHandle { get; internal set; } = -1;

        /// <summary>풀로 돌아갈 때마다 올라가는 수명 번호. 투사체가 쏜 유닛이 그 사이 풀로 돌아갔거나 다른 유닛으로 재사용됐는지 확인하는 데 쓴다.</summary>
        public int Lease { get; private set; }

        /// <summary>스킬이 이 유닛에 건 공격력/공격속도 보정 목록.</summary>
        public UnitStatModifiers Modifiers { get; } = new UnitStatModifiers();

        /// <summary>공격하는 순간 호출된다. (공격 애니메이션/이펙트 연결용)</summary>
        public event Action<Unit, IMonster> Attacked;

        /// <summary>공격 한 번의 효과(스킬·대미지·투사체)가 실제로 나가는 순간 호출된다. 즉시 공격은 Attacked와 같은 순간이고, 애니메이션 이벤트로 발사하는 유닛은 발사 프레임이다. (발사 이펙트 연결용)</summary>
        public event Action<Unit, IMonster> Released;

        /// <summary>발사하기 전에 공격할 적이 사라져 공격이 취소됐을 때 호출된다. (재생 중인 공격 모션을 끊는 연결용)</summary>
        public event Action<Unit> AttackCanceled;

        /// <summary>대미지가 들어가기 직전에 호출된다. 투사체를 쓰는 유닛은 투사체가 도착했을 때 호출된다. (적중 이펙트 연결용)</summary>
        public event Action<Unit, IMonster> Hit;

        /// <summary>이 유닛의 공격이나 스킬로 적이 죽은 직후 호출된다. 대상은 이미 죽었으므로 Position에 접근하지 않는다. (처치 이펙트/보상 연결용)</summary>
        public event Action<Unit, IMonster> Killed;

        /// <summary>칸 사이를 이동하는 중이면 true. (이동 애니메이션 연결용)</summary>
        public bool IsMoving { get; private set; }

        /// <summary>이동 애니메이션을 시작하거나 멈출 때 호출된다. (인자: 이동 중이면 true)</summary>
        public event Action<bool> MovingChanged;

        readonly AttackContext _attackContext = new AttackContext();

        IMonsterRegistry _monsters;
        IUnitStatProvider _statProvider;
        IMonster _target;
        float _nextAttackTime;
        float _nextRetargetTime;

        /// <summary>공격이 취소되어 모션이 Idle로 섞이는 시간(초). 이 시간 안에 들어오는 발사 이벤트는 취소된 모션의 낡은 이벤트로 보고 무시한다.</summary>
        public const float ATTACK_CANCEL_BLEND_TIME = 0.1f;

        // 애니메이션 이벤트로 발사하는 유닛의 대기 중인 공격. (공격은 시작했지만 아직 발사 프레임에 도달하지 않은 상태)
        IMonster _pendingTarget;
        float _pendingStartTime;
        float _pendingDeadline;
        bool _hasPendingAttack;

        Vector3 _targetPosition;
        Quaternion _homeRotation;
        bool _moving;

        // 사거리를 재는 기준점. 칸에 놓이면 칸 중심으로 지정된다. (지정 전에는 유닛 자신의 위치)
        Vector3 _rangeCenter;
        bool _hasRangeCenter;

        IObjectPool<Unit> _pool;

        /// <param name="monsterRegistry">null이면 공격하지 않는다.</param>
        /// <param name="stats">null이면 레벨 보너스 없이 기본 스탯을 쓴다.</param>
        /// <param name="currencyReceiver">null이면 코인을 주는 스킬이 작동하지 않는다. (약탈, 노획은 오류 로그를 남긴다)</param>
        public void Init(UnitData data, IMonsterRegistry monsterRegistry = null, IUnitStatProvider stats = null,
                         ICurrencyReceiver currencyReceiver = null)
        {
            Data = data;
            _monsters = monsterRegistry;
            _statProvider = stats;
            CurrencyReceiver = currencyReceiver;
            _homeRotation = transform.rotation; // 이동이 끝나면 이 방향으로 되돌린다

            RunSpawnSkills();
        }

        /// <summary>돌아갈 풀을 지정한다. 풀이 만들 때 한 번만 호출한다.</summary>
        internal void SetPool(IObjectPool<Unit> pool) => _pool = pool;

        /// <summary>유닛을 풀로 돌려보낸다. UnitPool.Release가 호출한다.</summary>
        internal void ReturnToPool()
        {
            if (_pool != null) _pool.Release(this);
            else Destroy(gameObject);
        }

        /// <summary>
        /// 풀로 돌아갈 때 호출한다. 다음에 다른 칸·다른 씬에서 다시 빌려가도 이전 수명의 상태와 씬 참조가 남지 않게 비운다.
        /// 다시 쓸 때는 Init이 새로 채운다.
        /// </summary>
        internal void ResetForPool()
        {
            Lease++;

            Data = null;
            _monsters = null;
            _statProvider = null;
            CurrencyReceiver = null;
            Slot = null;
            SourceSceneHandle = -1;

            _target = null;
            _nextAttackTime = 0f;
            _nextRetargetTime = 0f;
            CancelPendingAttack();

            _targetPosition = default;
            _homeRotation = default;
            _moving = false;
            IsMoving = false;

            _rangeCenter = default;
            _hasRangeCenter = false;

            Modifiers.Clear();
            _attackContext.Setup(null, null, null, 0f); // 마지막 공격의 유닛/몬스터/레지스트리 참조를 놓는다

            // 이전 수명의 구독자(연출, 애니메이터)가 다음 수명으로 이어지지 않게 비운다.
            Attacked = null;
            Released = null;
            AttackCanceled = null;
            Hit = null;
            Killed = null;
            MovingChanged = null;
        }

        // 같은 칸에 겹쳐 선 유닛이 모두 같은 사거리를 공유하도록, 칸에 놓인 유닛은 칸 중심에서 사거리를 잰다.
        public Vector3 RangeCenter => (_hasRangeCenter) ? _rangeCenter : transform.position;

        // 유물 등으로 늘어난 이동속도 배율. 없으면 1.
        float MoveSpeedMultiplier => (_statProvider != null) ? _statProvider.MoveSpeedMultiplier : 1f;

        /// <summary>유물 등으로 늘어난 스킬 피해 배율. 없으면 1.</summary>
        public float SkillDamageMultiplier => (_statProvider != null) ? _statProvider.SkillDamageMultiplier : 1f;

        /// <summary>유물 등으로 늘어난 보스 대미지 배율. 없으면 1.</summary>
        public float BossDamageMultiplier => (_statProvider != null) ? _statProvider.BossDamageMultiplier : 1f;

        /// <summary>
        /// 스킬이 발동했을 때 UnitSkill이 부른다. 유물의 스킬 골드 획득 확률로 코인 1개를 얻는다.
        /// 재화를 받는 쪽이 없으면 아무 일도 하지 않는다.
        /// </summary>
        internal void RollSkillGold()
        {
            if (CurrencyReceiver == null || _statProvider == null) return;

            float chance = _statProvider.SkillGoldChance;
            if (chance > 0f && UnityEngine.Random.value < chance) CurrencyReceiver.AddCurrency(1);
        }

        // 투사체가 나가는 위치. 발사 위치를 지정하지 않았으면 유닛 위치.
        Vector3 FirePosition => (_firePoint != null) ? _firePoint.position : transform.position;

        /// <summary>목표 위치로 이동한다. instant가 true면 즉시, 아니면 부드럽게 이동.</summary>
        public void MoveTo(Vector3 position, bool instant = false)
        {
            _targetPosition = position;
            if (instant)
            {
                transform.position = position;
                _moving = false;
                SetMoving(false);
            }
            else
            {
                _moving = true;
                SetMoving(Vector3.Distance(transform.position, position) >= _moveAnimMinDistance);
            }
        }

        /// <summary>사거리를 재는 기준점을 정한다. 칸이 유닛을 배치할 때마다 칸 중심으로 불러 준다.</summary>
        public void SetRangeCenter(Vector3 center)
        {
            _rangeCenter = center;
            _hasRangeCenter = true;
        }

        void Update()
        {
            UpdateMovement();
            UpdateCombat();
        }

        void UpdateMovement()
        {
            if (_moving == false) return;

            // 이동 애니메이션 중에는 가는 방향을 바라본다 (XZ 평면)
            Vector3 direction = _targetPosition - transform.position;
            direction.y = 0f;
            if (IsMoving && direction.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(direction);

            transform.position = Vector3.MoveTowards(transform.position, _targetPosition, _moveSpeed * MoveSpeedMultiplier * Time.deltaTime);
            if ((transform.position - _targetPosition).sqrMagnitude < 0.0001f)
            {
                transform.position = _targetPosition;
                _moving = false;
                if (IsMoving) transform.rotation = _homeRotation;
                SetMoving(false);
            }
        }

        // 값이 바뀔 때만 알린다.
        void SetMoving(bool value)
        {
            if (IsMoving == value) return;

            IsMoving = value;
            MovingChanged?.Invoke(value);
        }

        void UpdateCombat()
        {
            UpdatePendingAttack();

            // 칸 사이를 이동하는 동안에는 공격하지 않는다. (공격 주기는 계속 흐르므로 도착하면 바로 공격할 수 있다)
            if (Data == null || _monsters == null || _moving) return;

            UnitStats stats = CombatStats;

            // 타겟이 무효가 됐거나 갱신 주기가 되면 다시 고른다.
            if (IsValidTarget(_target, stats.AttackRange) == false || Time.time >= _nextRetargetTime)
            {
                _target = FindTarget(stats.AttackRange);
                _nextRetargetTime = Time.time + _retargetInterval;
            }
            if (_target == null) return;

            // 타겟 쪽을 바라본다 (XZ 평면). 이동 중에는 가는 방향을 유지한다.
            // 타겟이 죽어 다음 타겟으로 바뀌어도 순간적으로 돌지 않게 일정 속도로 회전한다.
            Vector3 toTarget = _target.Position - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 0.0001f)
            {
                Quaternion lookRotation = Quaternion.LookRotation(toTarget);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, lookRotation, _turnSpeed * Time.deltaTime);
            }

            if (Time.time < _nextAttackTime) return;

            _nextAttackTime = Time.time + stats.AttackInterval;
            Attacked?.Invoke(this, _target);

            // 애니메이션 이벤트로 발사하는 유닛은 여기서 공격만 시작하고, 스킬·대미지는 발사 프레임에 ReleaseAttack이 처리한다.
            if (Data.ReleaseOnAnimationEvent)
            {
                BeginPendingAttack(_target, stats.AttackInterval);
                return;
            }

            ResolveAttack(_target, stats.AttackPower);
        }

        /// <summary>
        /// 공격 한 번의 효과를 실제로 낸다. 스킬을 실행하고 최종 피해를 준다.
        /// 즉시 공격은 공격하는 순간, 애니메이션 이벤트로 발사하는 유닛은 발사 프레임에 호출된다.
        /// </summary>
        void ResolveAttack(IMonster target, float attackPower)
        {
            Released?.Invoke(this, target);
            float damage = RunAttackSkills(target, attackPower);
            // 스킬의 최종 피해와 표시 정보를 함께 전달한다. 일반 공격의 계산·공격 주기는 유지한다.
            // 투사체가 있는 유닛은 대미지가 투사체 도착 시점에 들어간다.
            DealDamage(target, damage, _attackContext.DamageKind);
        }

        // 공격을 시작만 해 두고 발사 프레임을 기다린다. 이벤트가 오지 않아도 공격이 사라지지 않게 다음 공격 시각을 발사 기한으로 둔다.
        void BeginPendingAttack(IMonster target, float interval)
        {
            _pendingTarget = target;
            _pendingStartTime = Time.time;
            _pendingDeadline = Time.time + interval;
            _hasPendingAttack = true;
        }

        void CancelPendingAttack()
        {
            _pendingTarget = null;
            _pendingStartTime = 0f;
            _pendingDeadline = 0f;
            _hasPendingAttack = false;
        }

        // 칸 사이를 이동하면 공격 애니메이션이 끊기므로 대기 중인 공격도 취소한다. 이벤트가 안 오면 기한에 맞춰 발사한다.
        void UpdatePendingAttack()
        {
            if (_hasPendingAttack == false) return;

            if (IsMoving)
            {
                CancelPendingAttack();
                return;
            }

            // 모션이 나오는 동안 타겟이 죽거나 사거리를 벗어나면 다른 적으로 바꾼다. 공격할 적이 없으면 모션을 취소한다.
            UnitStats stats = CombatStats;
            if (IsValidTarget(_pendingTarget, stats.AttackRange) == false)
            {
                _pendingTarget = FindTarget(stats.AttackRange);
                if (_pendingTarget == null)
                {
                    AbortPendingAttack();
                    return;
                }
            }

            if (Time.time >= _pendingDeadline) ReleaseAttack();
        }

        // 발사하기 전에 사거리 안에 공격할 적이 하나도 없어졌을 때: 공격을 없던 일로 하고(공격 쿨타임 반환) 모션을 취소하도록 알린다.
        void AbortPendingAttack()
        {
            CancelPendingAttack();
            _nextAttackTime = Time.time;
            AttackCanceled?.Invoke(this);
        }

        /// <summary>
        /// 공격 애니메이션의 발사 프레임(애니메이션 이벤트)에서 호출한다. 대기 중인 공격이 없으면 아무 일도 하지 않는다.
        /// </summary>
        public void ReleaseAttack()
        {
            if (_hasPendingAttack == false || Data == null || _monsters == null) return;

            // 공격이 취소된 직후 새 공격을 시작하면, Idle로 섞이는 중인 이전 클립의 이벤트가 들어올 수 있다.
            // 공격을 시작한 직후의 이벤트는 정상 발사 프레임일 수 없으므로 무시한다.
            if (Time.time - _pendingStartTime < ATTACK_CANCEL_BLEND_TIME) return;

            IMonster target = _pendingTarget;
            CancelPendingAttack();

            UnitStats stats = CombatStats;

            // 모션이 나오는 동안 타겟이 죽거나 사거리를 벗어났으면 지금 사거리 안에서 다시 고른다. 없으면 이번 공격은 취소한다.
            if (IsValidTarget(target, stats.AttackRange) == false) target = FindTarget(stats.AttackRange);
            if (target == null)
            {
                AbortPendingAttack();
                return;
            }

            ResolveAttack(target, stats.AttackPower);
        }

        /// <summary>
        /// 이 유닛의 공격 방식으로 대미지를 준다. UnitData에 투사체가 있으면 투사체를 날려 보내고, 없으면 즉시 준다.
        /// 추가 타격 같은 스킬도 MonsterSkillDamage 대신 이 함수로 주면 같은 투사체가 나간다.
        /// </summary>
        public void DealDamage(IMonster target, float damage, SkillDamageKind damageKind)
        {
            Projectile projectilePrefab = Data.ProjectilePrefab;
            ProjectilePool pool = (projectilePrefab != null) ? ProjectilePool.GetOrCreate() : null;
            if (pool == null)
            {
                ApplyHit(target, damage, damageKind);
                return;
            }

            pool.Fire(projectilePrefab, FirePosition, this, target, damage, damageKind);
        }

        /// <summary>적중 알림을 보낸 뒤 대미지를 실제로 적용한다. 즉시 공격과 투사체 도착이 모두 이 함수를 지난다.</summary>
        internal void ApplyHit(IMonster target, float damage, SkillDamageKind damageKind)
        {
            // 멀티샷처럼 같은 공격에서 먼저 들어간 타격으로 이미 죽은 대상에는 적중 알림도 대미지도 보내지 않는다.
            if (target == null || target.IsDead) return;

            Hit?.Invoke(this, target);
            MonsterSkillDamage.Apply(target, damage, damageKind, this);
        }

        /// <summary>이 유닛이 적을 처치했을 때 MonsterSkillDamage가 호출한다. 처치 알림을 보내고 스킬의 OnKill을 실행한다.</summary>
        internal void NotifyKill(IMonster target, Vector3 deathPosition)
        {
            // 투사체가 날아가는 사이 유닛이 팔리거나 승급해 사라졌으면 처치 효과는 없다.
            if (this == null || Data == null) return;

            Killed?.Invoke(this, target);

            IReadOnlyList<UnitSkill> skills = Data.Skills;
            for (int i = 0; i < skills.Count; i++)
            {
                if (skills[i] != null) skills[i].OnKill(this, target, deathPosition);
            }
        }

        // 새로 만들어진 직후 스킬의 OnSpawned를 호출한다. (소환, 승급)
        void RunSpawnSkills()
        {
            IReadOnlyList<UnitSkill> skills = Data.Skills;
            for (int i = 0; i < skills.Count; i++)
            {
                if (skills[i] != null) skills[i].OnSpawned(this);
            }
        }

        // 공격마다 스킬을 순서대로 실행하고, 스킬이 반영된 최종 대미지를 돌려준다.
        float RunAttackSkills(IMonster target, float baseDamage)
        {
            IReadOnlyList<UnitSkill> skills = Data.Skills;
            _attackContext.Setup(this, target, _monsters, baseDamage);
            for (int i = 0; i < skills.Count; i++)
            {
                if (skills[i] != null) skills[i].OnAttack(_attackContext);
            }
            return _attackContext.Damage;
        }

        bool IsValidTarget(IMonster monster, float range) =>
            monster != null && monster.IsDead == false && InRange(monster.Position, range);

        // 사거리는 XZ 평면 거리로 판정한다. (높이 차이 무시)
        bool InRange(Vector3 position, float range)
        {
            Vector3 center = RangeCenter;
            float dx = position.x - center.x;
            float dz = position.z - center.z;
            return dx * dx + dz * dz <= range * range;
        }

        // 사거리 안의 몬스터 중 경로 진행도가 가장 높은(출구에 가장 가까운) 몬스터.
        IMonster FindTarget(float range)
        {
            IMonster best = null;
            float bestProgress = float.NegativeInfinity;

            IReadOnlyList<IMonster> list = _monsters.ActiveMonsters;
            for (int i = 0; i < list.Count; i++)
            {
                IMonster monster = list[i];
                if (monster.IsDead || InRange(monster.Position, range) == false) continue;

                if (monster.PathProgress > bestProgress)
                {
                    best = monster;
                    bestProgress = monster.PathProgress;
                }
            }
            return best;
        }
    }
}