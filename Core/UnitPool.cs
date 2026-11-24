using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.SceneManagement;

namespace Defense.Units
{
    /// <summary>
    /// 유닛 종류(UnitData)별로 인스턴스를 재사용하는 풀. 소환·승급·판매·도박마다 생성/파괴하지 않기 위해 쓴다.
    /// 씬에 따로 배치할 필요 없이 처음 소환할 때 만들어지고, 종류별 풀도 그 종류가 처음 소환될 때 만들어진다.
    /// DontDestroyOnLoad로 씬이 바뀌어도 유지되므로, 로비에 다녀와서 다시 시작해도 이전 판의 유닛을 재사용한다.
    /// 씬이 내려갈 때는 그 씬에서 소환한 유닛을 풀로 돌려보낸다.
    /// </summary>
    [DisallowMultipleComponent]
    public class UnitPool : MonoBehaviour
    {
        // 한 종류당 보관할 최대 개수. 한 종류가 한꺼번에 이만큼 돌아와도 보관하고, 넘치는 개체만 파괴한다.
        const int MAX_SIZE = 9;

        public static UnitPool Instance { get; private set; }

        readonly Dictionary<UnitData, IObjectPool<Unit>> _pools = new Dictionary<UnitData, IObjectPool<Unit>>();
        readonly List<Unit> _active = new List<Unit>(); // 필드에 나가 있는 유닛

        Transform _storage;     // 풀에 보관 중인 유닛의 부모. 꺼 둔 채로 두어, 복제하는 동안 Awake/OnEnable이 돌지 않게 한다.
        Transform _activeRoot;  // 필드에 나가 있는 유닛의 부모. 씬 오브젝트가 아니므로 이전 씬과 함께 파괴되지 않는다.

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetInstance() => Instance = null;

        /// <summary>풀이 없으면 만들어서 돌려준다.</summary>
        public static UnitPool GetOrCreate()
        {
            if (Instance == null) new GameObject("UnitPool").AddComponent<UnitPool>();
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

            _storage = CreateRoot("InactiveUnits");
            _storage.gameObject.SetActive(false);
            _activeRoot = CreateRoot("ActiveUnits");

            SceneManager.sceneUnloaded += HandleSceneUnloaded;
        }

        void OnDestroy()
        {
            SceneManager.sceneUnloaded -= HandleSceneUnloaded;
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// data의 유닛을 position/rotation에 꺼내 켠다. 풀에 남은 개체가 없으면 새로 만든다.
        /// 스탯과 연결은 호출한 쪽이 이어서 Unit.Init으로 채운다. sourceSceneHandle은 유닛을 소환한 그리드가 속한 씬의 핸들이다.
        /// </summary>
        public Unit Get(UnitData data, Vector3 position, Quaternion rotation, int sourceSceneHandle)
        {
            if (data == null || data.Prefab == null) return null;

            Unit unit = GetPool(data).Get();
            unit.SourceSceneHandle = sourceSceneHandle;
            unit.transform.SetParent(_activeRoot, false);
            unit.transform.SetPositionAndRotation(position, rotation);
            _active.Add(unit);

            // 위치를 정한 뒤에 켠다. 켜지는 순간 Awake/OnEnable이 돈다.
            unit.gameObject.SetActive(true);
            return unit;
        }

        /// <summary>유닛을 풀로 돌려보낸다. 이미 돌려보냈거나 이 풀에서 나가지 않은 유닛이면 아무 일도 하지 않는다.</summary>
        public void Release(Unit unit)
        {
            if (unit == null || _active.Remove(unit) == false) return;

            unit.ReturnToPool();
        }

        Transform CreateRoot(string rootName)
        {
            Transform root = new GameObject(rootName).transform;
            root.SetParent(transform, false);
            return root;
        }

        IObjectPool<Unit> GetPool(UnitData data)
        {
            if (_pools.TryGetValue(data, out IObjectPool<Unit> pool)) return pool;

            ObjectPool<Unit> created = null;
            created = new ObjectPool<Unit>(
                createFunc: () => CreateUnit(data, created),
                actionOnRelease: OnReleased,
                actionOnDestroy: unit => { if (unit != null) Destroy(unit.gameObject); },
                maxSize: MAX_SIZE);

            _pools[data] = created;
            return created;
        }

        // 보관 부모 아래에 꺼 둔 채로 만든다. 켜는 것은 Get에서 위치를 정한 뒤에 한다.
        Unit CreateUnit(UnitData data, IObjectPool<Unit> pool)
        {
            GameObject instance = Instantiate(data.Prefab, _storage);
            if (instance.TryGetComponent(out Unit unit) == false) unit = instance.AddComponent<Unit>();

            unit.SetPool(pool);
            instance.SetActive(false);
            return unit;
        }

        void OnReleased(Unit unit)
        {
            // 먼저 꺼서 애니메이터 등이 OnDisable로 구독을 해제하게 한 뒤, 이전 수명의 상태를 비운다.
            unit.gameObject.SetActive(false);
            unit.ResetForPool();
            unit.transform.SetParent(_storage, false);
        }

        // 내려가는 씬에서 소환한 유닛은 그 씬의 그리드와 함께 쓸모가 없어지므로 회수한다. 회수하면 _active에서 스스로 빠진다.
        void HandleSceneUnloaded(Scene scene)
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                Unit unit = _active[i];
                if (unit == null)
                {
                    _active.RemoveAt(i);
                    continue;
                }

                if (unit.SourceSceneHandle == scene.handle) Release(unit);
            }
        }
    }
}
