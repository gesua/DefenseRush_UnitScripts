using UnityEditor;
using UnityEngine;

namespace Defense.EditorTools
{
    /// <summary>
    /// SkillIcons 폴더에 새로 들어온 텍스처를 Sprite 설정으로 자동 임포트한다. (UnitIcons와 동일한 설정)
    /// </summary>
    public class SkillIconImportProcessor : AssetPostprocessor
    {
        private const string TARGET_FOLDER = "/SkillIcons/";

        private void OnPreprocessTexture()
        {
            if (assetPath.Contains(TARGET_FOLDER) == false)
            {
                return;
            }

            // 최초 임포트(.meta 없음)일 때만 적용 → 이후 수동으로 바꾼 설정은 유지
            if (assetImporter.importSettingsMissing == false)
            {
                return;
            }

            TextureImporter importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
        }
    }
}
