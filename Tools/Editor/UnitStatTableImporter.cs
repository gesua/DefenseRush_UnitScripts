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
    /// 유닛 능력치 표(UnitStatTable.csv)를 읽어 유닛(UnitData) 에셋의 값을 채우는 에디터 도구.
    /// 표의 한 줄이 유닛 한 명이다. 유닛은 이름이 아니라 ID(UnitData.Id)로 찾는다.
    /// 표에 없는 열과 비어 있는 칸은 건드리지 않고, 표에 없는 유닛도 그대로 둔다.
    /// 스킬 배정은 SkillTableImporter가 맡으므로 여기서는 스킬을 건드리지 않는다.
    /// </summary>
    public class UnitStatTableImporter : EditorWindow
    {
        private const string MENU_PATH = "Tools/Unit/Unit Stat Table Importer";
        private const string TABLE_NAME = "UnitStatTable";

        private enum FieldKind
        {
            Number,
            Text,
            RangeType,
        }

        // 표의 열 하나가 UnitData의 어떤 필드에 들어가는지.
        private class FieldBinding
        {
            public readonly string Column;
            public readonly string Label;
            public readonly string Property;
            public readonly FieldKind Kind;
            public readonly float Min;

            public FieldBinding(string column, string label, string property, FieldKind kind, float min = 0f)
            {
                Column = column;
                Label = label;
                Property = property;
                Kind = kind;
                Min = min;
            }
        }

        // 표의 열 이름 -> UnitData 필드. 채울 값을 늘리려면 여기에 한 줄 추가한다.
        private static readonly FieldBinding[] _bindings =
        {
            new FieldBinding("displayName", "표시 이름", "_displayName", FieldKind.Text),
            new FieldBinding("attackPower", "공격력", "_attackPower", FieldKind.Number),
            new FieldBinding("attackSpeed", "공격 속도", "_attackSpeed", FieldKind.Number, 0.01f),
            new FieldBinding("attackRange", "사거리", "_attackRange", FieldKind.Number),
            new FieldBinding("rangeType", "공격 타입", "_attackRangeType", FieldKind.RangeType),
            new FieldBinding("lobbyAttackPowerPerLevel", "로비 레벨당 공격력", "_lobbyAttackPowerPerLevel", FieldKind.Number),
        };

        [SerializeField] private TextAsset _table;
        [SerializeField] private DefaultAsset _unitDataFolder;
        [SerializeField] private bool _dryRun = true;

        private string _report = string.Empty;
        private Vector2 _scroll;

        [MenuItem(MENU_PATH)]
        private static void Open()
        {
            GetWindow<UnitStatTableImporter>("Unit Stat Table Importer");
        }

        private void OnEnable()
        {
            if (_table == null) _table = EditorAssetFinder.FindTextAsset(TABLE_NAME);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("입력", EditorStyles.boldLabel);
            _table = (TextAsset)EditorGUILayout.ObjectField("능력치 표 (UnitStatTable.csv)", _table, typeof(TextAsset), false);
            _unitDataFolder = (DefaultAsset)EditorGUILayout.ObjectField("UnitData 폴더 (비우면 전체)", _unitDataFolder, typeof(DefaultAsset), false);

            EditorGUILayout.Space();
            _dryRun = EditorGUILayout.Toggle("미리보기만 (파일을 바꾸지 않음)", _dryRun);

            using (new EditorGUI.DisabledScope(_table == null))
            {
                if (GUILayout.Button((_dryRun) ? "미리보기" : "유닛 능력치 채우기", GUILayout.Height(32)))
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
            List<string> problems = new List<string>();
            List<TableCsv.Row> rows = TableCsv.Read(_table.text, new[] { "unitId" }, problems);
            if (rows.Count == 0)
            {
                _report = "표에서 읽은 줄이 없습니다.\n" + string.Join("\n", problems);
                return;
            }

            if (_dryRun == false && EditorUtility.DisplayDialog("유닛 능력치",
                    $"표에 있는 유닛 {rows.Count}명의 능력치를 표 값으로 바꿉니다.\n(빈 칸과 표에 없는 열·유닛은 그대로입니다)\n\n커밋해 두셨나요?",
                    "진행", "취소") == false)
                return;

            Dictionary<string, UnitData> units = EditorAssetFinder.FindUnits(_unitDataFolder);
            HashSet<string> seen = new HashSet<string>();
            List<string> changeLines = new List<string>();
            int changedCount = 0;
            int unchangedCount = 0;

            foreach (TableCsv.Row row in rows)
            {
                string unitId = row.Get("unitId");
                if (units.TryGetValue(unitId, out UnitData unit) == false)
                {
                    problems.Add($"{row.Line}번째 줄: id {unitId}의 UnitData를 찾지 못했습니다.");
                    continue;
                }

                if (seen.Add(unitId) == false)
                {
                    problems.Add($"{row.Line}번째 줄: id {unitId}가 표에 두 번 있습니다. (뒤의 줄은 건너뜀)");
                    continue;
                }

                List<string> changes = ApplyRow(unit, row, problems);
                if (changes.Count == 0)
                {
                    unchangedCount++;
                    continue;
                }

                changedCount++;
                changeLines.Add($"  {unit.name} ({unitId}): {string.Join(", ", changes)}");
            }

            if (_dryRun == false) AssetDatabase.SaveAssets();

            StringBuilder log = new StringBuilder();
            log.AppendLine((_dryRun) ? "[미리보기] 파일은 바뀌지 않았습니다." : "[완료]");
            log.AppendLine($"변경 {changedCount}명 / 그대로 {unchangedCount}명");
            if (changeLines.Count > 0) log.AppendLine(string.Join("\n", changeLines));

            List<string> missing = units.Keys.Where(id => seen.Contains(id) == false).OrderBy(id => id).ToList();
            if (missing.Count > 0) log.AppendLine($"표에 없어서 건드리지 않은 유닛: {string.Join(", ", missing)}");
            if (problems.Count > 0) log.AppendLine("\n문제:\n  " + string.Join("\n  ", problems));

            _report = log.ToString();
            Debug.Log("[UnitStatTableImporter]\n" + _report);
        }

        // 표의 한 줄을 유닛에 적용하고, 바뀐 내용을 돌려준다. 미리보기에서는 저장하지 않는다.
        private List<string> ApplyRow(UnitData unit, TableCsv.Row row, List<string> problems)
        {
            SerializedObject serialized = new SerializedObject(unit);
            List<string> changes = new List<string>();

            foreach (FieldBinding binding in _bindings)
            {
                if (row.HasColumn(binding.Column) == false) continue;

                string cell = row.Get(binding.Column);
                if (string.IsNullOrEmpty(cell)) continue;

                bool changed = TryApply(serialized, binding, cell, out string change, out string error);
                if (error != null) problems.Add($"{unit.name}: {error}");
                else if (changed) changes.Add(change);
            }

            if (_dryRun == false && changes.Count > 0)
            {
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(unit);
            }
            return changes;
        }

        // 칸의 값 하나를 필드에 넣는다. 값이 바뀌면 true(바뀐 내용은 change). 읽을 수 없는 값이면 error에 이유를 적는다.
        private static bool TryApply(SerializedObject serialized, FieldBinding binding, string cell, out string change, out string error)
        {
            change = null;
            error = null;

            SerializedProperty property = serialized.FindProperty(binding.Property);
            if (property == null)
            {
                error = $"UnitData에 '{binding.Property}' 필드가 없습니다. 도구와 UnitData가 맞는지 확인하세요.";
                return false;
            }

            switch (binding.Kind)
            {
                case FieldKind.Number:
                    if (TableCsv.TryParseFloat(cell, out float number) == false)
                    {
                        error = $"{binding.Label}: '{cell}'을(를) 숫자로 읽지 못했습니다.";
                        return false;
                    }
                    if (number < binding.Min)
                    {
                        error = $"{binding.Label}: {Number(number)}은(는) 최소값 {Number(binding.Min)}보다 작습니다.";
                        return false;
                    }
                    if (Mathf.Approximately(property.floatValue, number)) return false;

                    change = $"{binding.Label} {Number(property.floatValue)} → {Number(number)}";
                    property.floatValue = number;
                    return true;

                case FieldKind.Text:
                    if (property.stringValue == cell) return false;

                    change = $"{binding.Label} {property.stringValue} → {cell}";
                    property.stringValue = cell;
                    return true;

                case FieldKind.RangeType:
                    if (TryParseRangeType(cell, out UnitAttackRangeType rangeType) == false)
                    {
                        error = $"{binding.Label}: '{cell}'은(는) Short/Long(근거리/원거리)가 아닙니다.";
                        return false;
                    }
                    if (property.enumValueIndex == (int)rangeType) return false;

                    change = $"{binding.Label} {(UnitAttackRangeType)property.enumValueIndex} → {rangeType}";
                    property.enumValueIndex = (int)rangeType;
                    return true;
            }
            return false;
        }

        private static bool TryParseRangeType(string text, out UnitAttackRangeType rangeType)
        {
            switch (text)
            {
                case "Short":
                case "근거리":
                    rangeType = UnitAttackRangeType.Short;
                    return true;
                case "Long":
                case "원거리":
                    rangeType = UnitAttackRangeType.Long;
                    return true;
            }
            rangeType = UnitAttackRangeType.Short;
            return false;
        }

        private static string Number(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
