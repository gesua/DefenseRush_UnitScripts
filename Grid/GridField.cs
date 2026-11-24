using System;
using System.Collections.Generic;
using UnityEngine;

namespace Defense.Units
{
    /// <summary>
    /// 플레이어 유닛이 놓이는 그리드 필드.
    /// 칸 생성, 인덱스↔월드 좌표 변환, 유닛 생성/이동을 담당한다.
    ///
    /// 이 오브젝트의 right(빨간 축)가 화면 오른쪽, forward(파란 축)가 화면 위쪽(카메라에서 먼 쪽)이면
    /// 0번 칸(row 0, col 0)이 '왼쪽 위'가 된다. 스케일은 1로 유지할 것.
    /// </summary>
    public class GridField : MonoBehaviour
    {
        [Header("그리드 설정")]
        [SerializeField, Min(1)] int _rows = 3;
        [SerializeField, Min(1)] int _cols = 5;
        [Tooltip("칸 하나의 크기. x: 가로, y: 세로 (월드 단위)")]
        [SerializeField] Vector2 _cellSize = new Vector2(2f, 2f);
        [SerializeField, Min(1)] int _maxUnitsPerSlot = 3;
        [Tooltip("기본 최대 마릿수. 유물(영웅 수 증가) 효과는 여기에 더해진다. 도달하면 소환/도박/보상 지급이 막힌다.")]
        [SerializeField, Min(1)] int _maxTotalUnits = 20;
        [Tooltip("한 칸에 유닛이 여러 마리일 때 서로 떨어지는 반경")]
        [SerializeField] float _stackRadius = 0.4f;
        [Tooltip("유닛이 기본으로 바라보는 방향. 그리드의 forward(화면 위쪽)를 기준으로 한 Y 회전 각도. 180이면 화면 아래쪽을 바라본다.")]
        [SerializeField] float _unitFacingAngle = 180f;

        [Header("연결")]
        [Tooltip("IMonsterRegistry를 구현한 컴포넌트(몬스터/시스템 쪽). 비어 있으면 유닛이 공격하지 않는다.")]
        [SerializeField] MonoBehaviour _monsterRegistrySource;
        [Tooltip("IUnitStatProvider를 구현한 컴포넌트(UnitStatProvider). 비어 있으면 레벨 보너스 없이 기본 스탯으로 싸운다.")]
        [SerializeField] MonoBehaviour _statProviderSource;
        [Tooltip("ICurrencyReceiver를 구현한 컴포넌트(시스템 쪽 재화). 비어 있으면 코인을 주는 스킬(약탈, 노획)이 오류 로그를 남기고 작동하지 않는다. (BattleEconomy를 연결한다)")]
        [SerializeField] MonoBehaviour _currencySource;
        [Tooltip("IArtifactEffects를 구현한 컴포넌트(로비 유물). 비워두면 GameManager의 유물을, 그것도 없으면 유물 효과가 없다.")]
        [SerializeField] MonoBehaviour _artifactSource;

        readonly List<Slot> _slots = new List<Slot>();
        // ConsolidateStacks에서 쓰는 임시 버퍼. (같은 종류이면서 자리가 남은 칸)
        readonly List<Slot> _openStacks = new List<Slot>();
        IMonsterRegistry _monsterRegistry;
        IUnitStatProvider _statProvider;
        ICurrencyReceiver _currency;
        IArtifactEffects _artifacts;
        UnitAuraSystem _auraSystem;

        /// <summary>행 우선(0번 = 왼쪽 위) 순서의 칸 목록.</summary>
        public IReadOnlyList<Slot> Slots => _slots;

        // 유물 쪽이 아직 준비되지 않았을 수 있어, 비어 있으면 읽는 시점에 GameManager의 유물을 찾아 본다.
        IArtifactEffects Artifacts => _artifacts ??= GameModelArtifactEffects.TryCreate();

        /// <summary>유물 효과를 빼고 Inspector에 설정한 기본 최대 마릿수.</summary>
        public int BaseMaxTotalUnits => _maxTotalUnits;

        /// <summary>필드에 둘 수 있는 유닛 총 마릿수. 기본 최대치에 유물(영웅 수 증가)을 더한 값.</summary>
        public int MaxTotalUnits
        {
            get
            {
                IArtifactEffects artifacts = Artifacts;
                int bonus = (artifacts != null) ? Mathf.Max(0, Mathf.FloorToInt(artifacts.GetBonus(ArtifactEffect.UnitCapacity))) : 0;
                return _maxTotalUnits + bonus;
            }
        }

        /// <summary>지금 필드에 있는 유닛 총 마릿수.</summary>
        public int TotalUnitCount
        {
            get
            {
                int total = 0;
                for (int i = 0; i < _slots.Count; i++) total += _slots[i].Count;
                return total;
            }
        }

        /// <summary>유닛 총 마릿수가 최대치에 도달했으면 true.</summary>
        public bool IsUnitLimitReached => TotalUnitCount >= MaxTotalUnits;

        /// <summary>그리드 전체 크기(월드 단위). x: 가로, y: 세로.</summary>
        public Vector2 FieldSize => new Vector2(_cols * _cellSize.x, _rows * _cellSize.y);

        /// <summary>칸 하나의 크기(월드 단위). x: 가로, y: 세로.</summary>
        public Vector2 CellSize => _cellSize;

        /// <summary>소환/이동/업그레이드로 칸의 내용이 바뀔 때마다 호출된다. (UI가 구독)</summary>
        public event Action<Slot> SlotChanged;

        void Awake()
        {
            _monsterRegistry = _monsterRegistrySource as IMonsterRegistry;
            if (_monsterRegistrySource != null && _monsterRegistry == null)
                Debug.LogError("[GridField] monsterRegistrySource가 IMonsterRegistry를 구현하지 않았습니다.", this);
            else if (_monsterRegistry == null)
                Debug.LogWarning("[GridField] monsterRegistrySource가 비어 있어 유닛이 공격하지 않습니다.", this);

            _statProvider = _statProviderSource as IUnitStatProvider;
            if (_statProviderSource != null && _statProvider == null)
                Debug.LogError("[GridField] statProviderSource가 IUnitStatProvider를 구현하지 않았습니다.", this);

            _currency = _currencySource as ICurrencyReceiver;
            if (_currencySource != null && _currency == null)
                Debug.LogError("[GridField] currencySource가 ICurrencyReceiver를 구현하지 않았습니다.", this);

            _artifacts = _artifactSource as IArtifactEffects;
            if (_artifactSource != null && _artifacts == null)
                Debug.LogError("[GridField] artifactSource가 IArtifactEffects를 구현하지 않았습니다.", this);

            BuildSlots();

            // 소환, 이동, 판매, 승급은 모두 SlotChanged를 지나므로 오라 스킬은 그때마다 다시 계산된다.
            _auraSystem = new UnitAuraSystem(this);
        }

        void OnDestroy()
        {
            _auraSystem?.Dispose();
        }

        // Inspector에서 인터페이스를 구현하지 않은 컴포넌트를 끌어 넣으면 바로 알려준다.
        void OnValidate()
        {
            if (_monsterRegistrySource != null && (_monsterRegistrySource is IMonsterRegistry) == false)
                Debug.LogError("[GridField] monsterRegistrySource가 IMonsterRegistry를 구현하지 않았습니다.", this);

            if (_statProviderSource != null && (_statProviderSource is IUnitStatProvider) == false)
                Debug.LogError("[GridField] statProviderSource가 IUnitStatProvider를 구현하지 않았습니다.", this);

            if (_currencySource != null && (_currencySource is ICurrencyReceiver) == false)
                Debug.LogError("[GridField] currencySource가 ICurrencyReceiver를 구현하지 않았습니다.", this);

            if (_artifactSource != null && (_artifactSource is IArtifactEffects) == false)
                Debug.LogError("[GridField] artifactSource가 IArtifactEffects를 구현하지 않았습니다.", this);
        }

        void BuildSlots()
        {
            _slots.Clear();
            for (int r = 0; r < _rows; r++)
            {
                for (int c = 0; c < _cols; c++)
                {
                    Vector3 center = transform.TransformPoint(LocalCenter(r, c));
                    _slots.Add(new Slot(r, c, center, _maxUnitsPerSlot, _stackRadius, transform.rotation, this));
                }
            }
        }

        // 인덱스 → 좌표 변환은 이 함수 한 곳에서만 한다. row 0이 위쪽(+z).
        Vector3 LocalCenter(int row, int col)
        {
            float x = (col - (_cols - 1) * 0.5f) * _cellSize.x;
            float z = -(row - (_rows - 1) * 0.5f) * _cellSize.y;
            return new Vector3(x, 0f, z);
        }

        public Slot GetSlot(int row, int col)
        {
            if (row < 0 || row >= _rows || col < 0 || col >= _cols) return null;
            return _slots[row * _cols + col];
        }

        /// <summary>
        /// 기준 칸에 인접한 칸을 results에 담는다. 기본은 상하좌우, includeDiagonal이면 대각선까지 포함한다.
        /// 그리드 밖의 칸과 기준 칸 자신은 담지 않는다. (results는 호출한 쪽이 비워서 넘긴다)
        /// </summary>
        public void GetAdjacentSlots(Slot slot, bool includeDiagonal, List<Slot> results)
        {
            if (slot == null || results == null) return;

            for (int rowOffset = -1; rowOffset <= 1; rowOffset++)
            {
                for (int colOffset = -1; colOffset <= 1; colOffset++)
                {
                    if (rowOffset == 0 && colOffset == 0) continue;
                    if (includeDiagonal == false && rowOffset != 0 && colOffset != 0) continue;

                    Slot neighbor = GetSlot(slot.Row + rowOffset, slot.Col + colOffset);
                    if (neighbor != null) results.Add(neighbor);
                }
            }
        }

        /// <summary>월드 좌표가 속한 칸. 그리드 밖이면 null.</summary>
        public Slot WorldToSlot(Vector3 worldPos)
        {
            Vector3 local = transform.InverseTransformPoint(worldPos);
            int col = Mathf.FloorToInt(local.x / _cellSize.x + _cols * 0.5f);
            int row = Mathf.FloorToInt(-local.z / _cellSize.y + _rows * 0.5f);
            return GetSlot(row, col);
        }

        /// <summary>빈 칸이 하나라도 있으면 true.</summary>
        public bool HasEmptySlot
        {
            get
            {
                for (int i = 0; i < _slots.Count; i++)
                {
                    if (_slots[i].IsEmpty) return true;
                }
                return false;
            }
        }

        /// <summary>
        /// 같은 종류가 이미 있고 자리가 남은 칸(행 우선, 왼쪽 위부터). 없으면 null.
        /// 소환/도박/승급/보상이 모두 이 기준을 함께 쓴다.
        /// </summary>
        public Slot FindStackSlot(UnitData data)
        {
            if (data == null) return null;

            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].IsEmpty == false && _slots[i].CanAdd(data)) return _slots[i];
            }
            return null;
        }

        /// <summary>
        /// 유닛을 놓을 칸. 같은 종류가 이미 있고 자리가 남은 칸을 먼저, 없으면 첫 빈 칸(행 우선, 왼쪽 위부터).
        /// 놓을 자리가 없으면 null.
        /// </summary>
        public Slot FindSlotFor(UnitData data)
        {
            if (data == null) return null;

            Slot stackSlot = FindStackSlot(data);
            if (stackSlot != null) return stackSlot;

            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].IsEmpty) return _slots[i];
            }
            return null;
        }

        /// <summary>유닛을 생성해 칸에 배치한다. 자리가 없으면 null.</summary>
        public Unit SpawnUnit(UnitData data, Slot slot)
        {
            if (data == null || data.Prefab == null || slot == null || slot.CanAdd(data) == false || IsUnitLimitReached)
                return null;

            Unit unit = UnitPool.GetOrCreate().Get(data, slot.Center, transform.rotation * Quaternion.Euler(0f, _unitFacingAngle, 0f), gameObject.scene.handle);
            if (unit == null) return null;

            unit.Init(data, _monsterRegistry, _statProvider, _currency);

            slot.Add(unit);
            slot.Relayout(instantUnit: unit);
            SlotChanged?.Invoke(slot);
            SoundManager.Play(GameSfx.Summon);
            return unit;
        }

        /// <summary>
        /// 칸의 유닛 전체를 다른 칸으로 옮긴다.
        /// 빈 칸: 이동 / 같은 종류: 자리만큼 합치기 / 다른 종류: 교환 / 같은 종류인데 가득 참: 실패.
        /// </summary>
        public bool TryMove(Slot from, Slot to)
        {
            if (from == null || to == null || from == to || from.IsEmpty) return false;

            if (to.IsEmpty)
            {
                to.AddRange(from.TakeAll());                // 빈 칸: 스택 전체 이동
            }
            else if (to.Type == from.Type)
            {
                if (to.IsFull) return false;                // 같은 종류 + 가득 참: 원위치
                while (to.IsFull == false && from.IsEmpty == false)         // 같은 종류: 자리만큼 합치기
                    to.Add(from.TakeLast());
            }
            else
            {
                List<Unit> fromUnits = from.TakeAll();      // 다른 종류: 두 칸 교환
                List<Unit> toUnits = to.TakeAll();
                from.AddRange(toUnits);
                to.AddRange(fromUnits);
            }

            from.Relayout();
            to.Relayout();
            SlotChanged?.Invoke(from);
            SlotChanged?.Invoke(to);
            return true;
        }

        /// <summary>칸의 유닛 전체를 제거(풀로 반환)하고 칸이 바뀌었음을 알린다.</summary>
        public void ClearSlot(Slot slot)
        {
            if (slot == null || slot.IsEmpty) return;

            UnitPool pool = UnitPool.GetOrCreate();
            foreach (Unit unit in slot.TakeAll())
                pool.Release(unit);

            SlotChanged?.Invoke(slot);
        }

        /// <summary>
        /// 칸에 가장 마지막으로 쌓인 유닛 한 마리를 제거(풀로 반환)하고, 남은 유닛을 다시 배치한 뒤 칸이 바뀌었음을 알린다.
        /// 제거했으면 true. 빈 칸이면 false.
        /// </summary>
        public bool RemoveLastUnit(Slot slot)
        {
            if (slot == null || slot.IsEmpty) return false;

            UnitPool.GetOrCreate().Release(slot.TakeLast());
            slot.Relayout();
            SlotChanged?.Invoke(slot);
            return true;
        }

        /// <summary>
        /// 같은 종류가 자리를 남긴 채 여러 칸에 나뉘어 있으면 한 마리씩 걸어가서 합친다.
        /// 예: 3마리 + 2마리에서 3마리 칸의 한 마리를 팔면 2 + 2가 아니라 3 + 1이 된다.
        /// 가득 찬 칸은 건드리지 않고, 가장 많이 쌓인 칸(같으면 preferred 칸, 그다음 행 우선)으로 가장 적게 쌓인 칸의 유닛을 옮긴다.
        /// 자리가 남은 칸이 하나만 남을 때까지 반복한다.
        /// </summary>
        public void ConsolidateStacks(UnitData data, Slot preferred = null)
        {
            if (data == null) return;

            while (true)
            {
                CollectOpenStacks(data, _openStacks);
                if (_openStacks.Count < 2) break;

                Slot receiver = FindReceiver(_openStacks, preferred);
                Slot donor = FindDonor(_openStacks, receiver);

                // 가장 적게 쌓인 칸의 마지막 유닛 한 마리가 받는 칸으로 걸어간다. (TryMove의 합치기와 같은 방식)
                receiver.Add(donor.TakeLast());
                donor.Relayout();
                receiver.Relayout();
                SlotChanged?.Invoke(donor);
                SlotChanged?.Invoke(receiver);
            }

            _openStacks.Clear();
        }

        // 같은 종류이면서 자리가 남은 칸을 행 우선으로 results에 담는다.
        void CollectOpenStacks(UnitData data, List<Slot> results)
        {
            results.Clear();
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].IsEmpty == false && _slots[i].Type == data && _slots[i].IsFull == false)
                    results.Add(_slots[i]);
            }
        }

        // 가장 많이 쌓인 칸. 같으면 preferred 칸, 그것도 아니면 앞선 칸.
        static Slot FindReceiver(List<Slot> openStacks, Slot preferred)
        {
            Slot receiver = null;
            for (int i = 0; i < openStacks.Count; i++)
            {
                Slot candidate = openStacks[i];
                if (receiver == null || candidate.Count > receiver.Count || (candidate.Count == receiver.Count && candidate == preferred))
                    receiver = candidate;
            }
            return receiver;
        }

        // 받는 칸을 뺀 나머지 중 가장 적게 쌓인 칸. 같으면 앞선 칸.
        static Slot FindDonor(List<Slot> openStacks, Slot receiver)
        {
            Slot donor = null;
            for (int i = 0; i < openStacks.Count; i++)
            {
                Slot candidate = openStacks[i];
                if (candidate == receiver) continue;

                if (donor == null || candidate.Count < donor.Count)
                    donor = candidate;
            }
            return donor;
        }

        // Scene 뷰에서 칸 배치를 확인하기 위한 격자 표시
        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 0.9f, 0.4f, 0.9f);
            Gizmos.matrix = transform.localToWorldMatrix;

            var size = new Vector3(_cellSize.x * 0.95f, 0.05f, _cellSize.y * 0.95f);
            for (int r = 0; r < _rows; r++)
                for (int c = 0; c < _cols; c++)
                    Gizmos.DrawWireCube(LocalCenter(r, c), size);
        }
    }
}