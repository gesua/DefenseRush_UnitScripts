using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Defense.EditorTools
{
    /// <summary>
    /// 스킬 에셋(Skill_{계열}_{등급})의 _icon을 계열 키로 자동 연결하는 에디터 툴.
    /// 아이콘 파일명 규칙: Icon_{계열}.png (등급 전용 아이콘은 Icon_{계열}_{등급}.png)
    /// </summary>
    public static class SkillIconAssigner
    {
        private const string SKILL_ASSET_PREFIX = "Skill_";
        private const string ICON_PREFIX = "Icon_";
        private const string ICON_FIELD_NAME = "_icon";

        // 일시적 계열은 기존 계열의 아이콘을 그대로 공유한다.
        private static readonly Dictionary<string, string> _iconKeyAlias = new Dictionary<string, string>
        {
            { "TempAttackUp", "Fervor" },
            { "TempSpeedUp", "Morale" },
            { "TempSlow", "Slow" },
            { "TempArmorBreak", "ArmorBreak" },
        };

        // 계열 공용 아이콘을 쓰지 않고, 등급 전용 아이콘(Icon_{계열}_{등급})만 쓰는 스킬.
        // 전용 파일이 아직 없으면 비워둔다. (냉기 파동 / 혹한)
        private static readonly HashSet<string> _dedicatedOnly = new HashSet<string>
        {
            "Slow_Hero",
            "Slow_Legendary",
        };

        [MenuItem("Tools/Unit/Assign Skill Icons")]
        public static void AssignAll()
        {
            List<string> emptyList = new List<string>();
            int assignedCount = Assign(emptyList);

            string emptyLog = (emptyList.Count > 0) ? "\n" + string.Join("\n", emptyList) : string.Empty;
            Debug.Log($"[SkillIconAssigner] 연결 완료: {assignedCount}개 / 아이콘 없음: {emptyList.Count}개{emptyLog}");
        }

        // 메뉴와 SkillTableImporter가 함께 쓴다. 연결한 개수를 돌려주고, 아이콘을 찾지 못한 에셋 이름은 emptyList에 담는다.
        public static int Assign(List<string> emptyList)
        {
            string[] guids = AssetDatabase.FindAssets("t:ScriptableObject " + SKILL_ASSET_PREFIX);
            int assignedCount = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string assetName = Path.GetFileNameWithoutExtension(path);

                // Skill_{계열}_{등급} 형식만 대상
                string[] parts = assetName.Split('_');
                if (assetName.StartsWith(SKILL_ASSET_PREFIX) == false || parts.Length != 3)
                {
                    continue;
                }

                string family = parts[1];
                string dedicatedKey = family + "_" + parts[2];

                Sprite icon = FindSprite(ICON_PREFIX + dedicatedKey);
                if (icon == null && _dedicatedOnly.Contains(dedicatedKey) == false)
                {
                    string iconKey = (_iconKeyAlias.TryGetValue(family, out string alias)) ? alias : family;
                    icon = FindSprite(ICON_PREFIX + iconKey);
                }

                if (icon == null)
                {
                    emptyList.Add(assetName);
                    continue;
                }

                if (TryAssign(path, icon) == true)
                {
                    assignedCount++;
                }
            }

            AssetDatabase.SaveAssets();
            return assignedCount;
        }

        private static bool TryAssign(string assetPath, Sprite icon)
        {
            Object target = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (target == null)
            {
                return false;
            }

            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(ICON_FIELD_NAME);
            if (property == null)
            {
                Debug.LogWarning($"[SkillIconAssigner] '{ICON_FIELD_NAME}' 필드를 찾을 수 없습니다: {assetPath}");
                return false;
            }

            property.objectReferenceValue = icon;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
            return true;
        }

        // FindAssets는 부분 일치라서(Icon_Slow → Icon_Slow_Hero도 포함) 파일명이 정확히 같은 것만 고른다.
        private static Sprite FindSprite(string iconName)
        {
            string[] guids = AssetDatabase.FindAssets(iconName + " t:Sprite");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == iconName)
                {
                    return AssetDatabase.LoadAssetAtPath<Sprite>(path);
                }
            }

            return null;
        }
    }
}