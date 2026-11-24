using System;
using System.Collections.Generic;
using UnityEngine;

namespace Defense.Units
{
    /// <summary>
    /// 도박. 등급(희귀/영웅/전설)을 골라 전투 재화를 내면 일정 확률로 그 등급의 랜덤 유닛을 얻는다.
    /// 실패하면 재화만 사라진다. 성공 확률은 설정의 기본 확률에 유물의 도박 확률 증가(%p)를 더한 값이다.
    /// 유물의 도박 비용 감소는 모든 도박 비용에 반영되고, 유물의 도박 실패 보상이 있으면 실패했을 때 확률로 한 등급 아래 유닛을 대신 얻는다.
    /// 재화를 내기 전에 성공했을 때 놓을 칸을 확보하므로, 재화만 내고 유닛을 못 받는 일은 없다.
    /// 일반 소환처럼 성공할 때마다 그 등급의 비용이 설정의 증가량만큼 오른다. (등급마다 따로 오르며 실패하면 오르지 않는다)
    /// </summary>
    public class GambleManager : MonoBehaviour
    {
        [SerializeField] GridField _grid;
        [SerializeField] UnitDatabase _database;
        [SerializeField] GambleConfig _config;

        [Tooltip("IWallet을 구현한 컴포넌트(전투 재화 BattleEconomy). 비워두면 무료 도박.")]
        [SerializeField] MonoBehaviour _walletSource;

        [Tooltip("IArtifactEffects를 구현한 컴포넌트(로비 유물). 비워두면 GameManager의 유물을, 그것도 없으면 유물 효과가 없다.")]
        [SerializeField] MonoBehaviour _artifactSource;

        IWallet _wallet;
        IArtifactEffects _artifacts;

        // 등급별 성공 횟수. 이 컴포넌트가 만들어질 때(런 시작)마다 0부터 다시 센다.
        readonly Dictionary<UnitGrade, int> _successCounts = new Dictionary<UnitGrade, int>();

        /// <summary>도박을 시도할 때마다(성공/실패/막힘 모두) 결과와 함께 호출된다. (UI가 구독)</summary>
        public event Action<GambleResult> Gambled;

        bool HasReferences => _grid != null && _database != null && _config != null;

        // 유물 쪽이 아직 준비되지 않았을 수 있어, 비어 있으면 읽는 시점에 GameManager의 유물을 찾아 본다.
        IArtifactEffects Artifacts => _artifacts ??= GameModelArtifactEffects.TryCreate();

        void Awake()
        {
            if (HasReferences == false)
                Debug.LogError("[GambleManager] grid / database / config 참조가 비어 있습니다.", this);

            _wallet = _walletSource as IWallet;
            if (_walletSource != null && _wallet == null)
                Debug.LogError("[GambleManager] walletSource가 IWallet을 구현하지 않았습니다.", this);

            _artifacts = _artifactSource as IArtifactEffects;
            if (_artifactSource != null && _artifacts == null)
                Debug.LogError("[GambleManager] artifactSource가 IArtifactEffects를 구현하지 않았습니다.", this);
        }

        // Inspector에서 인터페이스를 구현하지 않은 컴포넌트를 끌어 넣으면 바로 알려준다.
        void OnValidate()
        {
            if (_walletSource != null && (_walletSource is IWallet) == false)
                Debug.LogError("[GambleManager] walletSource가 IWallet을 구현하지 않았습니다.", this);

            if (_artifactSource != null && (_artifactSource is IArtifactEffects) == false)
                Debug.LogError("[GambleManager] artifactSource가 IArtifactEffects를 구현하지 않았습니다.", this);
        }

        /// <summary>해당 등급으로 도박할 수 있는지(설정 표에 있는지).</summary>
        public bool HasEntry(UnitGrade grade) => TryGetEntry(grade, out _);

        /// <summary>한 번 도박하는 비용. 설정이 없으면 -1. 성공한 횟수만큼 오른 지금의 비용이다.</summary>
        public int GetCost(UnitGrade grade) => (TryGetEntry(grade, out GambleConfig.Entry entry)) ? GetCurrentCost(entry) : -1;

        /// <summary>기본 성공 확률(%). 설정이 없으면 0.</summary>
        public float GetBaseChance(UnitGrade grade) => (TryGetEntry(grade, out GambleConfig.Entry entry)) ? entry.BaseChance : 0f;

        /// <summary>유물이 더해 주는 성공 확률(%p). 유물 보정이 없는 등급이거나 유물이 없으면 0.</summary>
        public float GetBonusChance(UnitGrade grade)
        {
            if (TryGetEntry(grade, out GambleConfig.Entry entry) == false || entry.HasArtifactBonus == false) return 0f;

            IArtifactEffects artifacts = Artifacts;
            if (artifacts == null) return 0f;

            // 유물은 비율(0.05 = 5%p)로 돌려주므로 %로 바꿔서 더한다.
            return Mathf.Max(0f, artifacts.GetBonus(entry.ArtifactEffect)) * 100f;
        }

        /// <summary>실제 성공 확률(%). 기본 확률 + 유물 보정을 0~100으로 보정한 값.</summary>
        public float GetChance(UnitGrade grade) => Mathf.Clamp(GetBaseChance(grade) + GetBonusChance(grade), 0f, 100f);

        /// <summary>해당 등급으로 한 번 도박한다. 결과는 반환값과 Gambled 이벤트로 알린다.</summary>
        public GambleResult TryGamble(UnitGrade grade)
        {
            GambleResult result = Resolve(grade);
            if (result.Outcome == GambleOutcome.Miss) SoundManager.Play(GameSfx.GambleFail);
            Gambled?.Invoke(result);
            return result;
        }

        GambleResult Resolve(UnitGrade grade)
        {
            if (HasReferences == false
                || TryGetEntry(grade, out GambleConfig.Entry entry) == false
                || _database.HasUnits(grade) == false)
            {
                Debug.LogWarning($"[GambleManager] {grade} 등급은 설정 표나 유닛 목록에 없어 도박할 수 없습니다.", this);
                return new GambleResult(grade, GambleOutcome.Unavailable);
            }

            if (_grid.IsUnitLimitReached) return new GambleResult(grade, GambleOutcome.UnitLimit);

            // 1) 성공했을 때 놓을 자리를 결제 전에 확보한다. 빈 칸이 없으면 결제하지 않고 막는다.
            //    (이 조건이 없으면 필드가 가득 찼을 때 '실패'만 결제되는 불공정한 도박이 된다)
            if (_grid.HasEmptySlot == false)
                return new GambleResult(grade, GambleOutcome.NoSpace);

            // 2) 성공했을 때 얻을 유닛도 미리 정한다. 성공/실패 판정과는 무관하게 뽑으므로 확률에 영향이 없다.
            UnitData data = _database.GetRandom(grade);
            if (data == null || data.Prefab == null)
            {
                Debug.LogError($"[GambleManager] {grade} 등급 유닛 데이터 또는 프리팹이 비어 있습니다.", this);
                return new GambleResult(grade, GambleOutcome.Unavailable);
            }

            Slot slot = _grid.FindSlotFor(data);
            if (slot == null)
                return new GambleResult(grade, GambleOutcome.NoSpace);

            // 3) 결제. 성공할 때마다 오른 지금의 비용으로 낸다.
            if (_wallet != null && _wallet.TrySpend(GetCurrentCost(entry)) == false)
                return new GambleResult(grade, GambleOutcome.NoFunds);

            // 4) 판정. 실패하면 재화만 사라진다. (유물이 있으면 확률로 한 등급 아래 유닛을 대신 얻는다)
            if (Roll(GetChance(grade)) == false)
                return new GambleResult(grade, GambleOutcome.Miss, null, TrySpawnConsolation(grade));

            // 5) 성공: 미리 확보한 칸에 소환한다.
            if (_grid.SpawnUnit(data, slot) == null)
            {
                Debug.LogError($"[GambleManager] {data.name} 배치에 실패했습니다. (결제는 이미 완료된 상태)", this);
                return new GambleResult(grade, GambleOutcome.Unavailable);
            }

            // 6) 성공했을 때만 이 등급의 비용을 올린다. 실패나 막힘은 올리지 않는다.
            _successCounts[grade] = GetSuccessCount(grade) + 1;

            return new GambleResult(grade, GambleOutcome.Success, data);
        }

        int GetSuccessCount(UnitGrade grade) => (_successCounts.TryGetValue(grade, out int count)) ? count : 0;

        // 기본 비용 + 성공 횟수 × 증가량. 정수 한계를 넘지 않게 막고, 유물의 도박 비용 감소를 반영한다.
        int GetCurrentCost(GambleConfig.Entry entry)
        {
            int cost = (int)Math.Min(int.MaxValue, (long)entry.Cost + (long)entry.CostIncrease * GetSuccessCount(entry.Grade));
            return ArtifactMath.ApplyDiscount(cost, GetArtifactBonus(ArtifactEffect.GambleCostDiscount));
        }

        // 실패했을 때 유물 효과로 한 등급 아래 유닛을 한 마리 소환한다. 소환하지 않았으면 null.
        // 성공할 때와 같은 조건(빈 칸, 최대 마릿수)을 결제 전에 확인했으므로, 놓을 자리는 이미 있다.
        UnitData TrySpawnConsolation(UnitGrade grade)
        {
            if (grade.IsMin()) return null;
            if (Roll(Mathf.Clamp01(GetArtifactBonus(ArtifactEffect.GambleFailUnitChance)) * 100f) == false) return null;

            UnitGrade lower = grade.Previous();
            if (_database.HasUnits(lower) == false) return null;

            UnitData data = _database.GetRandom(lower);
            if (data == null || data.Prefab == null) return null;

            Slot slot = _grid.FindSlotFor(data);
            if (slot == null) return null;

            return (_grid.SpawnUnit(data, slot) != null) ? data : null;
        }

        // 유물 효과의 현재 값. 유물이 없으면 0.
        float GetArtifactBonus(ArtifactEffect effect)
        {
            IArtifactEffects artifacts = Artifacts;
            return (artifacts != null) ? Mathf.Max(0f, artifacts.GetBonus(effect)) : 0f;
        }

        bool TryGetEntry(UnitGrade grade, out GambleConfig.Entry entry)
        {
            entry = null;
            return _config != null && _config.TryGetEntry(grade, out entry);
        }

        // percent는 0~100. 0 이하는 항상 실패, 100 이상은 항상 성공.
        static bool Roll(float percent)
        {
            if (percent <= 0f) return false;
            if (percent >= 100f) return true;

            return UnityEngine.Random.value * 100f < percent;
        }
    }
}