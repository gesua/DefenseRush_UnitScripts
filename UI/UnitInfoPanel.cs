using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Defense.Units
{
    /// <summary>
    /// 유닛 정보창. 받은 정보를 글자와 색으로 보여주기만 한다. (무엇을 보여줄지는 UnitInfoPresenter가 정한다)
    /// 루트가 항상 켜져 있어야 하므로 SetActive 대신 CanvasGroup으로 보이고 숨긴다.
    /// 유닛 카드는 MainScene 유닛 슬롯과 같은 구조(등급별 배경 → Card 모양 마스크 → 캐릭터 이미지 → 하단 그림자)이다.
    /// 카드 틀(GradeFrame)은 프리팹에서 정한 시안 비율(세로가 긴 카드 + 그림자)을 그대로 쓰고, _squareCardFrame을 켠 경우에만 세로 길이는 그대로 두고 가로만 넓혀 1:1 정사각형으로 맞춘다. (배경, 마스크, 그림자가 함께 넓어진다)
    /// 유닛 초상화는 UnitData.Icon을 PortraitMask 안에 보여주고, 아이콘이 없는 유닛은 임시 표시(PortraitPlaceholder)를 대신 보여준다.
    /// 스킬 아이콘은 UnitData.Skills 순서대로 공격 방식 텍스트 왼쪽에 왼쪽에서 오른쪽으로 나란히 배치하고, 선택한 스킬의 이름과 설명을 표시한다.
    /// 오라/일시 버프로 오른 수치는 공격력/공격속도 줄 오른쪽의 고정 칸에 따로 보여주고, 버프가 끝나면 서서히 지운다.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class UnitInfoPanel : MonoBehaviour
    {
        [Header("프로필")]
        [SerializeField] TMP_Text _nameText;
        [Tooltip("등급 이름. (종족은 아직 데이터가 없어서 표시하지 않는다)")]
        [SerializeField] TMP_Text _gradeText;
        [SerializeField] TMP_Text _levelText;
        [Tooltip("등급별 카드 배경을 보여줄 이미지. 비워두면 이름이 CardBackground인 오브젝트를 찾는다.")]
        [SerializeField] Image _cardBackground;
        [Tooltip("등급 순서(일반, 희귀, 영웅, 전설)대로의 카드 배경. MainScene 유닛 슬롯의 card_background_* 와 같은 스프라이트를 쓴다.")]
        [SerializeField] Sprite[] _gradeBackgrounds;
        [Tooltip("켜면 카드 틀(GradeFrame)을 세로 길이는 그대로 두고 가로만 넓혀 1:1 정사각형으로 맞춘다. 꺼두면 프리팹에서 정한 카드 틀 크기를 그대로 쓴다. 카드 배경의 부모를 카드 틀로 본다.")]
        [SerializeField] bool _squareCardFrame = false;
        [Tooltip("유닛 초상화 이미지. PortraitMask(Mask) 안에 있으며 UnitData.Icon을 보여준다. 비워두면 이름이 PortraitIcon인 오브젝트를 찾는다.")]
        [SerializeField] Image _portraitIcon;
        [Tooltip("아이콘이 없는 유닛에 보여줄 임시 표시(방패와 검). 아이콘이 있으면 숨긴다. 비워두면 이름이 PortraitPlaceholder인 오브젝트를 찾는다.")]
        [SerializeField] GameObject _portraitPlaceholder;

        [Header("스탯")]
        [SerializeField] TMP_Text _attackPowerText;
        [SerializeField] TMP_Text _attackSpeedText;
        [Tooltip("근거리/원거리")]
        [SerializeField] TMP_Text _attackTypeText;

        [Header("스킬")]
        [Tooltip("기존 SkillBadge 오브젝트를 아이콘 템플릿으로 사용합니다. 이름/설명 텍스트는 SkillPanel 안의 SkillNameText, SkillDescriptionText를 자동으로 찾습니다.")]
        [SerializeField] Image _skillIconTemplate;
        [SerializeField] TMP_Text _skillNameText;
        [SerializeField] TMP_Text _skillDescriptionText;
        [Tooltip("스킬 아이콘 가로 줄. 비워두면 이름이 SkillIconRow(이전 이름 SkillIconColumn)인 오브젝트를 찾는다.")]
        [FormerlySerializedAs("_skillIconColumn")]
        [SerializeField] RectTransform _skillIconRow;
        [Tooltip("스킬 아이콘 한 칸의 크기(캔버스 단위)")]
        [SerializeField, Min(1f)] float _skillIconSize = 100f;
        [Tooltip("스킬 아이콘 사이의 간격(캔버스 단위)")]
        [SerializeField, Min(0f)] float _skillIconSpacing = 4f;

        [Header("강화 증가량")]
        [SerializeField] TMP_Text _attackPowerIncreaseText;
        [SerializeField] TMP_Text _attackSpeedIncreaseText;

        [Header("버프 증가량")]
        [Tooltip("오라/일시 버프로 오른 공격력. 줄 오른쪽의 고정 칸이라, 나타나고 사라져도 다른 글자가 움직이지 않는다.")]
        [SerializeField] TMP_Text _attackPowerBuffText;
        [Tooltip("오라/일시 버프로 오른 공격속도. 줄 오른쪽의 고정 칸이라, 나타나고 사라져도 다른 글자가 움직이지 않는다.")]
        [SerializeField] TMP_Text _attackSpeedBuffText;
        [Tooltip("버프가 끝났을 때 증가량이 서서히 사라지는 데 걸리는 시간(초)")]
        [Min(0f)][SerializeField] float _buffFadeTime = 0.3f;

        [Header("표시")]
        [Tooltip("등급 순서(일반, 희귀, 영웅, 전설)대로의 이름")]
        [SerializeField] string[] _gradeNames = { "일반", "희귀", "영웅", "전설" };
        [Tooltip("공격 방식 순서(근거리, 원거리)대로의 이름")]
        [SerializeField] string[] _attackTypeNames = { "근거리", "원거리" };

        // 스킬 아이콘 가로 줄과 아이콘 칸의 기준점. 앵커와 피벗을 왼쪽 위로 맞추면 위치 값이 왼쪽 위 모서리 기준이 된다.
        static readonly Vector2 _topLeft = new Vector2(0f, 1f);

        CanvasGroup _group;
        RectTransform _rect;
        GameObject _skillIconTemplateObject;
        Sprite _defaultSkillIconSprite;
        Color _defaultSkillIconColor;
        Image.Type _defaultSkillIconType;
        readonly List<SkillIconView> _skillIconViews = new List<SkillIconView>();
        readonly List<UnitSkill> _shownSkills = new List<UnitSkill>();
        IReadOnlyList<UnitSkill> _currentSkills;
        int _selectedSkillIndex = -1;
        BuffLabel _attackPowerBuff;
        BuffLabel _attackSpeedBuff;

        void Awake()
        {
            TryGetComponent(out _group);
            TryGetComponent(out _rect);
            FindUIReferences();
            FitCardFrameToSquare();
            _attackPowerBuff = new BuffLabel(_attackPowerBuffText, "0.#");
            _attackSpeedBuff = new BuffLabel(_attackSpeedBuffText, "0.0#");
            Hide();
        }

        /// <summary>정보를 채우고 보여준다. level은 인게임 강화 레벨.</summary>
        public void Show(UnitData data, UnitStats stats, int level, StatBonus nextLevelIncrease, bool hasNextLevel)
        {
            if (data == null) return;

            SetText(_nameText, data.DisplayName);
            SetText(_gradeText, GetName(_gradeNames, (int)data.Grade, data.Grade.ToString()));
            SetText(_levelText, $"Lv.{level}");

            SetText(_attackPowerText, stats.AttackPower.ToString("0.#", CultureInfo.InvariantCulture));
            SetText(_attackSpeedText, stats.AttackSpeed.ToString("0.0#", CultureInfo.InvariantCulture));

            SetEnhancementText(_attackPowerIncreaseText, nextLevelIncrease.AttackPower, hasNextLevel);
            SetEnhancementText(_attackSpeedIncreaseText, nextLevelIncrease.AttackSpeed, hasNextLevel);

            SetText(_attackTypeText, GetName(_attackTypeNames, (int)data.AttackRangeType, data.AttackRangeType.ToString()));

            SetCardBackground(data.Grade);
            FitCardFrameToSquare();
            SetPortrait(data.Icon);

            ShowSkills(data.Skills);

            SetVisible(true);
        }

        // 숨길 때 보던 스킬과 버프 표시를 잊어서, 다음에 열면 첫 스킬부터 보여주고 버프 증가량은 비운 채 시작한다.
        public void Hide()
        {
            _currentSkills = null;
            ClearBuff();
            SetVisible(false);
        }

        /// <summary>
        /// 정보창이 보이는 상태에서 화면 좌표가 정보창 영역 안인지 알려준다.
        /// 강화·도박 패널의 GridRaycastPassThrough가 정보창 위의 입력(스킬 아이콘 등)을 통과시킬지 정할 때 쓴다.
        /// 숨겨진 동안에는 항상 false라서, 패널이 열려 있어도 보이지 않는 정보창 자리는 기존처럼 입력을 막는다.
        /// </summary>
        public bool ContainsScreenPoint(Vector2 screenPoint, Camera eventCamera)
        {
            if (_rect == null || _group == null || _group.blocksRaycasts == false) return false;

            return RectTransformUtility.RectangleContainsScreenPoint(_rect, screenPoint, eventCamera);
        }

        /// <summary>
        /// 버프로 오른 공격력/공격속도를 오른쪽 고정 칸에 보여준다. 정보창이 열려 있는 동안 매 프레임 불러도 된다.
        /// 0 이하면 버프가 끝난 것으로 보고, 마지막 값을 남긴 채 서서히 지운다.
        /// </summary>
        public void SetBuff(float attackPower, float attackSpeed)
        {
            if (_attackPowerBuff == null || _attackSpeedBuff == null) return;

            float deltaTime = Time.unscaledDeltaTime;
            _attackPowerBuff.Apply(attackPower, _buffFadeTime, deltaTime);
            _attackSpeedBuff.Apply(attackSpeed, _buffFadeTime, deltaTime);
        }

        /// <summary>버프 증가량을 바로 지운다. (다른 유닛으로 바뀌었을 때 이전 유닛의 값이 서서히 사라지는 모습이 보이지 않게 한다)</summary>
        public void ClearBuff()
        {
            if (_attackPowerBuff != null) _attackPowerBuff.Clear();
            if (_attackSpeedBuff != null) _attackSpeedBuff.Clear();
        }

        static void SetEnhancementText(TMP_Text label, float increase, bool hasNextLevel)
        {
            if (label == null) return;

            label.gameObject.SetActive(hasNextLevel);
            if (hasNextLevel == false) return;

            string prefix = (increase >= 0f) ? "+" : string.Empty;
            label.text = $"{prefix}{increase.ToString("0.##", CultureInfo.InvariantCulture)}";
        }

        // 직접 연결한 참조를 먼저 쓰고, 비어 있는 것만 이름으로 찾는다.
        void FindUIReferences()
        {
            Transform[] transforms = GetComponentsInChildren<Transform>(true);
            Transform skillIconRow = null;
            Transform skillName = null;
            Transform skillDescription = null;
            GameObject skillBadge = null;
            Transform cardBackground = null;
            Transform portraitIcon = null;
            Transform portraitPlaceholder = null;

            foreach (Transform item in transforms)
            {
                if (item.name == "SkillIconRow" || item.name == "SkillIconColumn") skillIconRow = item; // 이전 이름(SkillIconColumn)도 함께 찾는다.
                else if (item.name == "SkillNameText") skillName = item;
                else if (item.name == "SkillDescriptionText") skillDescription = item;
                else if (item.name == "SkillBadge") skillBadge = item.gameObject;
                else if (item.name == "CardBackground") cardBackground = item;
                else if (item.name == "PortraitIcon") portraitIcon = item;
                else if (item.name == "PortraitPlaceholder") portraitPlaceholder = item;
            }

            if (_cardBackground == null && cardBackground != null)
                cardBackground.TryGetComponent(out _cardBackground);

            if (_portraitIcon == null && portraitIcon != null)
                portraitIcon.TryGetComponent(out _portraitIcon);

            if (_portraitPlaceholder == null && portraitPlaceholder != null)
                _portraitPlaceholder = portraitPlaceholder.gameObject;

            if (_skillIconTemplate == null && skillBadge != null)
                skillBadge.TryGetComponent(out _skillIconTemplate);

            if (_skillNameText == null && skillName != null)
                skillName.TryGetComponent(out _skillNameText);

            if (_skillDescriptionText == null && skillDescription != null)
                skillDescription.TryGetComponent(out _skillDescriptionText);

            if (_skillIconRow == null) _skillIconRow = skillIconRow as RectTransform;
            _skillIconTemplateObject = (_skillIconTemplate != null) ? _skillIconTemplate.gameObject : null;

            if (_skillIconTemplateObject == null || _skillIconRow == null)
            {
                Debug.LogWarning("[UnitInfoPanel] 스킬 아이콘 템플릿(SkillBadge)이나 가로 줄(SkillIconRow)을 찾지 못해 스킬 아이콘을 표시하지 않습니다.", this);
                return;
            }

            // 스킬 아이콘은 공격 방식 버튼과 분리된 별도 레이아웃에 배치한다.
            _skillIconTemplateObject.transform.SetParent(_skillIconRow, false);
            _skillIconTemplateObject.name = "SkillIcon_0";

            // 아이콘은 가로 줄의 왼쪽 위 모서리에서부터 오른쪽으로 나란히 놓는다. 가로 줄의 크기가 바뀌어도 그 모서리(첫 아이콘 자리)가 움직이지 않도록 피벗을 왼쪽 위로 옮긴다. (가로 줄의 현재 위치는 그대로 유지한다)
            SetPivotKeepingRect(_skillIconRow, _topLeft);

            // 아이콘 이미지가 없는 스킬은 템플릿의 원래 배경으로 되돌려야 하므로, 바꾸기 전에 기억해 둔다.
            _defaultSkillIconSprite = _skillIconTemplate.sprite;
            _defaultSkillIconColor = _skillIconTemplate.color;
            _defaultSkillIconType = _skillIconTemplate.type;
        }

        // 등급에 맞는 카드 배경으로 바꾼다. 등급 수보다 배열이 짧거나 비어 있으면 현재 배경을 그대로 둔다.
        void SetCardBackground(UnitGrade grade)
        {
            if (_cardBackground == null || _gradeBackgrounds == null) return;

            int index = (int)grade;
            if (index < 0 || index >= _gradeBackgrounds.Length || _gradeBackgrounds[index] == null) return;

            _cardBackground.sprite = _gradeBackgrounds[index];
        }

        // 카드 틀을 세로 길이 그대로 두고 가로만 넓혀 1:1 정사각형으로 만든다.
        // 배경·마스크·그림자는 카드 틀에 stretch로 붙어 있어서 틀만 넓히면 함께 넓어지고, 캐릭터 이미지는 비율 유지로 늘어나지 않는다.
        // 이미 정사각형이면 바로 끝나므로, 여러 번 불러도 결과가 같다.
        void FitCardFrameToSquare()
        {
            if (_squareCardFrame == false || _cardBackground == null) return;

            RectTransform frame = _cardBackground.rectTransform.parent as RectTransform;
            if (frame == null) return;

            float side = frame.rect.height;
            float delta = side - frame.rect.width;
            if (side <= 0f || Mathf.Approximately(delta, 0f)) return;

            frame.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, side);
        }

        // 아이콘이 있으면 초상화를 보여주고 임시 표시는 숨긴다. 아이콘이 없는 유닛은 이미지를 끄고 임시 표시로 되돌린다.
        void SetPortrait(Sprite icon)
        {
            bool hasIcon = (icon != null);

            if (_portraitIcon != null)
            {
                _portraitIcon.sprite = icon;
                _portraitIcon.enabled = hasIcon;
            }

            if (_portraitPlaceholder != null) _portraitPlaceholder.SetActive(hasIcon == false);
        }

        // 스킬 아이콘을 UnitData.Skills 순서대로 배치한다. 아이콘 수가 늘어나면 첫 아이콘 오른쪽으로 이어서 나란히 놓는다.
        void ShowSkills(IReadOnlyList<UnitSkill> skills)
        {
            if (_skillIconTemplateObject == null || _skillIconRow == null)
            {
                SetText(_skillNameText, string.Empty);
                SetText(_skillDescriptionText, string.Empty);
                return;
            }

            // 같은 유닛을 다시 그리는 경우(인게임 강화, 칸 변화 등)에는 아이콘을 새로 만들지 않고, 보고 있던 스킬을 그대로 둔다.
            if (_currentSkills != null && ReferenceEquals(skills, _currentSkills)) return;

            _currentSkills = skills;
            _selectedSkillIndex = -1;
            _shownSkills.Clear();
            if (skills != null)
            {
                for (int i = 0; i < skills.Count; i++)
                {
                    if (skills[i] != null) _shownSkills.Add(skills[i]);
                }
            }

            // 필요한 수만큼 아이콘을 준비한다. 부족하면 템플릿을 복제하고, 한 번 만든 아이콘은 계속 재사용한다.
            for (int i = 0; i < _shownSkills.Count; i++)
            {
                if (i >= _skillIconViews.Count) _skillIconViews.Add(CreateSkillIconView(i));

                ConfigureSkillIcon(_skillIconViews[i], _shownSkills[i]);
                _skillIconViews[i].Root.SetActive(true);
            }

            for (int i = _shownSkills.Count; i < _skillIconViews.Count; i++)
                _skillIconViews[i].Root.SetActive(false);

            // 템플릿은 복제 원본으로 유지하며, 스킬이 없는 경우에는 감춘다.
            if (_shownSkills.Count == 0)
            {
                _skillIconTemplateObject.SetActive(false);
                SetText(_skillNameText, string.Empty);
                SetText(_skillDescriptionText, string.Empty);
            }
            else
            {
                SelectSkill(0);
            }

            LayoutSkillIcons();
        }

        // 아이콘 칸 하나를 만든다. 첫 칸은 템플릿 자신을 쓰고, 나머지는 템플릿을 복제한다.
        // 크기, 클릭, 선택 테두리처럼 칸마다 한 번만 정하면 되는 것은 여기서 정하고, 스킬마다 달라지는 것은 ConfigureSkillIcon이 맞춘다.
        SkillIconView CreateSkillIconView(int index)
        {
            GameObject iconObject = (index == 0) ? _skillIconTemplateObject : Instantiate(_skillIconTemplateObject, _skillIconRow, false);
            iconObject.name = $"SkillIcon_{index}";

            // 가로 줄의 왼쪽 위 모서리를 기준으로 나란히 놓으므로 앵커와 피벗을 왼쪽 위로 정한다. 위치는 LayoutSkillIcons가 정한다.
            RectTransform rect = (RectTransform)iconObject.transform;
            rect.anchorMin = _topLeft;
            rect.anchorMax = _topLeft;
            rect.pivot = _topLeft;
            rect.sizeDelta = new Vector2(_skillIconSize, _skillIconSize);

            iconObject.TryGetComponent(out Image iconImage);
            if (iconImage != null)
            {
                iconImage.raycastTarget = true;
            }

            if (iconObject.TryGetComponent(out Button button) == false)
                button = iconObject.AddComponent<Button>();

            button.targetGraphic = iconImage;
            button.onClick.RemoveAllListeners();
            int capturedIndex = index;
            button.onClick.AddListener(() => SelectSkill(capturedIndex));

            if (iconObject.TryGetComponent(out Outline outline) == false)
                outline = iconObject.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.78f, 0.25f, 1f);
            outline.effectDistance = new Vector2(2f, 2f);
            outline.enabled = false;

            // 칸이 작아서 글자는 칸 크기에 맞춰 고정한다.
            TMP_Text label = iconObject.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.enableAutoSizing = false;
                label.alignment = TextAlignmentOptions.Center;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.overflowMode = TextOverflowModes.Ellipsis;
                label.raycastTarget = false;

                RectTransform labelRect = label.rectTransform;
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = Vector2.zero;
                labelRect.offsetMax = Vector2.zero;
            }

            return new SkillIconView(iconObject, rect, button, iconImage, outline, label);
        }

        void ConfigureSkillIcon(SkillIconView view, UnitSkill skill)
        {
            // 아직 스킬 아이콘 이미지가 없는 경우에는 배지 안에 스킬 이름을 표시한다.
            if (view.Label != null)
            {
                view.Label.text = skill.DisplayName.Replace(' ', '\n');
                view.Label.gameObject.SetActive(skill.Icon == null);
            }

            if (view.Image != null)
            {
                // 아이콘 스프라이트가 준비된 스킬만 이미지를 교체하고, 없으면 기존 배지 배경을 유지한다.
                bool hasIcon = skill.Icon != null;
                view.Image.sprite = (hasIcon) ? skill.Icon : _defaultSkillIconSprite;
                view.Image.color = (hasIcon) ? Color.white : _defaultSkillIconColor;

                // 배지 배경은 9-slice(Sliced)라서 아이콘 비율 유지가 먹지 않는다. 아이콘이 있을 때만 Simple + 비율 유지로 바꾼다.
                view.Image.type = (hasIcon) ? Image.Type.Simple : _defaultSkillIconType;
                view.Image.preserveAspect = hasIcon;
            }
        }

        void SelectSkill(int index)
        {
            if (index < 0 || index >= _shownSkills.Count) return;

            UnitSkill skill = _shownSkills[index];
            _selectedSkillIndex = index;
            SetText(_skillNameText, skill.DisplayName);
            SetText(_skillDescriptionText, skill.Description);

            for (int i = 0; i < _shownSkills.Count; i++)
            {
                Outline outline = _skillIconViews[i].Outline;
                if (outline != null) outline.enabled = (i == _selectedSkillIndex);
            }
        }

        void LayoutSkillIcons()
        {
            int count = _shownSkills.Count;

            // 가로 줄의 크기를 아이콘이 차지하는 영역에 맞춘다. 아이콘을 가로로 나란히 놓으므로 높이는 아이콘 한 칸으로 고정하고, 폭만 아이콘 수에 따라 오른쪽으로 늘린다.
            // (높이가 아이콘 수에 따라 변하면 가로 줄의 위쪽 끝이 움직여서, 왼쪽 위 모서리에 붙은 아이콘이 전부 함께 밀린다)
            // 가로 줄의 피벗이 왼쪽 위라서 크기가 바뀌어도 왼쪽 위 모서리(첫 아이콘 자리)는 그대로이다. 가로 줄의 위치는 프리팹에서 AttackTypeButton의 세로 가운데에 맞춰 두었다.
            float iconRowWidth = (count > 0) ? count * _skillIconSize + (count - 1) * _skillIconSpacing : 0f;
            _skillIconRow.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, iconRowWidth);
            _skillIconRow.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _skillIconSize);

            // 아이콘 수만큼 오른쪽으로 확장한다.
            // 앵커 (0,1), 피벗 (0,1) 기준이므로 첫 아이콘은 (0,0)이고, 이후 아이콘은 x가 양수 방향으로 이어진다. (앵커와 피벗은 CreateSkillIconView에서 정한다)
            for (int i = 0; i < count; i++)
            {
                float x = i * (_skillIconSize + _skillIconSpacing);
                _skillIconViews[i].Rect.anchoredPosition = new Vector2(x, 0f);
            }
        }

        // 숨겨진 동안에는 알파 0, 터치도 막지 않아서 뒤의 그리드 입력을 가리지 않는다.
        void SetVisible(bool visible)
        {
            if (_group == null) return;

            _group.alpha = (visible) ? 1f : 0f;
            _group.interactable = visible;
            _group.blocksRaycasts = visible;
        }

        static void SetText(TMP_Text label, string text)
        {
            if (label != null) label.text = text;
        }

        static string GetName(string[] names, int index, string fallback) =>
            (names != null && index < names.Length) ? names[index] : fallback;

        // 피벗을 바꿔도 사각형이 화면에서 움직이지 않도록 anchoredPosition을 함께 보정한다.
        // anchoredPosition은 피벗의 위치이므로, 피벗이 사각형 안에서 옮겨 간 만큼(sizeDelta 비율) 더해 주면 사각형 자리는 그대로이다. (앵커가 늘어나 있어도 맞다)
        static void SetPivotKeepingRect(RectTransform target, Vector2 pivot)
        {
            Vector2 delta = pivot - target.pivot;
            target.pivot = pivot;
            target.anchoredPosition += Vector2.Scale(delta, target.sizeDelta);
        }

        /// <summary>버프 증가량 글자 하나. 값이 있으면 "+값"을 보여주고, 0이 되면 마지막 값을 남긴 채 서서히 투명해진다.</summary>
        class BuffLabel
        {
            readonly TMP_Text _label;
            readonly string _format;
            float _shownValue;
            float _alpha;

            public BuffLabel(TMP_Text label, string format)
            {
                _label = label;
                _format = format;
                Clear();
            }

            public void Apply(float value, float fadeTime, float deltaTime)
            {
                if (_label == null) return;

                if (value > 0f)
                {
                    // 값이 달라졌을 때만 글자를 새로 만든다. (매 프레임 불러도 문자열이 쌓이지 않는다)
                    if (Mathf.Approximately(value, _shownValue) == false)
                    {
                        _shownValue = value;
                        _label.text = "+" + value.ToString(_format, CultureInfo.InvariantCulture);
                    }

                    SetAlpha(1f);
                    return;
                }

                if (_alpha <= 0f) return;

                float step = (fadeTime > 0f) ? deltaTime / fadeTime : 1f;
                SetAlpha(Mathf.MoveTowards(_alpha, 0f, step));
            }

            public void Clear()
            {
                if (_label == null) return;

                _shownValue = 0f;
                _label.text = string.Empty;
                SetAlpha(0f);
            }

            void SetAlpha(float alpha)
            {
                if (Mathf.Approximately(_alpha, alpha)) return;

                _alpha = alpha;
                _label.alpha = alpha;
            }
        }

        /// <summary>스킬 아이콘의 클릭 및 선택 테두리 요소.</summary>
        class SkillIconView
        {
            public GameObject Root { get; }
            public RectTransform Rect { get; }
            public Button Button { get; }
            public Image Image { get; }
            public Outline Outline { get; }
            public TMP_Text Label { get; }

            public SkillIconView(GameObject root, RectTransform rect, Button button, Image image, Outline outline, TMP_Text label)
            {
                Root = root;
                Rect = rect;
                Button = button;
                Image = image;
                Outline = outline;
                Label = label;
            }
        }
    }
}