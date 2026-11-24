using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Defense.Units;
using UnityEditor;
using UnityEngine;

namespace Defense.EditorTools
{
    /// <summary>
    /// 스킬 표(SkillTable.csv)를 읽어 스킬 에셋을 만들고(이미 있으면 값만 갱신) 유닛(UnitData)의 스킬 목록에 배정하는 에디터 도구.
    /// 표의 한 줄이 "유닛 한 명의 스킬 한 칸"이다. 유닛은 이름이 아니라 ID(UnitData.Id)로 찾는다.
    /// skillClass 칸이 비어 있는 줄(아직 구현되지 않은 스킬)은 건너뛰므로, 스킬을 구현할 때마다 표를 채우고 다시 실행하면 된다.
    /// 표에 있는 유닛의 스킬 목록은 표 순서대로 통째로 바뀐다. (표에 없는 유닛은 건드리지 않는다)
    /// 실행이 끝나면 SkillIconAssigner로 스킬 아이콘도 함께 연결할 수 있다. (미리보기일 때는 연결하지 않는다)
    /// </summary>
    public class SkillTableImporter : EditorWindow
    {
        private const string MENU_PATH = "Tools/Unit/Skill Table Importer";
        private const string TABLE_NAME = "SkillTable";

        // 표의 한 줄. 유닛 한 명의 스킬 한 칸.
        private class Row
        {
            public int UnitId;
            public string UnitName;
            public int Slot;
            public string Family;
            public string SkillClass;
            public string AssetName;
            public string DisplayName;
            public string Description;
            public float Chance;
            public float Multiplier;
            public float Ratio;
            public float Amount;
            public float Radius;
            public float Duration;
        }

        // 스킬 종류 하나: 어떤 에셋 타입인지, 표의 값을 어느 필드에 넣는지, 설명을 어떻게 만드는지.
        private class SkillBinding
        {
            public readonly Type AssetType;
            public readonly Action<SerializedObject, Row> Apply;
            public readonly Func<Row, string> Describe;

            public SkillBinding(Type assetType, Action<SerializedObject, Row> apply, Func<Row, string> describe)
            {
                AssetType = assetType;
                Apply = apply;
                Describe = describe;
            }
        }

        // 표의 skillClass 이름 -> 스킬 종류. 스킬을 새로 구현하면 여기에 한 줄 추가한다.
        private static readonly Dictionary<string, SkillBinding> _bindings = new Dictionary<string, SkillBinding>
        {
            { "ChanceDamage", new SkillBinding(typeof(ChanceDamageSkill), ApplyChanceDamage, DescribeChanceDamage) },
            { "Bomb", new SkillBinding(typeof(BombSkill), ApplyBomb, DescribeBomb) },
            { "Slow", new SkillBinding(typeof(SlowSkill), ApplySlow, DescribeSlow) },
            { "Plunder", new SkillBinding(typeof(PlunderSkill), ApplyPlunder, DescribePlunder) },
            { "ChanceCoin", new SkillBinding(typeof(ChanceCoinSkill), ApplyChanceCoin, DescribeChanceCoin) },
            { "MultiShot", new SkillBinding(typeof(MultiShotSkill), ApplyMultiShot, DescribeMultiShot) },
            { "ChainLightning", new SkillBinding(typeof(ChainLightningSkill), ApplyChainLightning, DescribeChainLightning) },
            { "Slash", new SkillBinding(typeof(SlashSkill), ApplySlash, DescribeSlash) },
            // 슬로우 에셋을 "모든 적" 모드로 쓰는 표 이름. 값은 Slow와 같은 칸(chance, amount, duration)을 쓴다.
            { "SlowAll", new SkillBinding(typeof(SlowSkill), ApplySlowAll, DescribeSlowAll) },
            // 일시 버프: 공격력/공격속도 중 무엇을 올리는지가 skillClass 이름으로 갈린다. (chance, ratio, duration을 쓴다)
            { "TempAttackUp", new SkillBinding(typeof(TempBuffSkill), (so, row) => ApplyTempBuff(so, row, isAttackPower: true), row => DescribeTempBuff(row, isAttackPower: true)) },
            { "TempSpeedUp", new SkillBinding(typeof(TempBuffSkill), (so, row) => ApplyTempBuff(so, row, isAttackPower: false), row => DescribeTempBuff(row, isAttackPower: false)) },
            // 스탯 오라: 올리는 스탯과 범위(인접/전체)가 skillClass 이름으로 갈린다. (ratio를 쓴다)
            { "AuraAttackAdjacent", new SkillBinding(typeof(StatAuraSkill), (so, row) => ApplyAura(so, row, isAttackPower: true, scope: AuraScope.Adjacent), row => DescribeAura(row, isAttackPower: true, scope: AuraScope.Adjacent)) },
            { "AuraAttackAll", new SkillBinding(typeof(StatAuraSkill), (so, row) => ApplyAura(so, row, isAttackPower: true, scope: AuraScope.All), row => DescribeAura(row, isAttackPower: true, scope: AuraScope.All)) },
            { "AuraSpeedAdjacent", new SkillBinding(typeof(StatAuraSkill), (so, row) => ApplyAura(so, row, isAttackPower: false, scope: AuraScope.Adjacent), row => DescribeAura(row, isAttackPower: false, scope: AuraScope.Adjacent)) },
            { "AuraSpeedAll", new SkillBinding(typeof(StatAuraSkill), (so, row) => ApplyAura(so, row, isAttackPower: false, scope: AuraScope.All), row => DescribeAura(row, isAttackPower: false, scope: AuraScope.All)) },
            { "Frenzy", new SkillBinding(typeof(FrenzySkill), ApplyFrenzy, DescribeFrenzy) },
            { "BossHunter", new SkillBinding(typeof(BossHunterSkill), ApplyBossHunter, DescribeBossHunter) },
            { "Execute", new SkillBinding(typeof(ExecuteSkill), ApplyExecute, DescribeExecute) },
            { "Bleed", new SkillBinding(typeof(BleedSkill), ApplyBleed, DescribeBleed) },
            { "Shockwave", new SkillBinding(typeof(ShockwaveSkill), ApplyShockwave, DescribeShockwave) },
            // 갑옷 파괴 에셋을 "모든 적" 모드로 쓰는 표 이름. 값은 ArmorBreak와 같은 칸을 쓴다.
            { "ArmorBreak", new SkillBinding(typeof(ArmorBreakSkill), ApplyArmorBreak, DescribeArmorBreak) },
            { "ArmorBreakAll", new SkillBinding(typeof(ArmorBreakSkill), ApplyArmorBreakAll, DescribeArmorBreakAll) },
            { "CorpseExplosion", new SkillBinding(typeof(CorpseExplosionSkill), ApplyCorpseExplosion, DescribeCorpseExplosion) },
            { "GiantSlayer", new SkillBinding(typeof(GiantSlayerSkill), ApplyGiantSlayer, DescribeGiantSlayer) },
            { "KillReward", new SkillBinding(typeof(KillRewardSkill), ApplyKillReward, DescribeKillReward) },
            { "Blessing", new SkillBinding(typeof(BlessingSkill), ApplyBlessing, DescribeBlessing) },
            { "Harvest", new SkillBinding(typeof(HarvestSkill), ApplyHarvest, DescribeHarvest) },
        };

        [SerializeField] private TextAsset _table;
        [SerializeField] private DefaultAsset _skillFolder;
        [SerializeField] private DefaultAsset _unitDataFolder;
        [SerializeField] private bool _dryRun = true;
        [SerializeField] private bool _assignIcons = true;

        private string _report = string.Empty;
        private Vector2 _scroll;

        [MenuItem(MENU_PATH)]
        private static void Open()
        {
            GetWindow<SkillTableImporter>("Skill Table Importer");
        }

        private void OnEnable()
        {
            if (_table == null) _table = EditorAssetFinder.FindTextAsset(TABLE_NAME);
            if (_skillFolder == null) _skillFolder = FindSkillFolder();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("입력 / 출력", EditorStyles.boldLabel);
            _table = (TextAsset)EditorGUILayout.ObjectField("스킬 표 (SkillTable.csv)", _table, typeof(TextAsset), false);
            _skillFolder = (DefaultAsset)EditorGUILayout.ObjectField("스킬 에셋 폴더", _skillFolder, typeof(DefaultAsset), false);
            _unitDataFolder = (DefaultAsset)EditorGUILayout.ObjectField("UnitData 폴더 (비우면 전체)", _unitDataFolder, typeof(DefaultAsset), false);

            EditorGUILayout.Space();
            _dryRun = EditorGUILayout.Toggle("미리보기만 (파일을 바꾸지 않음)", _dryRun);

            using (new EditorGUI.DisabledScope(_dryRun))
            {
                _assignIcons = EditorGUILayout.Toggle("스킬 아이콘도 함께 연결", _assignIcons);
            }

            using (new EditorGUI.DisabledScope(_table == null || _skillFolder == null))
            {
                if (GUILayout.Button((_dryRun) ? "미리보기" : "스킬 에셋 만들기 + 유닛에 배정", GUILayout.Height(32)))
                    Run();
            }

            if (string.IsNullOrEmpty(_report)) return;

            EditorGUILayout.Space();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.TextArea(_report, EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndScrollView();
        }

        private void Run()
        {
            StringBuilder log = new StringBuilder();
            List<string> problems = new List<string>();

            string folder = AssetDatabase.GetAssetPath(_skillFolder);
            if (AssetDatabase.IsValidFolder(folder) == false)
            {
                _report = "스킬 에셋 폴더를 지정해 주세요.";
                return;
            }

            List<Row> rows = ParseTable(_table.text, problems);
            if (rows.Count == 0)
            {
                _report = "표에서 읽은 줄이 없습니다.\n" + string.Join("\n", problems);
                return;
            }

            int unitCount = rows.Select(r => r.UnitId).Distinct().Count();
            if (_dryRun == false && EditorUtility.DisplayDialog("스킬 배정",
                    $"스킬 에셋을 만들거나 갱신하고, 표에 있는 유닛 {unitCount}명의 스킬 목록을 표대로 바꿉니다.\n(표에 없는 유닛은 그대로입니다)\n\n커밋해 두셨나요?",
                    "진행", "취소") == false)
                return;

            // 1) 스킬 에셋 만들기 / 갱신
            Dictionary<Row, UnitSkill> assets = new Dictionary<Row, UnitSkill>();
            SortedDictionary<string, int> pending = new SortedDictionary<string, int>();
            int created = 0;
            int updated = 0;

            foreach (Row row in rows)
            {
                if (string.IsNullOrEmpty(row.SkillClass))
                {
                    pending[row.Family] = (pending.TryGetValue(row.Family, out int count)) ? count + 1 : 1;
                    continue;
                }

                if (_bindings.TryGetValue(row.SkillClass, out SkillBinding binding) == false)
                {
                    problems.Add($"{row.AssetName}: 알 수 없는 skillClass '{row.SkillClass}'");
                    continue;
                }

                try
                {
                    UnitSkill skill = CreateOrUpdate(row, binding, folder, out bool isNew);
                    assets[row] = skill;
                    if (isNew) created++;
                    else updated++;
                }
                catch (Exception e)
                {
                    problems.Add($"{row.AssetName}: {e.Message}");
                }
            }

            // 2) 유닛에 배정
            Dictionary<string, UnitData> units = EditorAssetFinder.FindUnits(_unitDataFolder);
            int changed = 0;
            int unchanged = 0;
            List<string> partlyAssigned = new List<string>();

            foreach (IGrouping<int, Row> group in rows.GroupBy(r => r.UnitId))
            {
                string unitId = group.Key.ToString(CultureInfo.InvariantCulture);
                List<Row> ordered = group.OrderBy(r => r.Slot).ToList();

                if (units.TryGetValue(unitId, out UnitData unit) == false)
                {
                    problems.Add($"id {unitId} ({ordered[0].UnitName}): UnitData를 찾지 못했습니다.");
                    continue;
                }

                List<Row> assigned = ordered.Where(r => assets.ContainsKey(r)).ToList();
                if (assigned.Count < ordered.Count) partlyAssigned.Add($"{unit.DisplayName} ({assigned.Count}/{ordered.Count})");

                // 표의 줄 수가 등급 기본 칸보다 많으면 모자란 만큼을 추가 칸으로 채운다. (일부 유닛의 3번째 스킬)
                int extraSlots = Mathf.Max(0, ordered.Count - unit.Grade.SkillSlotCount());
                if (extraSlots > UnitData.MAX_EXTRA_SKILL_SLOTS)
                    problems.Add($"{unit.DisplayName}: {unit.Grade} 등급의 스킬은 최대 {unit.Grade.SkillSlotCount() + UnitData.MAX_EXTRA_SKILL_SLOTS}개입니다.");

                if (_dryRun)
                {
                    changed++;
                    continue;
                }

                if (AssignSkills(unit, assigned.Select(r => assets[r]).ToList(), Mathf.Min(extraSlots, UnitData.MAX_EXTRA_SKILL_SLOTS))) changed++;
                else unchanged++;
            }

            // 3) 아이콘 연결 (방금 만든 에셋도 찾을 수 있도록 Refresh 뒤에 한다)
            int iconAssigned = 0;
            List<string> iconEmpty = new List<string>();
            bool isIconAssigned = _dryRun == false && _assignIcons == true;

            if (_dryRun == false)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                if (isIconAssigned == true)
                {
                    iconAssigned = SkillIconAssigner.Assign(iconEmpty);
                }
            }

            log.AppendLine((_dryRun) ? "[미리보기] 파일은 바뀌지 않았습니다." : "[완료]");
            log.AppendLine($"스킬 에셋: 새로 {created}개 / 갱신 {updated}개 (폴더: {folder})");
            log.AppendLine((_dryRun) ? $"유닛 배정 대상: {changed}명" : $"유닛 배정: 변경 {changed}명 / 그대로 {unchanged}명");
            log.AppendLine($"아직 구현되지 않아 건너뛴 스킬 칸: {pending.Values.Sum()}개");
            if (pending.Count > 0) log.AppendLine("  " + string.Join(", ", pending.Select(p => $"{p.Key} {p.Value}")));
            if (partlyAssigned.Count > 0) log.AppendLine("일부만 배정된 유닛: " + string.Join(", ", partlyAssigned));
            if (isIconAssigned == true)
            {
                log.AppendLine($"스킬 아이콘: 연결 {iconAssigned}개 / 아이콘 없음 {iconEmpty.Count}개");
                if (iconEmpty.Count > 0) log.AppendLine("  " + string.Join(", ", iconEmpty));
            }
            if (problems.Count > 0) log.AppendLine("\n문제:\n  " + string.Join("\n  ", problems));
            _report = log.ToString();
            Debug.Log("[SkillTableImporter]\n" + _report);
        }

        // 스킬 에셋을 만들거나(없으면) 값만 갱신한다(있으면). 이미 있는 에셋은 GUID가 그대로라 유닛 연결이 유지된다.
        private UnitSkill CreateOrUpdate(Row row, SkillBinding binding, string folder, out bool isNew)
        {
            string path = $"{folder}/{row.AssetName}.asset";
            UnitSkill skill = AssetDatabase.LoadAssetAtPath<UnitSkill>(path);
            isNew = skill == null;

            if (isNew == false && skill.GetType() != binding.AssetType)
                throw new InvalidOperationException($"이미 있는 에셋의 종류({skill.GetType().Name})가 표({binding.AssetType.Name})와 다릅니다.");

            if (_dryRun) return skill;

            if (isNew) skill = (UnitSkill)CreateInstance(binding.AssetType);

            SerializedObject serialized = new SerializedObject(skill);
            Find(serialized, "_displayName").stringValue = row.DisplayName;
            Find(serialized, "_description").stringValue = (string.IsNullOrEmpty(row.Description)) ? binding.Describe(row) : row.Description;
            binding.Apply(serialized, row);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            if (isNew) AssetDatabase.CreateAsset(skill, path);
            else EditorUtility.SetDirty(skill);
            return skill;
        }

        // 목록과 추가 칸이 이미 같으면 건드리지 않는다. 바꿨으면 true.
        private static bool AssignSkills(UnitData unit, List<UnitSkill> skills, int extraSlots)
        {
            SerializedObject serialized = new SerializedObject(unit);
            SerializedProperty list = Find(serialized, "_skills");
            SerializedProperty extra = Find(serialized, "_extraSkillSlots");

            bool same = list.arraySize == skills.Count && extra.intValue == extraSlots;
            for (int i = 0; same && i < skills.Count; i++)
                same = list.GetArrayElementAtIndex(i).objectReferenceValue == skills[i];
            if (same) return false;

            extra.intValue = extraSlots;
            list.arraySize = skills.Count;
            for (int i = 0; i < skills.Count; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = skills[i];

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(unit);
            return true;
        }

        // ---------------- 스킬 종류별: 표의 값 -> 필드 / 설명

        private static void ApplyChanceDamage(SerializedObject so, Row row)
        {
            Find(so, "_chance").floatValue = row.Chance;
            Find(so, "_multiplier").floatValue = row.Multiplier;
        }

        private static string DescribeChanceDamage(Row row) =>
            $"공격 시 {Percent(row.Chance)}% 확률로 대미지 {Number(row.Multiplier)}배";

        private static void ApplyBomb(SerializedObject so, Row row)
        {
            Find(so, "_damageRatio").floatValue = row.Ratio;
            Find(so, "_radius").floatValue = row.Radius;
        }

        private static string DescribeBomb(Row row) =>
            $"공격 시 맞은 적 주변의 적에게 공격력의 {Percent(row.Ratio)}% 범위 피해";

        private static void ApplySlow(SerializedObject so, Row row)
        {
            Find(so, "_chance").floatValue = row.Chance;
            Find(so, "_amount").floatValue = row.Amount;
            Find(so, "_radius").floatValue = row.Radius;
            Find(so, "_duration").floatValue = row.Duration;
            // 같은 에셋을 SlowAll에서 Slow로 바꿔도 전체 적용이 남지 않게 한다.
            Find(so, "_affectAll").boolValue = false;
        }

        private static string DescribeSlow(Row row)
        {
            string when = (row.Chance < 1f) ? $"공격 시 {Percent(row.Chance)}% 확률로 " : "공격 시 ";
            string target = (row.Radius > 0f) ? $"맞은 적 주변 {Number(row.Radius)} 범위의 적" : "공격한 적";
            return $"{when}{target}의 이동속도 {Number(row.Amount)} 감소 ({Number(row.Duration)}초)";
        }

        private static void ApplyPlunder(SerializedObject so, Row row)
        {
            // 약탈은 소환 비용의 비율로만 코인을 준다. 비율이 비어 있으면 1코인만 주게 되므로 막는다.
            if (row.Ratio <= 0f)
                throw new InvalidOperationException($"Plunder '{row.DisplayName}'의 Ratio가 0 이하입니다. 소환 비용의 비율(0.3 = 30%)을 넣으세요.");

            Find(so, "_costRatio").floatValue = row.Ratio;
        }

        private static string DescribePlunder(Row row) =>
            $"소환될 때 소환 비용의 {Percent(row.Ratio)}%만큼 코인 획득";

        private static void ApplyChanceCoin(SerializedObject so, Row row)
        {
            Find(so, "_chance").floatValue = row.Chance;
            Find(so, "_amount").intValue = Mathf.RoundToInt(row.Amount);
        }

        private static string DescribeChanceCoin(Row row) =>
            $"공격 시 {Percent(row.Chance)}% 확률로 코인 {Number(row.Amount)} 획득";

        private static void ApplySlowAll(SerializedObject so, Row row)
        {
            ApplySlow(so, row);
            Find(so, "_radius").floatValue = 0f;
            Find(so, "_affectAll").boolValue = true;
        }

        private static string DescribeSlowAll(Row row)
        {
            string when = (row.Chance < 1f) ? $"공격 시 {Percent(row.Chance)}% 확률로 " : "공격 시 ";
            return $"{when}모든 적의 이동속도 {Number(row.Amount)} 감소 ({Number(row.Duration)}초)";
        }

        private static void ApplyMultiShot(SerializedObject so, Row row)
        {
            Find(so, "_chance").floatValue = row.Chance;
            Find(so, "_damageRatio").floatValue = row.Ratio;
            Find(so, "_extraHits").intValue = Mathf.Max(1, Mathf.RoundToInt(row.Amount));
        }

        private static string DescribeMultiShot(Row row) =>
            $"공격 시 {Percent(row.Chance)}% 확률로 같은 대상에게 {Mathf.Max(1, Mathf.RoundToInt(row.Amount))}회 추가 타격 (공격력의 {Percent(row.Ratio)}%, 적중 효과도 적용)";

        private static void ApplyChainLightning(SerializedObject so, Row row)
        {
            Find(so, "_chance").floatValue = row.Chance;
            Find(so, "_damageRatio").floatValue = row.Ratio;
            Find(so, "_maxTargets").intValue = Mathf.Max(1, Mathf.RoundToInt(row.Amount));
            Find(so, "_radius").floatValue = row.Radius;
        }

        private static string DescribeChainLightning(Row row) =>
            $"공격 시 {Percent(row.Chance)}% 확률로 근처 {Number(row.Radius)} 범위의 적 최대 {Mathf.Max(1, Mathf.RoundToInt(row.Amount))}명에게 공격력의 {Percent(row.Ratio)}% 번개 피해";

        private static void ApplySlash(SerializedObject so, Row row)
        {
            Find(so, "_chance").floatValue = row.Chance;
            Find(so, "_damageRatio").floatValue = row.Ratio;
        }

        private static string DescribeSlash(Row row) =>
            $"공격 시 {Percent(row.Chance)}% 확률로 사거리 안의 모든 적에게 공격력의 {Percent(row.Ratio)}% 피해";

        // 일시 버프는 표의 chance, ratio(올려 줄 비율), duration을 쓴다. 공격력/공격속도 중 어느 쪽인지는 skillClass가 정한다.
        private static void ApplyTempBuff(SerializedObject so, Row row, bool isAttackPower)
        {
            Find(so, "_chance").floatValue = row.Chance;
            Find(so, "_attackPowerRatio").floatValue = (isAttackPower) ? row.Ratio : 0f;
            Find(so, "_attackSpeedRatio").floatValue = (isAttackPower) ? 0f : row.Ratio;
            Find(so, "_duration").floatValue = row.Duration;
        }

        private static string DescribeTempBuff(Row row, bool isAttackPower)
        {
            string when = (row.Chance < 1f) ? $"공격 시 {Percent(row.Chance)}% 확률로 " : "공격 시 ";
            return $"{when}인접 칸 아군의 {StatName(isAttackPower)} {Percent(row.Ratio)}% 증가 ({Number(row.Duration)}초)";
        }

        // 오라는 표의 ratio(올려 줄 비율)를 쓴다. 공격력/공격속도와 범위(인접/전체)는 skillClass가 정한다.
        // 대각선 포함 여부(_includeDiagonal)는 표에 없으므로 에셋의 값을 그대로 둔다.
        private static void ApplyAura(SerializedObject so, Row row, bool isAttackPower, AuraScope scope)
        {
            Find(so, "_scope").enumValueIndex = (int)scope;
            Find(so, "_attackPowerRatio").floatValue = (isAttackPower) ? row.Ratio : 0f;
            Find(so, "_attackSpeedRatio").floatValue = (isAttackPower) ? 0f : row.Ratio;
        }

        private static string DescribeAura(Row row, bool isAttackPower, AuraScope scope)
        {
            string target = (scope == AuraScope.All) ? "모든 아군" : "인접 칸 아군";
            return $"{target}의 {StatName(isAttackPower)} {Percent(row.Ratio)}% 증가";
        }

        // 광분은 표의 ratio(올려 줄 공격속도 비율)와 duration을 쓴다.
        private static void ApplyFrenzy(SerializedObject so, Row row)
        {
            Find(so, "_attackSpeedRatio").floatValue = row.Ratio;
            Find(so, "_duration").floatValue = row.Duration;
        }

        private static string DescribeFrenzy(Row row) =>
            $"적을 처치하면 {Number(row.Duration)}초간 이 유닛의 공격속도 {Percent(row.Ratio)}% 증가";

        // 보스 사냥꾼은 표의 multiplier(보스에게 주는 대미지 배수)를 쓴다.
        private static void ApplyBossHunter(SerializedObject so, Row row)
        {
            Find(so, "_multiplier").floatValue = row.Multiplier;
        }

        private static string DescribeBossHunter(Row row) =>
            $"보스 몬스터에게 주는 대미지 {Percent(row.Multiplier - 1f)}% 증가";

        // 처형은 표의 ratio(발동 체력 비율)와 multiplier(대미지 배수)를 쓴다.
        private static void ApplyExecute(SerializedObject so, Row row)
        {
            Find(so, "_healthThreshold").floatValue = row.Ratio;
            Find(so, "_multiplier").floatValue = row.Multiplier;
        }

        private static string DescribeExecute(Row row) =>
            $"체력이 {Percent(row.Ratio)}% 이하인 적에게 주는 대미지 {Percent(row.Multiplier - 1f)}% 증가";

        // 출혈은 표의 chance, ratio(지속 시간 동안 들어가는 피해 합계 비율), duration을 쓴다.
        private static void ApplyBleed(SerializedObject so, Row row)
        {
            Find(so, "_chance").floatValue = row.Chance;
            Find(so, "_damageRatio").floatValue = row.Ratio;
            Find(so, "_duration").floatValue = row.Duration;
        }

        private static string DescribeBleed(Row row)
        {
            string when = (row.Chance < 1f) ? $"공격 시 {Percent(row.Chance)}% 확률로 " : "공격 시 ";
            return $"{when}공격한 적에게 {Number(row.Duration)}초간 공격력의 {Percent(row.Ratio)}% 지속 피해";
        }

        // 충격파는 표의 chance, ratio(대미지 비율), radius, duration(기절 시간)을 쓴다.
        private static void ApplyShockwave(SerializedObject so, Row row)
        {
            Find(so, "_chance").floatValue = row.Chance;
            Find(so, "_damageRatio").floatValue = row.Ratio;
            Find(so, "_radius").floatValue = row.Radius;
            Find(so, "_stunDuration").floatValue = row.Duration;
        }

        private static string DescribeShockwave(Row row) =>
            $"공격 시 {Percent(row.Chance)}% 확률로 맞은 적 주변 {Number(row.Radius)} 범위의 적에게 공격력의 {Percent(row.Ratio)}% 피해 + {Number(row.Duration)}초 기절";

        // 갑옷 파괴는 표의 chance, amount(깎을 방어력 고정값), ratio(깎을 방어력 비율), radius, duration을 쓴다.
        private static void ApplyArmorBreak(SerializedObject so, Row row)
        {
            Find(so, "_chance").floatValue = row.Chance;
            Find(so, "_amount").floatValue = row.Amount;
            Find(so, "_percent").floatValue = row.Ratio;
            Find(so, "_radius").floatValue = row.Radius;
            Find(so, "_duration").floatValue = row.Duration;
            // 같은 에셋을 ArmorBreakAll에서 ArmorBreak로 바꿔도 전체 적용이 남지 않게 한다.
            Find(so, "_affectAll").boolValue = false;
        }

        private static string DescribeArmorBreak(Row row)
        {
            string when = (row.Chance < 1f) ? $"공격 시 {Percent(row.Chance)}% 확률로 " : "공격 시 ";
            string target = (row.Radius > 0f) ? $"맞은 적 주변 {Number(row.Radius)} 범위의 적" : "공격한 적";
            return $"{when}{target}의 {ArmorReductionText(row)} ({Number(row.Duration)}초)";
        }

        private static void ApplyArmorBreakAll(SerializedObject so, Row row)
        {
            ApplyArmorBreak(so, row);
            Find(so, "_radius").floatValue = 0f;
            Find(so, "_affectAll").boolValue = true;
        }

        private static string DescribeArmorBreakAll(Row row)
        {
            string when = (row.Chance < 1f) ? $"공격 시 {Percent(row.Chance)}% 확률로 " : "공격 시 ";
            return $"{when}모든 적의 {ArmorReductionText(row)} ({Number(row.Duration)}초)";
        }

        // 고정값과 비율을 둘 다 쓰면 둘 다 적는다.
        private static string ArmorReductionText(Row row)
        {
            if (row.Amount > 0f && row.Ratio > 0f) return $"방어력 {Number(row.Amount)} + {Percent(row.Ratio)}% 감소";
            if (row.Ratio > 0f) return $"방어력 {Percent(row.Ratio)}% 감소";
            return $"방어력 {Number(row.Amount)} 감소";
        }

        // 시체 폭발은 표의 ratio(대미지 비율)와 radius를 쓴다.
        private static void ApplyCorpseExplosion(SerializedObject so, Row row)
        {
            Find(so, "_damageRatio").floatValue = row.Ratio;
            Find(so, "_radius").floatValue = row.Radius;
        }

        private static string DescribeCorpseExplosion(Row row) =>
            $"이 유닛이 처치한 적이 죽은 자리에서 폭발해 주변 {Number(row.Radius)} 범위의 적에게 공격력의 {Percent(row.Ratio)}% 피해";

        // 거인 학살은 표의 ratio(대상 최대 체력 비율)와 multiplier(추가 대미지 상한, 공격력 배수)를 쓴다.
        private static void ApplyGiantSlayer(SerializedObject so, Row row)
        {
            Find(so, "_healthRatio").floatValue = row.Ratio;
            Find(so, "_maxBonusMultiplier").floatValue = row.Multiplier;
        }

        private static string DescribeGiantSlayer(Row row) =>
            $"공격 시 대상 최대 체력의 {Percent(row.Ratio)}%만큼 추가 대미지 (최대 공격력의 {Percent(row.Multiplier)}%)";

        // 처치 보상은 표의 amount(처치할 때마다 받는 코인)를 쓴다.
        private static void ApplyKillReward(SerializedObject so, Row row)
        {
            Find(so, "_amount").intValue = Mathf.Max(1, Mathf.RoundToInt(row.Amount));
        }

        private static string DescribeKillReward(Row row) =>
            $"적을 처치할 때마다 코인 {Mathf.Max(1, Mathf.RoundToInt(row.Amount))} 획득";

        // 축복은 표의 ratio(일반에서 희귀 이상으로 옮길 소환 확률 비율)를 쓴다.
        private static void ApplyBlessing(SerializedObject so, Row row)
        {
            // 비율이 비어 있으면 아무 효과도 없는 스킬이 되므로 막는다.
            if (row.Ratio <= 0f)
                throw new InvalidOperationException($"Blessing '{row.DisplayName}'의 Ratio가 0 이하입니다. 옮길 소환 확률 비율(0.02 = 2%)을 넣으세요.");

            Find(so, "_rareOrHigherShift").floatValue = row.Ratio;
        }

        private static string DescribeBlessing(Row row) =>
            $"이 유닛이 필드에 있으면 희귀 이상 소환 확률 {Percent(row.Ratio)}% 증가";

        // 수확은 표의 amount(라운드가 시작될 때마다 받는 코인)를 쓴다.
        private static void ApplyHarvest(SerializedObject so, Row row)
        {
            Find(so, "_amount").intValue = Mathf.Max(1, Mathf.RoundToInt(row.Amount));
        }

        private static string DescribeHarvest(Row row) =>
            $"라운드가 시작될 때마다 코인 {Mathf.Max(1, Mathf.RoundToInt(row.Amount))} 획득";

        private static string StatName(bool isAttackPower) => (isAttackPower) ? "공격력" : "공격속도";

        // ---------------- 도우미

        private static SerializedProperty Find(SerializedObject so, string field)
        {
            SerializedProperty property = so.FindProperty(field);
            if (property == null)
                throw new InvalidOperationException($"{so.targetObject.GetType().Name}에 '{field}' 필드가 없습니다. 스킬 클래스와 도구가 맞는지 확인하세요.");

            return property;
        }

        private static string Number(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);

        private static string Percent(float ratio) => Number(ratio * 100f);

        // 이미 있는 스킬 에셋이 들어 있는 폴더를 기본 출력 폴더로 쓴다.
        private static DefaultAsset FindSkillFolder()
        {
            string[] guids = AssetDatabase.FindAssets("t:UnitSkill");
            if (guids.Length == 0) return null;

            string folder = System.IO.Path.GetDirectoryName(AssetDatabase.GUIDToAssetPath(guids[0])).Replace('\\', '/');
            return AssetDatabase.LoadAssetAtPath<DefaultAsset>(folder);
        }

        // ---------------- 표 읽기 (CSV 읽기는 TableCsv를 함께 쓴다)

        private static readonly string[] _requiredColumns = { "unitId", "slot", "skillClass", "assetName", "displayName" };

        private static List<Row> ParseTable(string text, List<string> problems)
        {
            List<Row> rows = new List<Row>();

            foreach (TableCsv.Row line in TableCsv.Read(text, _requiredColumns, problems))
            {
                if (int.TryParse(line.Get("unitId"), out int unitId) == false || int.TryParse(line.Get("slot"), out int slot) == false)
                {
                    problems.Add($"{line.Line}번째 줄: unitId / slot을 읽지 못했습니다.");
                    continue;
                }

                rows.Add(new Row
                {
                    UnitId = unitId,
                    UnitName = line.Get("unitName"),
                    Slot = slot,
                    Family = line.Get("family"),
                    SkillClass = line.Get("skillClass"),
                    AssetName = line.Get("assetName"),
                    DisplayName = line.Get("displayName"),
                    Description = line.Get("description"),
                    Chance = TableCsv.ParseFloat(line.Get("chance")),
                    Multiplier = TableCsv.ParseFloat(line.Get("multiplier")),
                    Ratio = TableCsv.ParseFloat(line.Get("ratio")),
                    Amount = TableCsv.ParseFloat(line.Get("amount")),
                    Radius = TableCsv.ParseFloat(line.Get("radius")),
                    Duration = TableCsv.ParseFloat(line.Get("duration")),
                });
            }
            return rows;
        }
    }
}