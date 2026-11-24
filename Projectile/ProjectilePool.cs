using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.SceneManagement;

namespace Defense.Units
{
    /// <summary>
    /// 투사체 프리팹별로 인스턴스를 재사용하는 풀. 공격마다 생성/파괴하지 않기 위해 쓴다.
    /// 씬에 따로 배치할 필요 없이 처음 쏠 때 만들어지고, DontDestroyOnLoad로 씬이 바뀌어도 유지된다.
    /// 씬이 내려갈 때는 그 씬에서 쏜 날아가는 중인 투사체를 대미지 없이 풀로 돌려보낸다.
    /// </summary>
    [DisallowMultipleComponent]
    public class ProjectilePool : MonoBehaviour
    {
        const int DEFAULT_CAPACITY = 16;
        const int MAX_SIZE = 64;

        public static ProjectilePool Instance { get; private set; }

        readonly Dictionary<Projectile, IObjectPool<Projectile>> _pools = new Dictionary<Projectile, IObjectPool<Projectile>>();
        readonly List<Projectile> _active = new List<Projectile>(); // 날아가는 중인 투사체

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetInstance() => Instance = null;

        /// <summary>풀이 없으면 만들어서 돌려준다. 편집 모드에서는 만들지 않고 null.</summary>
        public static ProjectilePool GetOrCreate()
        {
            if (Application.isPlaying == false) return null;

            if (Instance == null) new GameObject("ProjectilePool").AddComponent<ProjectilePool>();
            return Instance;
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneUnloaded += HandleSceneUnloaded;
        }

        void OnDestroy()
        {
            SceneManager.sceneUnloaded -= HandleSceneUnloaded;
            if (Instance == this) Instance = null;
        }

        /// <summary>prefab을 position에서 target을 향해 쏜다. 도착하면 owner를 통해 대미지가 들어간다.</summary>
        public void Fire(Projectile prefab, Vector3 position, Unit owner, IMonster target, float damage, SkillDamageKind damageKind)
        {
            if (prefab == null) return;

            Projectile projectile = GetPool(prefab).Get();
            _active.Add(projectile);
            projectile.Launch(position, owner, target, damage, damageKind);
        }

        IObjectPool<Projectile> GetPool(Projectile prefab)
        {
            if (_pools.TryGetValue(prefab, out IObjectPool<Projectile> pool)) return pool;

            ObjectPool<Projectile> created = null;
            created = new ObjectPool<Projectile>(
                createFunc: () => CreateProjectile(prefab, created),
                actionOnRelease: OnReleased,
                actionOnDestroy: projectile => { if (projectile != null) Destroy(projectile.gameObject); },
                defaultCapacity: DEFAULT_CAPACITY,
                maxSize: MAX_SIZE);

            _pools[prefab] = created;
            return created;
        }

        void OnReleased(Projectile projectile)
        {
            _active.Remove(projectile);
            projectile.gameObject.SetActive(false);
        }

        // 내려가는 씬에서 쏜 투사체는 대상(몬스터)도 함께 사라지므로 회수한다. 회수하면 _active에서 스스로 빠진다.
        void HandleSceneUnloaded(Scene scene)
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                if (_active[i].SceneHandle == scene.handle) _active[i].Cancel();
            }
        }

        // 쏘기 전까지는 꺼 둔다. 켜는 것은 Launch에서 위치를 정한 뒤에 한다.
        Projectile CreateProjectile(Projectile prefab, IObjectPool<Projectile> pool)
        {
            Projectile projectile = Instantiate(prefab, transform);
            projectile.SetPool(pool);
            projectile.gameObject.SetActive(false);
            return projectile;
        }
    }
}