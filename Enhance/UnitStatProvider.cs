using System.Collections.Generic;
using UnityEngine;

namespace Defense.Units
{
    /// <summary>
    /// 기본 스탯 + 로비 강화(유닛 종류별) + 인게임 강화(등급별)를 합산한다.
    /// 합산한 결과에 유물의 공격력/공격속도 증가 비율을 곱한다.
    /// </summary>
    public class UnitStatProvider : MonoBehaviour, IUnitStatProvider
    {
        [SerializeField] GradeEnhanceConfig _enhanceConfig;
        [SerializeField] InGameProgress _inGameProgress;

        [Tooltip("ILobbyProgress를 구현한 컴포넌트. 비워두면 GameManager의 저장 데이터를, 그것도 없으면 PlayerPrefs 기반 임시 구현을 쓴다.")]
        [SerializeField] MonoBehaviour _lobbyProgressSource;

        [Tooltip("IArtifactEffects를 구현한 컴포넌트(로비 유물). 비워두면 GameManager의 유물을, 그것도 없으면 유물 효과가 없다.")]
        [SerializeField] MonoBehaviour _artifactSource;

        ILobbyProgress _lobbyProgress;
        IArtifactEffects _artifacts;

        // 유물 효과는 판 도중에 바뀌지 않으므로 한 번 읽어서 캐시한다.
        float _attackPowerRate;
        float _attackSpeedRate;
        float _moveSpeedRate;
        float _skillDamageRate;
        float _bossDamageRate;
        float _skillGoldChance;
        readonly Dictionary<UnitData, int> _lobbyLevels = new Dictionary<UnitData, int>();

        void Awake()
        {
            _lobbyProgress = _lobbyProgressSource as ILobbyProgress;
            if (_lobbyProgressSource != null && _lobbyProgress == null)
                Debug.LogError("[UnitStatProvider] lobbyProgressSource가 ILobbyProgress를 구현하지 않았습니다.", this);

            // 직접 연결이 없으면 GameManager(로비 저장 데이터)를 쓰고, GameManager가 없으면(전투 씬만 실행) 임시 구현을 쓴다.
            if (_lobbyProgress == null) _lobbyProgress = GameModelLobbyProgress.TryCreate() ?? new PlayerPrefsLobbyProgress();

            _artifacts = _artifactSource as IArtifactEffects;
            if (_artifactSource != null && _artifacts == null)
                Debug.LogError("[UnitStatProvider] artifactSource가 IArtifactEffects를 구현하지 않았습니다.", this);

            // 직접 연결이 없으면 GameManager의 유물을 쓴다. GameManager가 없으면 null이라 유물 효과가 없다.
            if (_artifacts == null) _artifacts = GameModelArtifactEffects.TryCreate();
        }

        // 유물 쪽이 Awake에서 준비되는 경우를 위해, 모든 Awake가 끝난 Start에서 읽는다.
        void Start() => RefreshArtifacts();

        /// <summary>유닛 이동속도 배율. 유물의 이동속도 증가가 반영된다.</summary>
        public float MoveSpeedMultiplier => 1f + _moveSpeedRate;

        /// <summary>스킬 피해 배율. 유물의 스킬 데미지 증가가 반영된다.</summary>
        public float SkillDamageMultiplier => 1f + _skillDamageRate;

        /// <summary>보스 대미지 배율. 유물의 보스 데미지 증가가 반영된다.</summary>
        public float BossDamageMultiplier => 1f + _bossDamageRate;

        /// <summary>스킬 발동 시 코인 1개를 추가로 얻을 확률. 유물의 스킬 골드 획득 확률이 반영된다.</summary>
        public float SkillGoldChance => _skillGoldChance;

        /// <summary>유물 효과가 바뀐 직후 다시 읽고 싶을 때 호출.</summary>
        public void RefreshArtifacts()
        {
            _attackPowerRate = (_artifacts != null) ? _artifacts.GetBonus(ArtifactEffect.AttackPower) : 0f;
            _attackSpeedRate = (_artifacts != null) ? _artifacts.GetBonus(ArtifactEffect.AttackSpeed) : 0f;
            _moveSpeedRate = (_artifacts != null) ? _artifacts.GetBonus(ArtifactEffect.MoveSpeed) : 0f;
            _skillDamageRate = (_artifacts != null) ? Mathf.Max(0f, _artifacts.GetBonus(ArtifactEffect.SkillDamage)) : 0f;
            _bossDamageRate = (_artifacts != null) ? Mathf.Max(0f, _artifacts.GetBonus(ArtifactEffect.BossDamage)) : 0f;
            _skillGoldChance = (_artifacts != null) ? Mathf.Clamp01(_artifacts.GetBonus(ArtifactEffect.SkillGoldChance)) : 0f;
        }

        public UnitStats GetStats(UnitData data)
        {
            UnitStats stats = data.BaseStats;
            stats += data.GetLobbyBonus(GetLobbyLevel(data));

            if (_enhanceConfig != null && _inGameProgress != null)
                stats += _enhanceConfig.GetBonus(data.Grade, _inGameProgress.GetLevel(data.Grade));

            // 유물은 모든 강화를 합친 값에 비율로 곱한다. (사거리는 그대로)
            return new UnitStats(
                stats.AttackPower * (1f + _attackPowerRate),
                stats.AttackSpeed * (1f + _attackSpeedRate),
                stats.AttackRange);
        }

        // 로비 레벨은 판 도중에 바뀌지 않으므로 한 번 읽어서 캐시한다.
        int GetLobbyLevel(UnitData data)
        {
            if (_lobbyLevels.TryGetValue(data, out int level) == false)
            {
                level = (_lobbyProgress != null) ? _lobbyProgress.GetLevel(data) : 1;
                _lobbyLevels[data] = level;
            }
            return level;
        }

        /// <summary>같은 씬에서 로비 레벨이 바뀐 직후 다시 읽고 싶을 때 호출.</summary>
        public void RefreshLobbyLevels() => _lobbyLevels.Clear();
    }
}