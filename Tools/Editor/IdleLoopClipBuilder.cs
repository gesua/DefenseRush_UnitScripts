using UnityEditor;
using UnityEngine;

/// <summary>
/// 원본 FBX의 한 구간(예: 안정된 Idle 구간)을 코사인 이징으로 왕복시켜
/// 처음과 끝의 포즈·속도가 완전히 같은 루프용 AnimationClip(.anim)으로 만들어 주는 에디터 툴.
/// 메뉴: Tools/Animation/Idle Loop Clip Builder
/// </summary>
public class IdleLoopClipBuilder : EditorWindow
{
    // 원본 FBX (Remy_Gunplay.fbx)
    [SerializeField] private Object _sourceModel;
    // 원본 FBX 안에서 구간을 가져올 클립 이름
    [SerializeField] private string _sourceClipName = "Crossbow_Attack";
    // 생성될 .anim 파일 이름 (원본 FBX와 같은 폴더에 생성)
    [SerializeField] private string _outputClipName = "Crossbow_Idle_Loop";
    // 루프로 쓸 원본 프레임 구간
    [SerializeField] private int _startFrame = 8;
    [SerializeField] private int _endFrame = 17;
    // 한 바퀴(왕복) 재생 시간(초). 길수록 느긋한 Idle이 됨
    [SerializeField] private float _loopDuration = 1.2f;
    // 생성되는 클립의 키 샘플링 FPS
    [SerializeField] private float _sampleFps = 30f;

    [MenuItem("Tools/Animation/Idle Loop Clip Builder")]
    private static void Open()
    {
        GetWindow<IdleLoopClipBuilder>("Idle Loop Clip Builder");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("원본", EditorStyles.boldLabel);
        _sourceModel = EditorGUILayout.ObjectField("원본 FBX", _sourceModel, typeof(Object), false);
        _sourceClipName = EditorGUILayout.TextField("원본 클립 이름", _sourceClipName);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("루프 구간", EditorStyles.boldLabel);
        _startFrame = EditorGUILayout.IntField("시작 프레임", _startFrame);
        _endFrame = EditorGUILayout.IntField("끝 프레임", _endFrame);
        _loopDuration = EditorGUILayout.FloatField("루프 시간(초)", _loopDuration);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("출력", EditorStyles.boldLabel);
        _outputClipName = EditorGUILayout.TextField("출력 클립 이름", _outputClipName);
        _sampleFps = EditorGUILayout.FloatField("샘플링 FPS", _sampleFps);

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(_sourceModel == null))
        {
            if (GUILayout.Button("루프 클립 생성", GUILayout.Height(30f)))
            {
                Build();
            }
        }
    }

    /// <summary>
    /// 전체 생성 흐름: 원본 클립 찾기 → 입력 검증 → 루프 클립 생성 → 저장
    /// </summary>
    private void Build()
    {
        AnimationClip sourceClip = FindSourceClip();
        if (sourceClip == null)
        {
            EditorUtility.DisplayDialog("Idle Loop Clip Builder", $"'{_sourceClipName}' 클립을 FBX에서 찾지 못했습니다.", "확인");
            return;
        }

        if (ValidateInput(sourceClip) == false)
        {
            return;
        }

        AnimationClip loopClip = CreateLoopClip(sourceClip);
        SaveClip(loopClip);
    }

    /// <summary>
    /// FBX 서브 에셋에서 이름이 일치하는 AnimationClip을 찾는다.
    /// </summary>
    private AnimationClip FindSourceClip()
    {
        string modelPath = AssetDatabase.GetAssetPath(_sourceModel);
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(modelPath);

        foreach (Object asset in assets)
        {
            if (asset is AnimationClip clip && clip.name == _sourceClipName)
            {
                return clip;
            }
        }

        return null;
    }

    /// <summary>
    /// 프레임 구간, 시간, FPS 값이 올바른지 검사한다.
    /// </summary>
    private bool ValidateInput(AnimationClip sourceClip)
    {
        int maxFrame = Mathf.FloorToInt((sourceClip.length * sourceClip.frameRate) + 0.001f);

        if (_startFrame < 0 || _endFrame > maxFrame || _startFrame >= _endFrame)
        {
            EditorUtility.DisplayDialog("Idle Loop Clip Builder", $"프레임 구간이 올바르지 않습니다. (0 ~ {maxFrame} 범위, 시작 < 끝)", "확인");
            return false;
        }

        if (_loopDuration <= 0f || _sampleFps <= 0f)
        {
            EditorUtility.DisplayDialog("Idle Loop Clip Builder", "루프 시간과 샘플링 FPS는 0보다 커야 합니다.", "확인");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 0~1 구간 값을 0→1→0 으로 왕복시키는 코사인 이징.
    /// 양 끝에서 속도가 0이라 루프 이음새에서 튀지 않는다.
    /// </summary>
    private float EaseLoop(float u)
    {
        return 0.5f * (1f - Mathf.Cos(u * Mathf.PI * 2f));
    }

    /// <summary>
    /// 원본 클립의 모든 커브를 왕복 샘플링해서 루프용 클립을 만든다.
    /// </summary>
    private AnimationClip CreateLoopClip(AnimationClip sourceClip)
    {
        float startTime = _startFrame / sourceClip.frameRate;
        float endTime = _endFrame / sourceClip.frameRate;
        int sampleCount = Mathf.Max(4, Mathf.RoundToInt(_loopDuration * _sampleFps));

        AnimationClip loopClip = new AnimationClip();
        loopClip.frameRate = _sampleFps;

        foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(sourceClip))
        {
            AnimationCurve sourceCurve = AnimationUtility.GetEditorCurve(sourceClip, binding);
            AnimationCurve loopCurve = new AnimationCurve();

            for (int i = 0; i <= sampleCount; i++)
            {
                // u: 0~1 (마지막 키는 첫 키와 같은 값이 되어 루프가 닫힌다)
                float u = (float)i / sampleCount;
                float sourceTime = Mathf.Lerp(startTime, endTime, EaseLoop(u));
                loopCurve.AddKey(new Keyframe(u * _loopDuration, sourceCurve.Evaluate(sourceTime)));
            }

            // 키마다 탄젠트가 0이면 샘플마다 끊겨 보이므로 부드럽게 보정
            for (int i = 0; i < loopCurve.length; i++)
            {
                loopCurve.SmoothTangents(i, 0f);
            }

            AnimationUtility.SetEditorCurve(loopClip, binding, loopCurve);
        }

        // 쿼터니언 커브(RootQ 등)의 부호 뒤집힘 방지
        loopClip.EnsureQuaternionContinuity();

        // 원본 클립의 설정(orientationOffsetY, keepOriginal 계열 등)을 그대로 이어받고 루프만 켠다
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(sourceClip);
        settings.startTime = 0f;
        settings.stopTime = _loopDuration;
        settings.loopTime = true;
        settings.loopBlend = false;
        AnimationUtility.SetAnimationClipSettings(loopClip, settings);
        loopClip.wrapMode = WrapMode.Loop;

        return loopClip;
    }

    /// <summary>
    /// 원본 FBX와 같은 폴더에 .anim으로 저장한다.
    /// 이미 같은 이름이 있으면 GUID를 유지한 채 내용만 덮어써서 Animator 연결이 끊기지 않게 한다.
    /// </summary>
    private void SaveClip(AnimationClip loopClip)
    {
        string modelPath = AssetDatabase.GetAssetPath(_sourceModel);
        string folder = System.IO.Path.GetDirectoryName(modelPath).Replace('\\', '/');
        string clipPath = $"{folder}/{_outputClipName}.anim";

        AnimationClip savedClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (savedClip == null)
        {
            AssetDatabase.CreateAsset(loopClip, clipPath);
            savedClip = loopClip;
        }
        else
        {
            EditorUtility.CopySerialized(loopClip, savedClip);
            EditorUtility.SetDirty(savedClip);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorGUIUtility.PingObject(savedClip);
        Debug.Log($"[IdleLoopClipBuilder] 루프 클립 생성 완료: {clipPath}");
    }
}
