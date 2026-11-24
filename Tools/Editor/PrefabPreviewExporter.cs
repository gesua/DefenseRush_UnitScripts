using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Defense.EditorTools
{
    /// <summary>
    /// 폴더 안의 프리팹을 일괄 렌더링해서
    /// 번호 라벨이 붙은 컨택트 시트(Sheet_XX.png), 개별 PNG, 번호-경로 목록(Index.txt)으로 저장하는 에디터 도구.
    /// 출력 위치는 프로젝트 루트의 PrefabPreviews 폴더이며 Assets 밖이라 임포트되지 않는다.
    /// </summary>
    public class PrefabPreviewExporter : EditorWindow
    {
        private const string MENU_PATH = "Tools/Unit/Prefab Preview Exporter";
        private const string OUTPUT_FOLDER_NAME = "PrefabPreviews";
        private const string INDIVIDUAL_FOLDER_NAME = "Individual";
        private const string INDEX_FILE_NAME = "Index.txt";

        // 카메라 / 조명 설정
        private const float FIELD_OF_VIEW = 30f;
        private const float PITCH_DEGREES = 10f;
        private const float YAW_DEGREES = 25f;

        private static readonly Color _backgroundColor = new Color(0.24f, 0.25f, 0.28f, 1f);
        private static readonly Color _labelBackgroundColor = new Color(0f, 0f, 0f, 1f);
        private static readonly Color _labelTextColor = new Color(1f, 1f, 1f, 1f);

        // 3x5 비트맵 숫자 폰트 (행 단위, 위에서 아래)
        private static readonly string[][] _digitGlyphs =
        {
            new[] { "111", "101", "101", "101", "111" }, // 0
            new[] { "010", "110", "010", "010", "111" }, // 1
            new[] { "111", "001", "111", "100", "111" }, // 2
            new[] { "111", "001", "111", "001", "111" }, // 3
            new[] { "101", "101", "111", "001", "001" }, // 4
            new[] { "111", "100", "111", "001", "111" }, // 5
            new[] { "111", "100", "111", "101", "111" }, // 6
            new[] { "111", "001", "001", "001", "001" }, // 7
            new[] { "111", "101", "111", "101", "111" }, // 8
            new[] { "111", "101", "111", "001", "111" }  // 9
        };

        [SerializeField] private DefaultAsset _sourceFolder;
        [SerializeField] private int _cellSize = 256;
        [SerializeField] private int _columns = 5;
        [SerializeField] private int _rows = 4;
        [SerializeField] private bool _saveIndividual = true;

        /// <summary>프리팹 하나의 렌더링 결과. Image가 null이면 렌더링 실패.</summary>
        private class PreviewEntry
        {
            public string AssetPath;
            public Texture2D Image;
        }

        [MenuItem(MENU_PATH)]
        private static void Open()
        {
            GetWindow<PrefabPreviewExporter>("Prefab Preview Exporter");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("대상", EditorStyles.boldLabel);
            _sourceFolder = (DefaultAsset)EditorGUILayout.ObjectField("프리팹 폴더", _sourceFolder, typeof(DefaultAsset), false);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("출력 설정", EditorStyles.boldLabel);
            _cellSize = EditorGUILayout.IntSlider("셀 크기(px)", _cellSize, 128, 512);
            _columns = EditorGUILayout.IntSlider("시트 가로 칸", _columns, 1, 8);
            _rows = EditorGUILayout.IntSlider("시트 세로 칸", _rows, 1, 8);
            _saveIndividual = EditorGUILayout.Toggle("개별 PNG 저장", _saveIndividual);

            EditorGUILayout.HelpBox("출력 위치: " + GetOutputRoot(), MessageType.Info);

            using (new EditorGUI.DisabledScope(_sourceFolder == null))
            {
                if (GUILayout.Button("내보내기", GUILayout.Height(32f)))
                {
                    Export();
                }
            }
        }

        #region 내보내기 흐름

        private void Export()
        {
            string folderPath = AssetDatabase.GetAssetPath(_sourceFolder);
            if (AssetDatabase.IsValidFolder(folderPath) == false)
            {
                EditorUtility.DisplayDialog("Prefab Preview Exporter", "폴더를 지정해 주세요.", "확인");
                return;
            }

            List<string> prefabPaths = CollectPrefabPaths(folderPath);
            if (prefabPaths.Count == 0)
            {
                EditorUtility.DisplayDialog("Prefab Preview Exporter", "폴더에 프리팹이 없습니다.", "확인");
                return;
            }

            string outputRoot = GetOutputRoot();
            Directory.CreateDirectory(outputRoot);

            List<PreviewEntry> entries = new List<PreviewEntry>();
            PreviewRenderUtility renderUtility = CreateRenderUtility();

            try
            {
                for (int i = 0; i < prefabPaths.Count; i++)
                {
                    float progress = (float)i / prefabPaths.Count;
                    if (EditorUtility.DisplayCancelableProgressBar("프리뷰 렌더링", prefabPaths[i], progress))
                    {
                        break;
                    }

                    entries.Add(new PreviewEntry
                    {
                        AssetPath = prefabPaths[i],
                        Image = RenderPrefab(renderUtility, prefabPaths[i])
                    });
                }

                EditorUtility.DisplayProgressBar("프리뷰 저장", "시트 / 목록 저장 중", 1f);
                SaveIndividuals(entries, outputRoot);
                SaveSheets(entries, outputRoot);
                SaveIndex(entries, outputRoot);
            }
            finally
            {
                renderUtility.Cleanup();
                EditorUtility.ClearProgressBar();

                foreach (PreviewEntry entry in entries)
                {
                    if (entry.Image != null)
                    {
                        DestroyImmediate(entry.Image);
                    }
                }
            }

            Debug.Log($"[PrefabPreviewExporter] {entries.Count}개 프리팹 저장 완료 → {outputRoot}");
            EditorUtility.RevealInFinder(outputRoot);
        }

        private List<string> CollectPrefabPaths(string folderPath)
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { folderPath });

            return guids
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path, System.StringComparer.Ordinal)
                .ToList();
        }

        private string GetOutputRoot()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, OUTPUT_FOLDER_NAME);
        }

        #endregion

        #region 렌더링

        private PreviewRenderUtility CreateRenderUtility()
        {
            PreviewRenderUtility utility = new PreviewRenderUtility();

            utility.cameraFieldOfView = FIELD_OF_VIEW;
            utility.camera.clearFlags = CameraClearFlags.SolidColor;
            utility.camera.backgroundColor = _backgroundColor;
            utility.ambientColor = new Color(0.45f, 0.45f, 0.5f, 1f);

            // 메인 조명은 카메라 반대편 위쪽, 보조 조명은 카메라 쪽에서 약하게
            utility.lights[0].intensity = 1.2f;
            utility.lights[0].transform.rotation = Quaternion.Euler(40f, 150f, 0f);
            utility.lights[1].intensity = 0.7f;
            utility.lights[1].transform.rotation = Quaternion.Euler(20f, 220f, 0f);

            return utility;
        }

        private Texture2D RenderPrefab(PreviewRenderUtility renderUtility, string assetPath)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[PrefabPreviewExporter] 프리팹을 불러오지 못했습니다: {assetPath}");
                return null;
            }

            GameObject instance = Instantiate(prefab);
            renderUtility.AddSingleGO(instance);

            try
            {
                if (TryGetBounds(instance, out Bounds bounds) == false)
                {
                    Debug.LogWarning($"[PrefabPreviewExporter] 렌더러가 없습니다: {assetPath}");
                    return null;
                }

                FrameCamera(renderUtility.camera, bounds);

                renderUtility.BeginStaticPreview(new Rect(0f, 0f, _cellSize, _cellSize));
                renderUtility.camera.Render();
                return renderUtility.EndStaticPreview();
            }
            finally
            {
                DestroyImmediate(instance);
            }
        }

        /// <summary>활성화된 모든 렌더러를 합친 바운드를 구한다.</summary>
        private bool TryGetBounds(GameObject target, out Bounds bounds)
        {
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
            bounds = default;

            if (renderers.Length == 0)
            {
                return false;
            }

            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return true;
        }

        /// <summary>바운드 전체가 화면에 들어오도록 정면 약간 위, 오른쪽 비스듬히 카메라를 배치한다.</summary>
        private void FrameCamera(Camera camera, Bounds bounds)
        {
            float radius = bounds.extents.magnitude;
            float distance = radius / Mathf.Sin(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            Vector3 direction = Quaternion.Euler(-PITCH_DEGREES, YAW_DEGREES, 0f) * Vector3.forward;

            camera.transform.position = bounds.center + direction * distance;
            camera.transform.LookAt(bounds.center);
            camera.nearClipPlane = Mathf.Max(0.01f, distance - radius * 2f);
            camera.farClipPlane = distance + radius * 2f;
        }

        #endregion

        #region 저장

        private void SaveIndividuals(List<PreviewEntry> entries, string outputRoot)
        {
            if (_saveIndividual == false)
            {
                return;
            }

            string individualDir = Path.Combine(outputRoot, INDIVIDUAL_FOLDER_NAME);
            Directory.CreateDirectory(individualDir);

            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Image == null)
                {
                    continue;
                }

                string prefabName = Path.GetFileNameWithoutExtension(entries[i].AssetPath);
                string filePath = Path.Combine(individualDir, $"{i + 1:D3}_{prefabName}.png");
                File.WriteAllBytes(filePath, entries[i].Image.EncodeToPNG());
            }
        }

        private void SaveSheets(List<PreviewEntry> entries, string outputRoot)
        {
            int perSheet = _columns * _rows;
            int sheetCount = Mathf.CeilToInt(entries.Count / (float)perSheet);
            int labelScale = Mathf.Max(2, _cellSize / 64);

            for (int sheetIndex = 0; sheetIndex < sheetCount; sheetIndex++)
            {
                Texture2D sheet = new Texture2D(_columns * _cellSize, _rows * _cellSize, TextureFormat.RGBA32, false);
                FillRect(sheet, 0, 0, sheet.width, sheet.height, _backgroundColor);

                for (int slot = 0; slot < perSheet; slot++)
                {
                    int entryIndex = sheetIndex * perSheet + slot;
                    if (entryIndex >= entries.Count)
                    {
                        break;
                    }

                    int column = slot % _columns;
                    int row = slot / _columns;
                    int cellX = column * _cellSize;
                    int cellY = sheet.height - (row + 1) * _cellSize; // 텍스처 원점은 좌하단

                    if (entries[entryIndex].Image != null)
                    {
                        sheet.SetPixels(cellX, cellY, _cellSize, _cellSize, entries[entryIndex].Image.GetPixels());
                    }

                    DrawNumberLabel(sheet, entryIndex + 1, cellX, cellY, labelScale);
                }

                sheet.Apply();
                string filePath = Path.Combine(outputRoot, $"Sheet_{sheetIndex + 1:D2}.png");
                File.WriteAllBytes(filePath, sheet.EncodeToPNG());
                DestroyImmediate(sheet);
            }
        }

        /// <summary>번호와 에셋 경로 목록. 시트의 번호와 1:1로 대응한다.</summary>
        private void SaveIndex(List<PreviewEntry> entries, string outputRoot)
        {
            List<string> lines = new List<string>();

            for (int i = 0; i < entries.Count; i++)
            {
                string failMark = (entries[i].Image == null) ? "\t(렌더링 실패)" : string.Empty;
                lines.Add($"{i + 1:D3}\t{entries[i].AssetPath}{failMark}");
            }

            File.WriteAllLines(Path.Combine(outputRoot, INDEX_FILE_NAME), lines);
        }

        #endregion

        #region 번호 라벨 그리기

        /// <summary>셀 좌상단에 검은 박스와 3자리 번호를 그린다.</summary>
        private void DrawNumberLabel(Texture2D target, int number, int cellX, int cellY, int scale)
        {
            string text = number.ToString("D3");
            int padding = scale * 2;
            int margin = scale * 2;
            int textWidth = text.Length * 4 * scale - scale; // 글자 3칸 + 간격 1칸, 마지막 간격 제외
            int boxWidth = textWidth + padding * 2;
            int boxHeight = 5 * scale + padding * 2;

            int boxX = cellX + margin;
            int boxY = cellY + _cellSize - boxHeight - margin;
            FillRect(target, boxX, boxY, boxWidth, boxHeight, _labelBackgroundColor);

            for (int i = 0; i < text.Length; i++)
            {
                int digit = text[i] - '0';
                int glyphX = boxX + padding + i * 4 * scale;
                DrawGlyph(target, _digitGlyphs[digit], glyphX, boxY + padding, scale, _labelTextColor);
            }
        }

        private void DrawGlyph(Texture2D target, string[] glyph, int originX, int originY, int scale, Color color)
        {
            for (int row = 0; row < glyph.Length; row++)
            {
                for (int column = 0; column < glyph[row].Length; column++)
                {
                    if (glyph[row][column] != '1')
                    {
                        continue;
                    }

                    int x = originX + column * scale;
                    int y = originY + (glyph.Length - 1 - row) * scale;
                    FillRect(target, x, y, scale, scale, color);
                }
            }
        }

        private void FillRect(Texture2D target, int x, int y, int width, int height, Color color)
        {
            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }

            target.SetPixels(x, y, width, height, pixels);
        }

        #endregion
    }
}
