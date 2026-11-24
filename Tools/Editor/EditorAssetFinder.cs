using System.Collections.Generic;
using Defense.Units;
using UnityEditor;
using UnityEngine;

namespace Defense.EditorTools
{
    /// <summary>표 가져오기 도구들이 함께 쓰는 에셋 찾기.</summary>
    public static class EditorAssetFinder
    {
        /// <summary>이름이 같은 TextAsset(표 CSV)을 찾는다. 없으면 null.</summary>
        public static TextAsset FindTextAsset(string assetName)
        {
            string[] guids = AssetDatabase.FindAssets($"{assetName} t:TextAsset");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path) == assetName)
                    return AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            }
            return null;
        }

        /// <summary>UnitData를 ID(UnitData.Id)로 찾을 수 있게 모은다. 폴더가 비어 있으면 프로젝트 전체에서 찾는다.</summary>
        public static Dictionary<string, UnitData> FindUnits(DefaultAsset folderAsset)
        {
            string folder = (folderAsset != null) ? AssetDatabase.GetAssetPath(folderAsset) : null;
            string[] guids = (string.IsNullOrEmpty(folder))
                ? AssetDatabase.FindAssets("t:UnitData")
                : AssetDatabase.FindAssets("t:UnitData", new[] { folder });

            Dictionary<string, UnitData> units = new Dictionary<string, UnitData>();
            foreach (string guid in guids)
            {
                UnitData unit = AssetDatabase.LoadAssetAtPath<UnitData>(AssetDatabase.GUIDToAssetPath(guid));
                if (unit != null && string.IsNullOrEmpty(unit.Id) == false) units[unit.Id] = unit;
            }
            return units;
        }
    }
}
