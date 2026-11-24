using System.Collections.Generic;
using UnityEngine;

namespace Defense.Units
{
    /// <summary>유닛 한 종류의 데이터. Project 창에서 Create > Defense > Unit Data 로 생성한다.</summary>
    [CreateAssetMenu(fileName = "Unit_", menuName = "Defense/Unit Data")]
    public class UnitData : ScriptableObject
    {
        /// <summary>등급 기본 칸 외에 더 줄 수 있는 스킬 칸의 최대 개수.</summary>
        public const int MAX_EXTRA_SKILL_SLOTS = 1;

        [Header("기본 정보")]
        [Tooltip("로비 레벨 저장 키로 쓰인다. 유닛마다 달라야 하고, 한 번 정하면 바꾸지 말 것. 비워두면 에셋 이름으로 채워진다.")]
        [SerializeField] string _id;
        [SerializeField] string _displayName;
        [SerializeField] UnitGrade _grade;
        [Tooltip("필드에 소환될 3D 모델 프리팹")]
        [SerializeField] GameObject _prefab;
        [Tooltip("UI에 표시할 아이콘")]
        [SerializeField] Sprite _icon;

        [Header("기본 스탯 (레벨 1)")]
        [Min(0f)][SerializeField] float _attackPower = 10f;
        [Tooltip("초당 공격 횟수. 공격 간격 = 1 / attackSpeed")]
        [Min(0.01f)][SerializeField] float _attackSpeed = 1f;
        [Tooltip("공격범위. 유닛 위치 기준 XZ 평면 거리(월드 단위)")]
        [Min(0f)][SerializeField] float _attackRange = 5f;
        [Tooltip("근거리/원거리. 유닛 정보창에 표시한다.")]
        [SerializeField] UnitAttackRangeType _attackRangeType = UnitAttackRangeType.Short;

        [Header("투사체")]
        [Tooltip("공격할 때 날려 보낼 투사체 프리팹. 비워두면 투사체 없이 즉시 대미지를 준다. (궁수, 석궁병 등 원거리 유닛용)")]
        [SerializeField] Projectile _projectilePrefab;

        [Header("공격 애니메이션")]
        [Tooltip("켜면 스킬·대미지·투사체가 공격을 시작하는 순간이 아니라 공격 클립의 애니메이션 이벤트(OnAttackRelease, 발사 프레임)에서 나간다. 끄면 기존처럼 공격하는 순간 바로 나간다.")]
        [SerializeField] bool _releaseOnAnimationEvent;

        [Header("로비 강화")]
        [Tooltip("로비 레벨이 1 오를 때마다 더해지는 공격력. 레벨 1은 보너스 0, 레벨 N은 (N-1)배.")]
        [Min(0f)][SerializeField] float _lobbyAttackPowerPerLevel;

        [Header("스킬 (패시브)")]
        [Tooltip("일반/희귀는 1개, 영웅/전설은 2개. 추가 칸이 있는 유닛은 1개 더. 위에서부터 순서대로 실행된다.")]
        [SerializeField] UnitSkill[] _skills = new UnitSkill[0];
        [Tooltip("등급 기본 칸 외에 더 가질 수 있는 스킬 칸. 일부 유닛만 3번째 스킬을 가질 때 쓴다. (스킬 표 가져오기가 표에 맞춰 채운다)")]
        [Range(0, MAX_EXTRA_SKILL_SLOTS)][SerializeField] int _extraSkillSlots;

        [Header("유닛 프리뷰")]
        [Tooltip("유닛 프리뷰 카메라가 유닛을 잡는 중심과 크기. Tools > Unit > Unit Preview Framing Baker로 채운다. 비어 있으면 프리뷰가 선택한 순간의 자세로 직접 잰다.")]
        [SerializeField] UnitPreviewFraming _previewFraming = new UnitPreviewFraming { DistanceScale = 1f };

        public string Id => _id;
        public string DisplayName => _displayName;
        public float AttackPower => _attackPower;
        public float AttackSpeed => _attackSpeed;
        public float LobbyAttackPowerPerLevel => _lobbyAttackPowerPerLevel;
        public UnitGrade Grade => _grade;
        public GameObject Prefab => _prefab;
        public Sprite Icon => _icon;
        public Projectile ProjectilePrefab => _projectilePrefab;
        public bool ReleaseOnAnimationEvent => _releaseOnAnimationEvent;
        public UnitAttackRangeType AttackRangeType => _attackRangeType;
        public IReadOnlyList<UnitSkill> Skills => _skills;
        public UnitPreviewFraming PreviewFraming => _previewFraming;

        /// <summary>이 유닛이 가질 수 있는 스킬 칸 수. 등급 기본 칸 + 추가 칸.</summary>
        public int SkillSlotCount => _grade.SkillSlotCount() + _extraSkillSlots;

        /// <summary>레벨 보너스가 없는 기본 스탯.</summary>
        public UnitStats BaseStats => new UnitStats(_attackPower, _attackSpeed, _attackRange);

        /// <summary>로비 레벨에 해당하는 누적 보너스. 레벨은 1~15로 보정.</summary>
        /// <param name="level">레벨</param>
        /// <returns>StatBonus 객체로 반환</returns>
        public StatBonus GetLobbyBonus(int level) => new StatBonus
        {
            // 현재 공격력만 고정 수치로 올라감
            AttackPower = _lobbyAttackPowerPerLevel * (UnitLevel.Clamp(level) - 1)
        };

        void OnValidate()
        {
            if (string.IsNullOrEmpty(_id)) _id = name;

            int skillSlots = SkillSlotCount;
            if (_skills != null && _skills.Length > skillSlots)
                Debug.LogWarning($"[UnitData] {name}: 이 유닛의 스킬은 최대 {skillSlots}개입니다. ({_grade} 등급 기본 {_grade.SkillSlotCount()}개 + 추가 {_extraSkillSlots}개)", this);
        }
    }
}