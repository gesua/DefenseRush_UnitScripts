using System.Collections.Generic;
using UnityEngine;

namespace Defense.Units
{
    /// <summary>랜덤 유닛을 소환해 그리드에 배치한다.</summary>
    public partial class SummonManager : MonoBehaviour
    {
        [SerializeField] GridField _grid;
        [SerializeField] UnitDatabase _database;

        [Header("소환 설정")]
        [Tooltip("walletSource가 가격을 따로 받지 않는 단순 지갑일 때만 쓰이는 고정 소환 비용.\n" +
                 "BattleSummonController처럼 변동 가격을 직접 결제하는 지갑을 연결하면 이 값은 무시된다. (walletSource가 비어 있으면 무료 소환)")]
        [SerializeField] int _summonCost = 10;
        [Tooltip("등급별 소환 가중치 (순서: 일반, 희귀, 영웅, 전설). 합이 100일 필요는 없다.\n" +
                 "소환 확률 강화(chanceProgress)를 연결하면 이 값 대신 강화 레벨의 가중치를 쓴다.")]
        [SerializeField] float[] _gradeWeights = { 97.45f, 1.95f, 0.5f, 0.1f };

        [Tooltip("소환 확률 강화 상태. 연결하면 현재 강화 레벨의 가중치를 쓰고, 비워두면 위의 가중치를 그대로 쓴다.")]
        [SerializeField] SummonChanceProgress _chanceProgress;

        [Tooltip("IWallet을 구현한 컴포넌트(시스템 쪽 골드). 비워두면 무료 소환.")]
        [SerializeField] MonoBehaviour _walletSource;

        IWallet _wallet;

        /// <summary>유닛 총 마릿수가 최대치라 소환이 막혔을 때 호출된다. (재화는 쓰지 않는다)</summary>
        public event System.Action UnitLimitReached;
        /// <summary>빈 칸이 없어 소환이 막혔을 때 호출된다. (재화는 쓰지 않는다. UI가 안내 문구를 띄우는 데 쓴다)</summary>
        public event System.Action NoSpace;

        void Awake()
        {
            if (_grid == null || _database == null)
                Debug.LogError("[SummonManager] grid / database 참조가 비어 있습니다.", this);

            _wallet = _walletSource as IWallet;
            if (_walletSource != null && _wallet == null)
                Debug.LogError("[SummonManager] walletSource가 IWallet을 구현하지 않았습니다.", this);
        }

        public void OnSummonClick()
        {
            Summon();
        }

        /// <summary>랜덤 유닛을 소환한다. 성공하면 true.</summary>
        public bool Summon()
        {
            if (_grid == null || _database == null) return false;

            // 최대 마릿수에 도달했으면 시작하지 않는다.
            if (_grid.IsUnitLimitReached)
            {
                UnitLimitReached?.Invoke();
                return false;
            }

            // 0) 빈 칸이 없으면 시작하지 않는다. 도박과 같은 규칙이며, 칸을 비우면(승급 등) 다시 소환할 수 있다.
            if (_grid.HasEmptySlot == false)
            {
                NoSpace?.Invoke();
                return false;
            }

            // 1) 랜덤 유닛 결정
            UnitData data = _database.GetRandom(RollGrade());
            if (data == null || data.Prefab == null) return false;

            // 2) 놓을 칸 선택. 자리가 없으면 실패 (이 경우 골드는 쓰지 않는다)
            Slot slot = PickSlot(data);
            if (slot == null) return false;

            // 3) 골드 차감
            if (_wallet != null && _wallet.TrySpend(_summonCost) == false) return false;

            // 4) 생성 및 배치
            return _grid.SpawnUnit(data, slot) != null;
        }

        // 같은 종류가 이미 있고 자리가 남은 칸 우선, 없으면 첫 빈 칸(행 우선, 왼쪽 위부터).
        Slot PickSlot(UnitData data) => _grid.FindSlotFor(data);

        UnitGrade RollGrade()
        {
            IReadOnlyList<float> weights = GetWeights();

            float total = 0f;
            for (int i = 0; i < weights.Count; i++) total += Mathf.Max(0f, weights[i]);
            if (total <= 0f) return UnitGrade.Common;

            float roll = Random.value * total;
            int last = 0;
            for (int i = 0; i < weights.Count; i++)
            {
                float w = Mathf.Max(0f, weights[i]);
                if (w <= 0f) continue;

                last = i;
                roll -= w;
                if (roll < 0f) return (UnitGrade)i;
            }
            return (UnitGrade)last;
        }

        // 소환 확률 강화가 연결돼 있으면 그 가중치를, 아니면 이 컴포넌트의 가중치를 쓴다.
        IReadOnlyList<float> GetWeights()
        {
            IReadOnlyList<float> chance = (_chanceProgress != null) ? _chanceProgress.Weights : null;
            return chance ?? _gradeWeights;
        }
    }
}