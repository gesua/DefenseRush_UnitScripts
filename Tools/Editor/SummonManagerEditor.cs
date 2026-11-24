using System.Collections.Generic;
using Defense.Units;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Defense.EditorTools
{
    /// <summary>소환 매니저 Inspector에 플레이 전용 유닛 지정 소환 치트를 붙인다.</summary>
    [CustomEditor(typeof(SummonManager))]
    public class SummonManagerEditor : Editor
    {
        private const string DATABASE_PROPERTY = "_database";
        private const int MIN_CHEAT_COUNT = 1;
        private const int MAX_CHEAT_COUNT = 20;

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            InspectorElement.FillDefaultInspector(root, serializedObject, this);

            // UnitDatabase를 바꾸면 목록도 다시 만든다.
            var cheatPanel = new VisualElement();
            root.Add(cheatPanel);
            BuildCheatPanel(cheatPanel);
            root.TrackPropertyValue(serializedObject.FindProperty(DATABASE_PROPERTY), _ =>
            {
                cheatPanel.Clear();
                BuildCheatPanel(cheatPanel);
            });
            return root;
        }

        private void BuildCheatPanel(VisualElement panel)
        {
            serializedObject.Update();
            var database = serializedObject.FindProperty(DATABASE_PROPERTY).objectReferenceValue as UnitDatabase;
            List<UnitData> units = GetSortedUnits(database);

            panel.Add(new Label("개발용 유닛 지정 소환"));
            if (units.Count == 0)
            {
                panel.Add(new HelpBox("Database에 유닛이 연결되어 있어야 목록에서 고를 수 있습니다.", HelpBoxMessageType.Info));
                return;
            }

            var unitChoice = new PopupField<UnitData>("소환할 유닛", units, 0, FormatUnit, FormatUnit)
                { name = "CheatSummonUnitChoice" };
            var countField = new IntegerField("소환 마릿수") { value = MIN_CHEAT_COUNT, name = "CheatSummonCount" };
            countField.RegisterValueChangedCallback(evt =>
            {
                int clamped = Mathf.Clamp(evt.newValue, MIN_CHEAT_COUNT, MAX_CHEAT_COUNT);
                if (clamped != evt.newValue) countField.SetValueWithoutNotify(clamped);
            });

            var cheatButton = new Button(() => RunCheat(unitChoice.value, countField.value))
                { text = "[치트] 선택한 유닛 소환 (플레이 중)", name = "CheatSummonUnit" };

            panel.Add(unitChoice);
            panel.Add(countField);
            panel.Add(cheatButton);
            panel.Add(new HelpBox("재화와 등급 확률을 무시하고 고른 유닛을 바로 소환합니다. 최대 마릿수와 칸 자리는 일반 소환과 같은 규칙입니다.", HelpBoxMessageType.Info));

            void RefreshCheat()
            {
                var manager = target as SummonManager;
                cheatButton.SetEnabled(EditorApplication.isPlaying && manager != null && manager.isActiveAndEnabled);
            }
            RefreshCheat();
            cheatButton.schedule.Execute(RefreshCheat).Every(200);
        }

        private void RunCheat(UnitData data, int count)
        {
            int requested = Mathf.Clamp(count, MIN_CHEAT_COUNT, MAX_CHEAT_COUNT);
            int summoned = ((SummonManager)target).CheatSummon(data, requested);

            if (summoned <= 0)
                Debug.LogWarning("유닛 소환 치트 실행 불가: 플레이 상태, Grid 연결, 유닛 프리팹, 남은 자리를 확인하세요.", target);
            else if (summoned < requested)
                Debug.LogWarning($"유닛 소환 치트: 최대 마릿수 또는 칸 자리가 부족해 {requested}마리 중 {summoned}마리만 소환했습니다.", target);
        }

        // 등급 → 이름 순으로 정렬해, 같은 등급끼리 모여 보이게 한다.
        private static List<UnitData> GetSortedUnits(UnitDatabase database)
        {
            var units = new List<UnitData>();
            if (database == null) return units;

            foreach (UnitData unit in database.All)
            {
                if (unit != null) units.Add(unit);
            }

            units.Sort((a, b) =>
            {
                int byGrade = a.Grade.CompareTo(b.Grade);
                return (byGrade != 0) ? byGrade : string.CompareOrdinal(a.name, b.name);
            });
            return units;
        }

        private static string FormatUnit(UnitData unit)
        {
            if (unit == null) return "(없음)";

            string displayName = (string.IsNullOrEmpty(unit.DisplayName)) ? unit.name : unit.DisplayName;
            return $"[{GetGradeName(unit.Grade)}] {displayName} ({unit.Id})";
        }

        private static string GetGradeName(UnitGrade grade)
        {
            switch (grade)
            {
                case UnitGrade.Common: return "일반";
                case UnitGrade.Rare: return "희귀";
                case UnitGrade.Hero: return "영웅";
                case UnitGrade.Legendary: return "전설";
                default: return grade.ToString();
            }
        }
    }
}
