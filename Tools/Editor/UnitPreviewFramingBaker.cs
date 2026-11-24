using System.Collections.Generic;
using System.Linq;
using Defense.Units;
using UnityEditor;
using UnityEngine;

namespace Defense.EditorTools
{
    /// <summary>
    /// UnitData의 프리팹을 Idle 자세로 세워 놓고 크기를 재서, 유닛 프리뷰 카메라가 쓸 중심과 크기(UnitPreviewFraming)를 UnitData에 저장하는 에디터 도구.
    /// 실행 중에 재면 클릭한 순간의 공격 모션에 따라 구도가 흔들리므로, 자세가 고정된 에디터에서 미리 잰다.
    /// 거리 보정 배율(DistanceScale)은 손으로 고치는 값이라 다시 구워도 건드리지 않는다.
    /// </summary>
    public class UnitPreviewFramingBaker : EditorWindow
    {
        private const string MENU_PATH = "Tools/Unit/Unit Preview Framing Baker";
        private const string WINDOW_TITLE = "Unit Preview Framing Baker";

        // UnitData의 직렬화 필드 경로. (UnitData._previewFraming, UnitPreviewFraming.Center / Extents)
        private const string FRAMING_PROPERTY = "_previewFraming";
        private const string CENTER_PROPERTY = FRAMING_PROPERTY + ".Center";
        private const string EXTENTS_PROPERTY = FRAMING_PROPERTY + ".Extents";

        // 포즈가 실제로 바뀌었다고 보는 최소 변화량. (양손 위치 합의 제곱 거리)
        private const float POSE_CHANGE_THRESHOLD = 0.0001f;

        [SerializeField] private DefaultAsset _unitDataFolder;
        [SerializeField] private bool _applyIdlePose = true;
        [SerializeField] private AnimationClip _poseClip;
        [SerializeField] private float _poseTime;

        // 굽는 동안 쌓이는 경고 (포즈 적용 실패 등). 끝나면 요약에 같이 보여준다.
        private readonly List<string> _warnings = new List<string>();

        [MenuItem(MENU_PATH)]
        private static void Open()
        {
            GetWindow<UnitPreviewFramingBaker>(WINDOW_TITLE);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("대상", EditorStyles.boldLabel);
            _unitDataFolder = (DefaultAsset)EditorGUILayout.ObjectField("UnitData 폴더 (비우면 전체)", _unitDataFolder, typeof(DefaultAsset), false);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("포즈", EditorStyles.boldLabel);
            _applyIdlePose = EditorGUILayout.Toggle("Idle 포즈로 재기", _applyIdlePose);
            using (new EditorGUI.DisabledScope(_applyIdlePose == false))
            {
                _poseClip = (AnimationClip)EditorGUILayout.ObjectField("Idle 클립 (비우면 자동 탐색)", _poseClip, typeof(AnimationClip), false);
                _poseTime = EditorGUILayout.FloatField("샘플 시간 (초)", _poseTime);
            }

            EditorGUILayout.HelpBox("UnitData의 프리뷰 중심과 크기를 덮어씁니다. 거리 보정 배율은 그대로 유지됩니다.\n캐릭터 파츠와 무기(SkinnedMeshRenderer, MeshRenderer)의 영역으로 잽니다.", MessageType.Info);

            if (GUILayout.Button("선택한 UnitData만 굽기", GUILayout.Height(28f)))
            {
                Bake(CollectUnits(true));
            }

            if (GUILayout.Button("폴더 전체 굽기", GUILayout.Height(28f)))
            {
                Bake(CollectUnits(false));
            }
        }

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
                EditorUtility.DisplayDialog(WINDOW_TITLE, "대상 UnitData가 없습니다.", "확인");
                return;
            }

            _warnings.Clear();
            bool startedAnimationMode = false;
            int succeeded = 0;
            List<string> failures = new List<string>();

            // 임시로 만든 유닛을 메인 씬이 아니라 미리보기 씬에 두기 위해 쓴다. (화면에는 그리지 않는다)
            PreviewRenderUtility renderUtility = new PreviewRenderUtility();

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
                    if (EditorUtility.DisplayCancelableProgressBar("유닛 프리뷰 구도 굽기", unit.name, (float)i / units.Count))
                    {
                        break;
                    }

                    if (TryBakeUnit(renderUtility, unit, out string error))
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
            }

            string summary = $"성공 {succeeded}개 / 실패 {failures.Count}개";
            if (failures.Count > 0)
            {
                summary += "\n\n" + string.Join("\n", failures);
                Debug.LogWarning($"[UnitPreviewFramingBaker] 실패한 유닛\n{string.Join("\n", failures)}");
            }

            if (_warnings.Count > 0)
            {
                string warningText = string.Join("\n", _warnings.Distinct());
                summary += "\n\n[경고]\n" + warningText;
                Debug.LogWarning($"[UnitPreviewFramingBaker] 경고\n{warningText}");
            }

            Debug.Log($"[UnitPreviewFramingBaker] {summary}");
            EditorUtility.DisplayDialog(WINDOW_TITLE, summary, "확인");
        }

        /// <summary>프리팹을 임시로 생성해 Idle 자세로 세우고, 크기를 재서 UnitData에 저장한다.</summary>
        private bool TryBakeUnit(PreviewRenderUtility renderUtility, UnitData unit, out string error)
        {
            error = string.Empty;

            if (unit.Prefab == null)
            {
                error = "프리팹이 비어 있음";
                return false;
            }

            GameObject instance = Instantiate(unit.Prefab);
            renderUtility.AddSingleGO(instance);

            try
            {
                // 유닛 로컬 좌표와 월드 좌표를 같게 맞춘다. (런타임에서는 유닛이 바라보는 방향이 로컬 forward)
                instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                ApplyIdlePose(instance, unit.name);

                if (TryMeasureBounds(instance, out Bounds bounds) == false)
                {
                    error = "잴 렌더러를 찾지 못함";
                    return false;
                }

                UnitPreviewFraming framing = UnitPreviewFraming.FromLocalBounds(instance.transform.InverseTransformPoint(bounds.center), bounds.extents);
                if (TrySaveFraming(unit, framing) == false)
                {
                    error = $"UnitData에서 {FRAMING_PROPERTY} 필드를 찾지 못함";
                    return false;
                }

                return true;
            }
            finally
            {
                DestroyImmediate(instance);
            }
        }

        /// <summary>
        /// Idle 클립의 한 프레임을 적용해 T포즈를 푼다. 클립 샘플링이 먹히지 않으면 컨트롤러 기본 상태 평가를 시도하고,
        /// 그래도 안 되면 기본 자세로 두고 경고만 남긴다. (T포즈로 재면 팔이 벌어져서 영역이 가로로 넓게 잡힌다)
        /// </summary>
        private void ApplyIdlePose(GameObject instance, string label)
        {
            if (_applyIdlePose == false)
            {
                return;
            }

            Animator animator = instance.GetComponentInChildren<Animator>();
            if (animator == null)
            {
                _warnings.Add($"{label}: Animator를 찾지 못해 기본 자세로 쟀음");
                return;
            }

            Vector3 signatureBefore = GetPoseSignature(animator);
            AnimationClip clip = (_poseClip != null) ? _poseClip : FindIdleClip(animator);
            GameObject target = animator.gameObject;

            if (clip != null)
            {
                AnimationMode.SampleAnimationClip(target, clip, _poseTime);
                if (HasPoseChanged(animator, signatureBefore))
                {
                    return;
                }

                clip.SampleAnimation(target, _poseTime);
                if (HasPoseChanged(animator, signatureBefore))
                {
                    return;
                }
            }

            animator.Rebind();
            animator.Update(0f);
            if (HasPoseChanged(animator, signatureBefore))
            {
                return;
            }

            _warnings.Add($"{label}: Idle 포즈 적용 실패, 기본(T) 자세로 쟀음");
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

        /// <summary>양손 위치의 합. 포즈가 실제로 바뀌었는지 비교하는 용도. 휴머노이드가 아니면 0.</summary>
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

        // 휴머노이드가 아니면 바뀌었는지 확인할 방법이 없으므로 적용된 것으로 본다.
        private bool HasPoseChanged(Animator animator, Vector3 signatureBefore)
        {
            return (animator.isHuman == false) || ((GetPoseSignature(animator) - signatureBefore).sqrMagnitude > POSE_CHANGE_THRESHOLD);
        }

        /// <summary>켜져 있는 캐릭터 파츠와 무기의 월드 영역을 구한다. 파티클·트레일·바닥 링 같은 효과는 크기에 넣지 않는다.</summary>
        private bool TryMeasureBounds(GameObject instance, out Bounds bounds)
        {
            bool hasBounds = false;
            bounds = default;

            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(false))
            {
                bool isBody = (renderer is SkinnedMeshRenderer) || (renderer is MeshRenderer);
                if (isBody == false || renderer.enabled == false)
                {
                    continue;
                }

                if (hasBounds)
                {
                    bounds.Encapsulate(renderer.bounds);
                }
                else
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
            }

            return hasBounds;
        }

        /// <summary>UnitData의 중심과 크기만 덮어쓴다. 거리 보정 배율은 건드리지 않는다.</summary>
        private bool TrySaveFraming(UnitData unit, UnitPreviewFraming framing)
        {
            SerializedObject serializedUnit = new SerializedObject(unit);
            SerializedProperty center = serializedUnit.FindProperty(CENTER_PROPERTY);
            SerializedProperty extents = serializedUnit.FindProperty(EXTENTS_PROPERTY);

            if (center == null || extents == null)
            {
                return false;
            }

            center.vector3Value = framing.Center;
            extents.vector3Value = framing.Extents;
            serializedUnit.ApplyModifiedProperties();
            EditorUtility.SetDirty(unit);
            return true;
        }
    }
}
