using System;
using System.Collections.Generic;
using UnityEngine;

namespace Defense.Units
{
    /// <summary>
    /// 소환 확률 강화 상태. 레벨(1~최고)을 들고 있고, 판이 시작될 때(씬 로드)마다 1로 초기화된다.
    /// SummonManager가 Weights를 읽어 등급을 뽑는다.
    /// </summary>
    public class SummonChanceProgress : MonoBehaviour
    {
        [SerializeField] SummonChanceConfig _config;

        [Tooltip("IWallet을 구현한 컴포넌트(시스템 쪽 골드). 비워두면 무료 강화.")]
        [SerializeField] MonoBehaviour _walletSource;

        [Tooltip("IArtifactEffects를 구현한 컴포넌트(로비 유물). 비워두면 GameManager의 유물을, 그것도 없으면 유물 효과가 없다.")]
        [SerializeField] MonoBehaviour _artifactSource;

        [Tooltip("축복 스킬을 가진 유닛이 서 있는 그리드. 비워두면 축복이 소환 확률에 반영되지 않는다.")]
        [SerializeField] GridField _grid;

        IWallet _wallet;
        IArtifactEffects _artifacts;
        float[] _adjustedWeights; // 유물 보정을 반영한 가중치. 매번 새로 만들지 않고 재사용한다.
        float _blessingShift;     // 필드의 축복 유닛이 일반에서 희귀 이상으로 옮기는 비율의 합. 칸 구성이 바뀔 때마다 다시 센다.

        // 소환 확률 증가 유물이 있는 등급. 전체 가중치의 bonus 비율만큼을 일반에서 그 등급으로 옮긴다.
        static readonly (UnitGrade Grade, ArtifactEffect Effect)[] _summonBonuses =
        {
            (UnitGrade.Rare, ArtifactEffect.RareSummonChance),
            (UnitGrade.Hero, ArtifactEffect.HeroSummonChance),
            (UnitGrade.Legendary, ArtifactEffect.LegendSummonChance)
        };

        /// <summary>소환 확률 레벨이 바뀔 때 호출된다. (UI가 구독)</summary>
        public event Action LevelChanged;

        /// <summary>소환 확률(가중치)이 바뀔 때 호출된다. 레벨이 바뀌었거나 필드의 축복 유닛이 달라졌을 때다. (확률 표시 UI가 구독)</summary>
        public event Action WeightsChanged;

        public int Level { get; private set; } = 1;

        public int MaxLevel => (_config != null) ? _config.MaxLevel : 1;

        public bool IsMaxLevel => Level >= MaxLevel;

        /// <summary>다음 레벨로 올리는 비용. 최고 레벨이거나 설정이 없으면 -1. 유물의 강화 비용 감소가 반영된다.</summary>
        public int NextCost => (_config != null) ? ArtifactMath.ApplyDiscount(_config.GetUpgradeCost(Level), UpgradeDiscount) : -1;

        float UpgradeDiscount => (_artifacts != null) ? Mathf.Max(0f, _artifacts.GetBonus(ArtifactEffect.UpgradeCostDiscount)) : 0f;

        /// <summary>
        /// 현재 레벨의 등급별 소환 가중치에 유물의 '희귀/영웅/전설 소환 확률 증가'와 필드의 축복을 반영한 값. 설정이 없으면 null.
        /// 돌려준 배열은 다음 호출 때 덮어쓰이므로 받아서 바로 쓴다.
        /// </summary>
        public IReadOnlyList<float> Weights => ApplyBonuses((_config != null) ? _config.GetWeights(Level) : null);

        void Awake()
        {
            _wallet = _walletSource as IWallet;
            if (_walletSource != null && _wallet == null)
                Debug.LogError("[SummonChanceProgress] walletSource가 IWallet을 구현하지 않았습니다.", this);

            _artifacts = _artifactSource as IArtifactEffects;
            if (_artifactSource != null && _artifacts == null)
                Debug.LogError("[SummonChanceProgress] artifactSource가 IArtifactEffects를 구현하지 않았습니다.", this);

            // 직접 연결이 없으면 GameManager의 유물을 쓴다. GameManager가 없으면 null이라 유물 효과가 없다.
            if (_artifacts == null) _artifacts = GameModelArtifactEffects.TryCreate();

            if (_grid == null)
                Debug.LogWarning("[SummonChanceProgress] grid가 비어 있어 축복 스킬이 소환 확률에 반영되지 않습니다. GridField를 연결하세요.", this);
        }

        void OnEnable()
        {
            if (_grid == null) return;

            _grid.SlotChanged += OnSlotChanged;
            RefreshBlessing();
        }

        void OnDisable()
        {
            if (_grid != null) _grid.SlotChanged -= OnSlotChanged;
        }

        void OnSlotChanged(Slot changed) => RefreshBlessing();

        // 필드에 있는 모든 유닛의 스킬 중 소환 확률 증가(축복)를 가진 것의 값을 더한다. 칸 구성이 바뀔 때만 다시 센다.
        void RefreshBlessing()
        {
            float shift = 0f;
            IReadOnlyList<Slot> slots = _grid.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                IReadOnlyList<Unit> units = slots[i].Units;
                for (int j = 0; j < units.Count; j++)
                {
                    if (units[j].Data == null) continue;

                    IReadOnlyList<UnitSkill> skills = units[j].Data.Skills;
                    for (int k = 0; k < skills.Count; k++)
                    {
                        if (skills[k] is ISummonChanceBonus bonus) shift += bonus.RareOrHigherShift;
                    }
                }
            }

            if (Mathf.Approximately(shift, _blessingShift)) return;

            _blessingShift = shift;
            WeightsChanged?.Invoke();
        }

        // 전체 가중치의 bonus 비율만큼을 일반에서 해당 등급으로 옮긴다. (일반 소환에서 희귀/영웅/전설이 나올 확률 증가)
        // 일반에서 옮길 수 있는 만큼만 옮기므로, 여러 등급의 유물이 겹쳐도 일반 가중치가 음수가 되지 않는다.
        // 필드의 축복(희귀 이상 소환 확률 증가)은 유물 보정 뒤에 같은 방식으로 옮기되, 희귀 이상 등급의 현재 비율대로 나눠 준다.
        IReadOnlyList<float> ApplyBonuses(IReadOnlyList<float> weights)
        {
            int common = (int)UnitGrade.Common;
            if ((_artifacts == null && _blessingShift <= 0f) || weights == null || weights.Count <= common) return weights;

            float total = 0f;
            for (int i = 0; i < weights.Count; i++) total += Mathf.Max(0f, weights[i]);

            if (_adjustedWeights == null || _adjustedWeights.Length != weights.Count)
                _adjustedWeights = new float[weights.Count];
            for (int i = 0; i < weights.Count; i++) _adjustedWeights[i] = weights[i];

            float movable = Mathf.Max(0f, weights[common]);
            bool isShifted = false;
            for (int i = 0; i < _summonBonuses.Length; i++)
            {
                int index = (int)_summonBonuses[i].Grade;
                if (index >= weights.Count) continue;

                float bonus = (_artifacts != null) ? _artifacts.GetBonus(_summonBonuses[i].Effect) : 0f;
                if (bonus <= 0f) continue;

                float shift = Mathf.Min(bonus * total, movable);
                _adjustedWeights[common] -= shift;
                _adjustedWeights[index] += shift;
                movable -= shift;
                isShifted = true;
            }

            if (_blessingShift > 0f && movable > 0f)
            {
                float higherTotal = 0f;
                for (int i = common + 1; i < weights.Count; i++) higherTotal += Mathf.Max(0f, _adjustedWeights[i]);

                if (higherTotal > 0f)
                {
                    float shift = Mathf.Min(_blessingShift * total, movable);
                    _adjustedWeights[common] -= shift;
                    for (int i = common + 1; i < weights.Count; i++)
                        _adjustedWeights[i] += shift * Mathf.Max(0f, _adjustedWeights[i]) / higherTotal;
                    isShifted = true;
                }
            }

            // 옮긴 것이 없으면 원본을 그대로 돌려준다.
            return (isShifted) ? _adjustedWeights : weights;
        }

        /// <summary>소환 확률을 한 단계 강화한다. 최고 레벨이거나 골드가 부족하면 false.</summary>
        public bool TryUpgrade()
        {
            if (_config == null || IsMaxLevel) return false;

            int cost = NextCost;
            if (cost < 0) return false;
            if (_wallet != null && _wallet.TrySpend(cost) == false) return false;

            Level++;
            SoundManager.Play(GameSfx.Upgrade);
            LevelChanged?.Invoke();
            WeightsChanged?.Invoke();
            return true;
        }
    }
}