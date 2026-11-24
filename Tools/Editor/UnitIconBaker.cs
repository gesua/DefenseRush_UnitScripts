using System.Collections.Generic;
using System.IO;
using System.Linq;
using Defense.Units;
using UnityEditor;
using UnityEngine;

namespace Defense.EditorTools
{
    /// <summary>
    /// UnitData의 프리팹에서 얼굴(머리 + 어깨 윗부분)만 찍어 투명 배경 PNG로 저장하고,
    /// 스프라이트로 임포트한 뒤 UnitData의 _icon에 자동으로 지정하는 에디터 도구.
    /// 등급 배경색·프레임·레벨 표시는 UI에서 입히는 것을 전제로 캐릭터만 굽는다.
    /// T포즈로 찍으면 팔이 같이 찍히므로 Idle 클립의 한 프레임을 적용한 뒤 찍는다.
    /// </summary>
    public class UnitIconBaker : EditorWindow
    {
        private const string MENU_PATH = "Tools/Unit/Unit Icon Baker";

        // 머리 메시 이름 접두사 (Chr_Head_Male_18, Chr_Head_No_Elements_Female_03 등. HeadCoverings는 해당 없음)
        private const string HEAD_PART_PREFIX = "Chr_Head_";
        private const string HEAD_BONE_NAME = "Head";

        // 카메라
        private const float FIELD_OF_VIEW = 20f;
        private const float FALLBACK_HEAD_SIZE = 0.3f;

        private static readonly Color _blackBackground = new Color(0f, 0f, 0f, 1f);
        private static readonly Color _whiteBackground = new Color(1f, 1f, 1f, 1f);

        [SerializeField] private DefaultAsset _unitDataFolder;
        [SerializeField] private DefaultAsset _outputFolder;
        [SerializeField] private int _width = 256;
        [SerializeField] private int _height = 320;
        [SerializeField] private bool _transparentBackground = true;
        [SerializeField] private Color _backgroundColor = new Color(0.24f, 0.25f, 0.28f, 1f);
        [SerializeField] private bool _skipExisting;
        [SerializeField] private float _frameHeightInHeads = 1.45f;
        [SerializeField] private float _verticalOffsetInHeads = -0.18f;
        [SerializeField] private float _yawDegrees = 10f;
        [SerializeField] private float _pitchDegrees = 3f;
        [SerializeField] private bool _applyIdlePose = true;
        [SerializeField] private AnimationClip _poseClip;
        [SerializeField] private float _poseTime;

        // 굽는 동안 쌓이는 경고 (포즈 적용 실패 등). 끝나면 요약에 같이 보여준다.
        private readonly List<string> _warnings = new List<string>();

        [MenuItem(MENU_PATH)]
        private static void Open()
        {
            GetWindow<UnitIconBaker>("Unit Icon Baker");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("대상 / 출력", EditorStyles.boldLabel);
            _unitDataFolder = (DefaultAsset)EditorGUILayout.ObjectField("UnitData 폴더 (비우면 전체)", _unitDataFolder, typeof(DefaultAsset), false);
            _outputFolder = (DefaultAsset)EditorGUILayout.ObjectField("아이콘 출력 폴더", _outputFolder, typeof(DefaultAsset), false);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("이미지", EditorStyles.boldLabel);
            _width = EditorGUILayout.IntSlider("가로 (px)", _width, 64, 1024);
            _height = EditorGUILayout.IntSlider("세로 (px)", _height, 64, 1024);
            _transparentBackground = EditorGUILayout.Toggle("투명 배경", _transparentBackground);
            using (new EditorGUI.DisabledScope(_transparentBackground))
            {
                _backgroundColor = EditorGUILayout.ColorField("배경색", _backgroundColor);
            }
            _skipExisting = EditorGUILayout.Toggle("이미 있는 아이콘 건너뛰기", _skipExisting);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("카메라", EditorStyles.boldLabel);
            _frameHeightInHeads = EditorGUILayout.Slider("프레임 높이 (머리 높이 배수)", _frameHeightInHeads, 0.8f, 3f);
            _verticalOffsetInHeads = EditorGUILayout.Slider("세로 위치 (음수일수록 아래)", _verticalOffsetInHeads, -1f, 1f);
            _yawDegrees = EditorGUILayout.Slider("좌우 회전 (도)", _yawDegrees, -180f, 180f);
            _pitchDegrees = EditorGUILayout.Slider("위아래 각도 (도)", _pitchDegrees, -30f, 30f);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("포즈", EditorStyles.boldLabel);
            _applyIdlePose = EditorGUILayout.Toggle("Idle 포즈로 찍기", _applyIdlePose);
            using (new EditorGUI.DisabledScope(_applyIdlePose == false))
            {
                _poseClip = (AnimationClip)EditorGUILayout.ObjectField("Idle 클립 (비우면 자동 탐색)", _poseClip, typeof(AnimationClip), false);
                _poseTime = EditorGUILayout.FloatField("샘플 시간 (초)", _poseTime);
            }

            EditorGUILayout.HelpBox("아이콘은 {UnitData 이름}.png 로 저장되고 UnitData의 _icon에 자동 지정됩니다. 같은 이름이 있으면 덮어씁니다.", MessageType.Info);

            using (new EditorGUI.DisabledScope(_outputFolder == null))
            {
                if (GUILayout.Button("선택한 UnitData만 굽기", GUILayout.Height(28f)))
                {
                    Bake(CollectUnits(true));
                }

                if (GUILayout.Button("폴더 전체 굽기", GUILayout.Height(28f)))
                {
                    Bake(CollectUnits(false));
                }
            }
        }

        #region 굽기 흐름

        private List<UnitData> CollectUnits(bool selectedOnly)
        {
            if (selectedOnly)
            {
                return Selection.GetFiltered<UnitData>(SelectionMode.Assets).ToList();
            }

            string[] folders = (_unitDataFolder != null) ? new[] { AssetDatabase.GetAssetPath(_unitDataFolder) } : new[] { "Assets" };

            return AssetDatabase.FindAssets("t:UnitData", folders)
                .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
                .OrderBy(path => path, System.StringComparer.Ordinal)
                .Select(path => AssetDatabase.LoadAssetAtPath<UnitData>(path))
                .Where(unit => unit != null)
                .ToList();
        }

        private void Bake(List<UnitData> units)
        {
            if (units.Count == 0)
            {
                EditorUtility.DisplayDialog("Unit Icon Baker", "대상 UnitData가 없습니다.", "확인");
                return;
            }

            string outputAssetPath = AssetDatabase.GetAssetPath(_outputFolder);
            if (AssetDatabase.IsValidFolder(outputAssetPath) == false)
            {
                EditorUtility.DisplayDialog("Unit Icon Baker", "출력 폴더를 지정해 주세요.", "확인");
                return;
            }

            _warnings.Clear();
            bool startedAnimationMode = false;
            int succeeded = 0;
            int skipped = 0;
            List<string> failures = new List<string>();
            PreviewRenderUtility renderUtility = CreateRenderUtility();

            try
            {
                if (_applyIdlePose && AnimationMode.InAnimationMode() == false)
                {
                    AnimationMode.StartAnimationMode();
                    startedAnimationMode = true;
                }

                for (int i = 0; i < units.Count; i++)
                {
                    UnitData unit = units[i];
                    if (EditorUtility.DisplayCancelableProgressBar("유닛 아이콘 굽기", unit.name, (float)i / units.Count))
                    {
                        break;
                    }

                    string assetPath = $"{outputAssetPath}/{unit.name}.png";
                    if (_skipExisting && File.Exists(ToFullPath(assetPath)))
                    {
                        skipped++;
                        continue;
                    }

                    if (TryBakeUnit(renderUtility, unit, assetPath, out string error))
                    {
                        succeeded++;
                    }
                    else
                    {
                        failures.Add($"{unit.name}: {error}");
                    }
                }
            }
            finally
            {
                if (startedAnimationMode)
                {
                    AnimationMode.StopAnimationMode();
                }

                renderUtility.Cleanup();
                EditorUtility.ClearProgressBar();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            string summary = $"성공 {succeeded}개 / 건너뜀 {skipped}개 / 실패 {failures.Count}개";
            if (failures.Count > 0)
            {
                summary += "\n\n" + string.Join("\n", failures);
                Debug.LogWarning($"[UnitIconBaker] 실패한 유닛\n{string.Join("\n", failures)}");
            }

            if (_warnings.Count > 0)
            {
                string warningText = string.Join("\n", _warnings.Distinct());
                Debug.LogWarning($"[UnitIconBaker] 경고\n{warningText}");
                summary += $"\n\n경고 {_warnings.Count}건 (자세한 내용은 Console 참고)\n" + string.Join("\n", _warnings.Distinct().Take(3));
            }

            Debug.Log($"[UnitIconBaker] {summary}");
            EditorUtility.DisplayDialog("Unit Icon Baker", summary, "확인");
        }

        private bool TryBakeUnit(PreviewRenderUtility renderUtility, UnitData unit, string assetPath, out string error)
        {
            SerializedObject serialized = new SerializedObject(unit);
            GameObject prefab = serialized.FindProperty("_prefab").objectReferenceValue as GameObject;
            if (prefab == null)
            {
                error = "_prefab이 비어 있음";
                return false;
            }

            Texture2D icon = RenderIcon(renderUtility, prefab, out error);
            if (icon == null)
            {
                return false;
            }

            try
            {
                File.WriteAllBytes(ToFullPath(assetPath), icon.EncodeToPNG());
            }
            finally
            {
                DestroyImmediate(icon);
            }

            Sprite sprite = ImportAsSprite(assetPath);
            if (sprite == null)
            {
                error = "스프라이트 임포트 실패";
                return false;
            }

            serialized.FindProperty("_icon").objectReferenceValue = sprite;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(unit);
            return true;
        }

        private string ToFullPath(string assetPath)
        {
            return Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath);
        }

        private Sprite ImportAsSprite(string assetPath)
        {
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

            if (AssetImporter.GetAtPath(assetPath) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.maxTextureSize = Mathf.NextPowerOfTwo(Mathf.Max(_width, _height));
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }

        #endregion

        #region 포즈

        /// <summary>
        /// Idle 클립의 한 프레임을 적용해 T포즈를 풀고, 그 때문에 머리 뼈가 움직인 만큼을 돌려준다.
        /// 클립 샘플링이 먹히지 않으면 컨트롤러 기본 상태 평가를 시도하고, 그래도 안 되면 T포즈로 두고 경고만 남긴다.
        /// </summary>
        private Vector3 ApplyIdlePose(GameObject instance, Animator animator, string label)
        {
            if (_applyIdlePose == false || animator == null)
            {
                return Vector3.zero;
            }

            Transform head = FindHeadBone(instance, animator);
            Vector3 headBefore = (head != null) ? head.position : Vector3.zero;
            Vector3 signatureBefore = GetPoseSignature(animator);
            bool canVerify = animator.isHuman;

            AnimationClip clip = (_poseClip != null) ? _poseClip : FindIdleClip(animator);
            GameObject target = animator.gameObject;
            List<System.Action> attempts = new List<System.Action>();

            if (clip != null)
            {
                attempts.Add(() => AnimationMode.SampleAnimationClip(target, clip, _poseTime));
                attempts.Add(() => clip.SampleAnimation(target, _poseTime));
            }

            attempts.Add(() =>
            {
                animator.Rebind();
                animator.Update(0f);
            });

            foreach (System.Action attempt in attempts)
            {
                attempt();

                // 휴머노이드면 양손 위치가 바뀌었는지로 실제 적용 여부를 확인한다. (T포즈 -> Idle이면 손이 크게 움직임)
                if (canVerify == false || (GetPoseSignature(animator) - signatureBefore).sqrMagnitude > 0.0001f)
                {
                    return (head != null) ? head.position - headBefore : Vector3.zero;
                }
            }

            RuntimeAnimatorController controller = animator.runtimeAnimatorController;
            string clipNames = (controller != null) ? string.Join(", ", controller.animationClips.Select(c => c.name).Distinct()) : "컨트롤러 없음";
            _warnings.Add($"{label}: Idle 포즈 적용 실패, T포즈로 촬영함 (컨트롤러 클립: {clipNames})");
            return Vector3.zero;
        }

        /// <summary>컨트롤러의 클립 중 이름에 idle이 들어간 첫 번째를 찾는다.</summary>
        private AnimationClip FindIdleClip(Animator animator)
        {
            RuntimeAnimatorController controller = animator.runtimeAnimatorController;
            if (controller == null)
            {
                return null;
            }

            return controller.animationClips.FirstOrDefault(clip => clip != null && clip.name.IndexOf("idle", System.StringComparison.OrdinalIgnoreCase) >= 0);
        }

        /// <summary>양손 위치의 합. 포즈가 실제로 바뀌었는지 비교하는 용도.</summary>
        private Vector3 GetPoseSignature(Animator animator)
        {
            if (animator.isHuman == false)
            {
                return Vector3.zero;
            }

            Vector3 sum = Vector3.zero;
            Transform left = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            Transform right = animator.GetBoneTransform(HumanBodyBones.RightHand);

            if (left != null)
            {
                sum += left.position;
            }

            if (right != null)
            {
                sum += right.position;
            }

            return sum;
        }

        #endregion

        #region 렌더링

        private PreviewRenderUtility CreateRenderUtility()
        {
            PreviewRenderUtility utility = new PreviewRenderUtility();

            utility.cameraFieldOfView = FIELD_OF_VIEW;
            utility.camera.clearFlags = CameraClearFlags.SolidColor;
            utility.ambientColor = new Color(0.5f, 0.5f, 0.55f, 1f);
            utility.lights[0].intensity = 1.1f;
            utility.lights[1].intensity = 0.8f;

            return utility;
        }

        /// <summary>프리팹을 임시로 생성해 얼굴을 찍는다. 투명 배경이면 검정/흰 배경 두 장의 차이로 알파를 복원한다.</summary>
        private Texture2D RenderIcon(PreviewRenderUtility renderUtility, GameObject prefab, out string error)
        {
            error = string.Empty;

            GameObject instance = Instantiate(prefab);
            renderUtility.AddSingleGO(instance);

            try
            {
                HideNonCharacterParts(instance);

                Animator animator = instance.GetComponentInChildren<Animator>();
                Vector3 poseOffset = ApplyIdlePose(instance, animator, prefab.name);
                if (TryGetHeadBounds(instance, animator, poseOffset, out Bounds headBounds) == false)
                {
                    error = "머리 위치를 찾지 못함";
                    return null;
                }

                Quaternion faceRotation = GetFaceRotation(instance, animator);
                FrameCamera(renderUtility, headBounds, faceRotation);
                ApplyLights(renderUtility, faceRotation);

                if (_transparentBackground == false)
                {
                    return Render(renderUtility, _backgroundColor);
                }

                Texture2D onBlack = Render(renderUtility, _blackBackground);
                Texture2D onWhite = Render(renderUtility, _whiteBackground);
                try
                {
                    return ExtractAlpha(onBlack, onWhite);
                }
                finally
                {
                    DestroyImmediate(onBlack);
                    DestroyImmediate(onWhite);
                }
            }
            finally
            {
                DestroyImmediate(instance);
            }
        }

        private Texture2D Render(PreviewRenderUtility renderUtility, Color background)
        {
            renderUtility.camera.backgroundColor = background;
            renderUtility.BeginStaticPreview(new Rect(0f, 0f, _width, _height));
            renderUtility.camera.Render();
            return renderUtility.EndStaticPreview();
        }

        /// <summary>캐릭터 파츠(SkinnedMeshRenderer)만 남기고 무기·등급 링 같은 나머지 렌더러와 스크립트는 끈다.</summary>
        private void HideNonCharacterParts(GameObject instance)
        {
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>())
            {
                renderer.enabled = (renderer is SkinnedMeshRenderer);
            }

            foreach (Behaviour behaviour in instance.GetComponentsInChildren<Behaviour>())
            {
                if ((behaviour is Animator) == false)
                {
                    behaviour.enabled = false;
                }
            }
        }

        /// <summary>활성 상태인 머리 메시의 바운드를 구한다. 없으면 Head 뼈 위치 주변의 근사 바운드를 쓴다.</summary>
        private bool TryGetHeadBounds(GameObject instance, Animator animator, Vector3 poseOffset, out Bounds bounds)
        {
            bounds = default(Bounds);
            bool found = false;

            foreach (SkinnedMeshRenderer part in instance.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (part.name.StartsWith(HEAD_PART_PREFIX) == false)
                {
                    continue;
                }

                if (found == false)
                {
                    bounds = part.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(part.bounds);
                }
            }

            if (found)
            {
                // 메시 바운드는 기본 자세 기준이라, 포즈로 머리 뼈가 움직인 만큼 옮겨 준다.
                bounds.center += poseOffset;
                return true;
            }

            Transform head = FindHeadBone(instance, animator);
            if (head == null)
            {
                return false;
            }

            bounds = new Bounds(head.position, Vector3.one * FALLBACK_HEAD_SIZE);
            return true;
        }

        private Transform FindHeadBone(GameObject instance, Animator animator)
        {
            Transform head = (animator != null && animator.isHuman) ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
            if (head != null)
            {
                return head;
            }

            foreach (Transform child in instance.GetComponentsInChildren<Transform>())
            {
                if (child.name == HEAD_BONE_NAME)
                {
                    return child;
                }
            }

            return null;
        }

        /// <summary>모델이 바라보는 방향(Animator가 붙은 Model의 앞)을 기준 회전으로 쓴다.</summary>
        private Quaternion GetFaceRotation(GameObject instance, Animator animator)
        {
            Transform reference = (animator != null) ? animator.transform : instance.transform;
            Vector3 forward = Vector3.ProjectOnPlane(reference.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.forward;
            }

            return Quaternion.LookRotation(forward.normalized, Vector3.up);
        }

        /// <summary>머리 높이를 기준으로 프레임 크기와 위치를 정해 얼굴 정면에서 약간 비스듬히 배치한다.</summary>
        private void FrameCamera(PreviewRenderUtility renderUtility, Bounds headBounds, Quaternion faceRotation)
        {
            Camera camera = renderUtility.camera;
            float headHeight = Mathf.Max(headBounds.size.y, 0.01f);
            float frameHeight = headHeight * _frameHeightInHeads;
            float distance = frameHeight / (2f * Mathf.Tan(FIELD_OF_VIEW * 0.5f * Mathf.Deg2Rad));
            Vector3 target = headBounds.center + Vector3.up * (headHeight * _verticalOffsetInHeads);
            Vector3 direction = faceRotation * Quaternion.Euler(-_pitchDegrees, _yawDegrees, 0f) * Vector3.forward;

            camera.transform.position = target + direction * distance;
            camera.transform.LookAt(target);
            camera.aspect = (float)_width / _height;
            camera.nearClipPlane = Mathf.Max(0.01f, distance - headHeight * 4f);
            camera.farClipPlane = distance + headHeight * 4f;
        }

        /// <summary>얼굴 앞쪽 위에서 메인 조명, 카메라 쪽에서 약한 보조 조명을 비춘다.</summary>
        private void ApplyLights(PreviewRenderUtility renderUtility, Quaternion faceRotation)
        {
            renderUtility.lights[0].transform.rotation = faceRotation * Quaternion.Euler(35f, 150f, 0f);
            renderUtility.lights[1].transform.rotation = faceRotation * Quaternion.Euler(15f, 215f, 0f);
        }

        #endregion

        #region 투명 배경 복원

        /// <summary>
        /// 같은 장면을 검정/흰 배경으로 찍은 두 장의 차이로 알파를 구한다.
        /// 불투명 물체는 배경이 달라도 색이 같으므로, 두 배경의 차이가 곧 (1 - 알파)가 된다.
        /// </summary>
        private Texture2D ExtractAlpha(Texture2D onBlack, Texture2D onWhite)
        {
            Color32[] black = onBlack.GetPixels32();
            Color32[] white = onWhite.GetPixels32();
            Color32[] result = new Color32[black.Length];

            for (int i = 0; i < black.Length; i++)
            {
                int difference = (white[i].r - black[i].r) + (white[i].g - black[i].g) + (white[i].b - black[i].b);
                float alpha = Mathf.Clamp01(1f - difference / (3f * 255f));

                if (alpha < 0.004f)
                {
                    result[i] = new Color32(0, 0, 0, 0);
                    continue;
                }

                result[i] = new Color32(
                    ToByte(black[i].r / (255f * alpha)),
                    ToByte(black[i].g / (255f * alpha)),
                    ToByte(black[i].b / (255f * alpha)),
                    ToByte(alpha));
            }

            Texture2D texture = new Texture2D(onBlack.width, onBlack.height, TextureFormat.RGBA32, false);
            texture.SetPixels32(result);
            texture.Apply();
            return texture;
        }

        private byte ToByte(float value)
        {
            return (byte)Mathf.Clamp(Mathf.RoundToInt(value * 255f), 0, 255);
        }

        #endregion
    }
}