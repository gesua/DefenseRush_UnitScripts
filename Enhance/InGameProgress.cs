using System;
using System.Collections.Generic;
using UnityEngine;

namespace Defense.Units
{
    /// <summary>
    /// 인게임 강화 상태. 등급별로 레벨(1~15)을 들고 있고, 판이 시작될 때(씬 로드)마다 1로 초기화된다.
    /// GradeEnhanceConfig에서 묶은 등급(예: 일반+희귀)은 레벨을 같이 올린다.
    /// </summary>
    public class InGameProgress : MonoBehaviour
    {
        [SerializeField] GradeEnhanceConfig _config;

        [Tooltip("IWallet을 구현한 컴포넌트(시스템 쪽 골드). 비워두면 무료 강화.")]
        [SerializeField] MonoBehaviour _walletSource;

        [Tooltip("IArtifactEffects를 구현한 컴포넌트(로비 유물). 비워두면 GameManager의 유물을, 그것도 없으면 유물 효과가 없다.")]
        [SerializeField] MonoBehaviour _artifactSource;

        readonly int[] _levels = NewLevels();
        IWallet _wallet;
        IArtifactEffects _artifacts;

        // 유물 쪽이 아직 준비되지 않았을 수 있어, 비어 있으면 읽는 시점에 GameManager의 유물을 찾아 본다.
        IArtifactEffects Artifacts => _artifacts ??= GameModelArtifactEffects.TryCreate();

        /// <summary>등급의 강화 레벨이 바뀔 때 호출된다. (UI가 구독)</summary>
        public event Action<UnitGrade> LevelChanged;

        static int[] NewLevels()
        {
            var array = new int[UnitGrades.Count];
            for (int i = 0; i < array.Length; i++) array[i] = 1;
            return array;
        }

        void Awake()
        {
            _wallet = _walletSource as IWallet;
            if (_walletSource != null && _wallet == null)
                Debug.LogError("[InGameProgress] walletSource가 IWallet을 구현하지 않았습니다.", this);

            _artifacts = _artifactSource as IArtifactEffects;
            if (_artifactSource != null && _artifacts == null)
                Debug.LogError("[InGameProgress] artifactSource가 IArtifactEffects를 구현하지 않았습니다.", this);
        }

        // Inspector에서 인터페이스를 구현하지 않은 컴포넌트를 끌어 넣으면 바로 알려준다.
        void OnValidate()
        {
            if (_artifactSource != null && (_artifactSource is IArtifactEffects) == false)
                Debug.LogError("[InGameProgress] artifactSource가 IArtifactEffects를 구현하지 않았습니다.", this);
        }

        public int GetLevel(UnitGrade grade) => _levels[(int)grade];

        public bool IsMaxLevel(UnitGrade grade) => GetLevel(grade) >= UnitLevel.MAX;

        /// <summary>현재 레벨에서 다음 레벨로 강화할 때 추가되는 능력치만 반환한다.</summary>
        public StatBonus GetNextLevelIncrease(UnitGrade grade)
        {
            if (_config == null || IsMaxLevel(grade)) return default;

            StatBonus currentBonus = _config.GetBonus(grade, GetLevel(grade));
            StatBonus nextBonus = _config.GetBonus(grade, GetLevel(grade) + 1);
            return new StatBonus
            {
                AttackPower = nextBonus.AttackPower - currentBonus.AttackPower,
                AttackSpeed = nextBonus.AttackSpeed - currentBonus.AttackSpeed,
                AttackRange = nextBonus.AttackRange - currentBonus.AttackRange
            };
        }

        /// <summary>다음 레벨로 올리는 비용. 최고 레벨이거나 설정이 없으면 -1. 유물의 강화 비용 감소가 반영된다.</summary>
        public int GetNextCost(UnitGrade grade)
        {
            if (_config == null) return -1;

            IArtifactEffects artifacts = Artifacts;
            float discount = (artifacts != null) ? Mathf.Max(0f, artifacts.GetBonus(ArtifactEffect.UpgradeCostDiscount)) : 0f;
            return ArtifactMath.ApplyDiscount(_config.GetUpgradeCost(grade, GetLevel(grade)), discount);
        }

        /// <summary>해당 등급 전체를 한 단계 강화한다. 묶인 등급은 같이 올라간다. 최고 레벨이거나 골드가 부족하면 false.</summary>
        public bool TryUpgrade(UnitGrade grade)
        {
            if (_config == null || IsMaxLevel(grade)) return false;

            int cost = GetNextCost(grade);
            if (cost < 0) return false;
            if (_wallet != null && _wallet.TrySpend(cost) == false) return false;

            IReadOnlyList<UnitGrade> group = _config.GetGroup(grade);
            for (int i = 0; i < group.Count; i++) _levels[(int)group[i]]++;
            SoundManager.Play(GameSfx.Upgrade);
            for (int i = 0; i < group.Count; i++) LevelChanged?.Invoke(group[i]);
            return true;
        }
    }
}