using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Defense.Units
{
    /// <summary>
    /// 선택한 유닛을 정면에서 보여주는 유닛 프리뷰. 유닛 프리뷰 프레임(PreviewFrame) 안의 RawImage에 전용 카메라의 결과(RenderTexture)를 출력한다.
    /// 선택한 유닛의 렌더러만 UnitPreview 레이어로 옮기고, 프리뷰 카메라는 그 레이어만 그리므로 다른 유닛과 바닥에 가리지 않는다.
    /// 메인 카메라는 모든 레이어를 그리므로 전장에서는 유닛이 그대로 보인다.
    /// 프리뷰 카메라는 유닛이 바라보는 방향의 정면에서 매 프레임 유닛을 따라간다. 유닛이 적을 향해 돌아도 화면 속 유닛은 계속 정면이고 배경(투명)만 돈다.
    /// 유닛은 풀링되므로, 프리뷰를 닫거나 유닛이 풀로 돌아갈 때 반드시 원래 레이어로 되돌린다.
    /// 공통 구도를 켜면 모든 유닛을 같은 중심·크기로 잡아서, 유닛마다 구워 둔 값이 달라도(투구 장식이 긴 유닛 등) 같은 기준으로 보여준다.
    /// 드래그 입력(UnitPreviewInteraction)이 Orbit을 부르면 카메라가 유닛 둘레를 돌고, 다른 유닛을 보여주거나 프리뷰를 닫으면 정면으로 돌아온다.
    /// 유닛 프리뷰 프레임(PreviewFrame) 아래의 RawImage 오브젝트에 붙인다. (프레임이 보이고 숨겨지는 것은 Shown, Closed 이벤트를 받는 UnitPreviewInteraction이 정한다)
    /// </summary>
    public class UnitPreviewController : MonoBehaviour
    {
        // Project Settings > Tags and Layers에 이 이름으로 추가한 레이어(User Layer 6)
        const string PREVIEW_LAYER_NAME = "UnitPreview";
        const string PREVIEW_CAMERA_NAME = "UnitPreviewCamera";
        const float MIN_DISTANCE = 1f;
        const float MIN_NEAR_CLIP = 0.05f;

        [Header("연결")]
        [Tooltip("프리뷰를 보여줄 RawImage. 비워두면 이 오브젝트에서 찾는다.")]
        [SerializeField] RawImage _rawImage;

        [Header("렌더 텍스처")]
        [Tooltip("프리뷰 해상도. RawImage의 가로세로 비율과 같게 맞출 것. (다르면 화면에서 찌그러진다)")]
        [SerializeField] Vector2Int _textureSize = new Vector2Int(512, 512);
        [Tooltip("안티 앨리어싱 배수. 1, 2, 4, 8 중 하나. 모바일에서는 2 이하를 권장한다.")]
        [SerializeField] int _antiAliasing = 2;

        [Header("카메라")]
        [SerializeField, Range(10f, 60f)] float _fieldOfView = 30f;
        [Tooltip("유닛을 내려다보는 각도(도). 0이면 정면 눈높이.")]
        [SerializeField, Range(0f, 45f)] float _pitchAngle = 10f;
        [Tooltip("유닛 크기에 맞춘 거리에 곱하는 여유 배율. 클수록 유닛이 작게 보인다.")]
        [SerializeField, Min(1f)] float _framePadding = 1.2f;
        [Tooltip("프리뷰 배경색. 알파가 0이면 투명해서 정보창 카드 배경이 비쳐 보인다.")]
        [SerializeField] Color _backgroundColor = new Color(0f, 0f, 0f, 0f);

        [Header("공통 구도")]
        [Tooltip("켜면 유닛마다 구워 둔 중심·크기 대신 아래 공통 값을 쓴다. 유닛별 거리 배율(Distance Scale)은 공통 값에 곱해서 예외 보정용으로 그대로 쓴다.")]
        [SerializeField] bool _useSharedFraming;
        [Tooltip("모든 유닛에 쓸 중심과 크기. 체형이 평범한 유닛 하나의 UnitData에 구워진 Preview Framing 값을 보고 정한다. 크기(Extents)가 모두 0이면 공통 값은 적용되지 않는다.")]
        [SerializeField] UnitPreviewFraming _sharedFraming = new UnitPreviewFraming { DistanceScale = 1f };

        [Header("카메라 회전")]
        [Tooltip("드래그 1픽셀당 카메라가 도는 각도(도).")]
        [SerializeField, Min(0f)] float _orbitSpeed = 0.4f;
        [Tooltip("드래그로 내려다볼 수 있는 각도의 최솟값(도). 0이면 정면 눈높이까지만 내려간다.")]
        [SerializeField, Range(-30f, 80f)] float _minPitchAngle = 0f;
        [Tooltip("드래그로 내려다볼 수 있는 각도의 최댓값(도).")]
        [SerializeField, Range(-30f, 80f)] float _maxPitchAngle = 70f;

        // 프리뷰 레이어로 옮긴 렌더러 오브젝트와 원래 레이어. (같은 인덱스끼리 짝)
        readonly List<GameObject> _layerObjects = new List<GameObject>();
        readonly List<int> _originalLayers = new List<int>();
        // 렌더러를 모으는 임시 버퍼
        readonly List<Renderer> _rendererBuffer = new List<Renderer>();

        Camera _camera;
        RenderTexture _texture;
        Unit _unit;
        int _previewLayer = -1;
        bool _isReady;
        bool _isShowing;
        Vector3 _localCenter;
        Vector3 _extents; // 선택한 순간에 잰 유닛의 반 크기(월드 단위). Inspector 값을 바꿨을 때 다시 맞추는 데 쓴다.
        float _distanceScale = 1f; // UnitData의 거리 보정 배율
        float _distance;
        float _orbitYaw;   // 드래그로 돌린 좌우 각도(도). 0이면 유닛 정면
        float _orbitPitch; // 드래그로 더한 위아래 각도(도). _pitchAngle에 더해서 쓴다.

        /// <summary>프리뷰가 닫힐 때 알린다. (보여주던 유닛이 없을 때 닫는 경우는 알리지 않는다)</summary>
        public event System.Action Closed;

        /// <summary>프리뷰가 열릴 때 알린다. (이미 보여주는 중에 다른 유닛으로 바뀌는 경우는 알리지 않는다)</summary>
        public event System.Action Shown;

        void Awake()
        {
            _previewLayer = LayerMask.NameToLayer(PREVIEW_LAYER_NAME);
            if (_previewLayer < 0)
            {
                Debug.LogError($"[UnitPreviewController] '{PREVIEW_LAYER_NAME}' 레이어가 없습니다. Project Settings > Tags and Layers에 추가해 주세요.", this);
                return;
            }

            if (_rawImage == null) TryGetComponent(out _rawImage);
            if (_rawImage == null)
            {
                Debug.LogError("[UnitPreviewController] RawImage 참조가 비어 있습니다.", this);
                return;
            }

            CreateTexture();
            CreateCamera();

            _rawImage.texture = _texture;
            _rawImage.raycastTarget = false; // 터치는 뒤쪽 프레임(UnitPreviewInteraction)이 받으므로 RawImage는 입력을 막지 않는다
            _isReady = true;
        }

        // 플레이 중에 Inspector 값을 바꾸면 바로 보이게 한다. (거리 계산과 렌더 텍스처는 평소 한 번만 정하므로 직접 다시 맞춘다)
        void OnValidate()
        {
            if (Application.isPlaying == false || _isReady == false) return;

            ApplyLiveSettings();
        }

        void OnDisable()
        {
            Hide();
        }

        void OnDestroy()
        {
            RestoreLayers();

            if (_camera != null) Destroy(_camera.gameObject);

            if (_texture != null)
            {
                _texture.Release();
                Destroy(_texture);
            }
        }

        // 유닛이 돌거나 움직여도 정면을 유지하도록, 모든 Update가 끝난 뒤에 카메라를 옮긴다.
        void LateUpdate()
        {
            if (_isShowing == false) return;

            // 판매나 승급, 씬 정리로 유닛이 풀에 돌아가 꺼졌으면 프리뷰도 닫는다.
            if (_unit == null || _unit.Data == null || _unit.gameObject.activeInHierarchy == false)
            {
                Hide();
                return;
            }

#if UNITY_EDITOR
            RefreshBakedFraming();
#endif
            UpdateCameraPose();
        }

        /// <summary>유닛을 프리뷰에 보여준다. 이미 보여주는 유닛이면 아무 일도 하지 않는다. unit이 null이면 닫는다.</summary>
        public void Show(Unit unit)
        {
            if (_isReady == false) return;

            if (unit == null)
            {
                Hide();
                return;
            }

            if (_isShowing && unit == _unit) return;

            bool wasShowing = _isShowing;

            // 다른 유닛으로 바뀌는 경우: 이전 유닛의 레이어를 먼저 되돌린다.
            RestoreLayers();

            _unit = unit;
            ResetOrbit();
            bool hasBounds = ApplyLayerAndMeasure(unit, out Bounds bounds);
            ApplyFraming(unit, bounds, hasBounds);
            ApplyCameraFit();

            _isShowing = true;
            _camera.enabled = true;
            UpdateCameraPose();

            if (wasShowing == false) Shown?.Invoke();
        }

        /// <summary>프리뷰를 닫고 유닛의 레이어를 원래대로 되돌린다.</summary>
        public void Hide()
        {
            bool wasShowing = _isShowing;

            RestoreLayers();

            _unit = null;
            _isShowing = false;
            ResetOrbit();
            if (_camera != null) _camera.enabled = false;

            if (wasShowing) Closed?.Invoke();
        }

        /// <summary>
        /// 드래그한 만큼 카메라를 유닛 둘레로 돌린다. 유닛을 잡고 돌리는 느낌이라, 오른쪽으로 끌면 유닛 앞면이 오른쪽으로 돌고 아래로 끌면 위에서 내려다본다.
        /// screenDelta는 화면 픽셀 단위의 드래그 이동량. 보여주는 유닛이 없으면 무시한다.
        /// </summary>
        public void Orbit(Vector2 screenDelta)
        {
            if (_isShowing == false) return;

            _orbitYaw = Mathf.Repeat(_orbitYaw + screenDelta.x * _orbitSpeed, 360f);
            _orbitPitch = Mathf.Clamp(_orbitPitch - screenDelta.y * _orbitSpeed, _minPitchAngle - _pitchAngle, _maxPitchAngle - _pitchAngle);
        }

        /// <summary>드래그로 돌린 카메라를 유닛 정면으로 되돌린다.</summary>
        public void ResetOrbit()
        {
            _orbitYaw = 0f;
            _orbitPitch = 0f;
        }

        void CreateTexture()
        {
            int width = Mathf.Max(16, _textureSize.x);
            int height = Mathf.Max(16, _textureSize.y);

            _texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                name = "UnitPreviewTexture",
                antiAliasing = Mathf.Clamp(Mathf.ClosestPowerOfTwo(_antiAliasing), 1, 8)
            };
            _texture.Create();
        }

        // 씬 루트에 프리뷰 전용 카메라를 만든다. 평소에는 꺼 두어 렌더링 비용이 들지 않는다.
        void CreateCamera()
        {
            var cameraObject = new GameObject(PREVIEW_CAMERA_NAME);

            _camera = cameraObject.AddComponent<Camera>();
            _camera.enabled = false;
            _camera.orthographic = false;
            _camera.fieldOfView = _fieldOfView;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = _backgroundColor;
            _camera.cullingMask = 1 << _previewLayer;
            _camera.allowHDR = false;
            _camera.allowMSAA = false;
            _camera.depth = -100f; // 메인 카메라보다 먼저 그린다
            _camera.targetTexture = _texture;
        }

        // 유닛의 렌더러가 붙은 오브젝트만 프리뷰 레이어로 옮기고, 보이는 렌더러들의 월드 영역을 돌려준다.
        // 바닥에 까는 등급 링(SpriteRenderer)은 프리뷰에서 어색하므로 옮기지 않는다.
        // 유닛 루트가 아니라 렌더러 오브젝트만 옮기므로 유닛의 다른 동작(입력, 물리 판정)에는 영향이 없다.
        bool ApplyLayerAndMeasure(Unit unit, out Bounds bounds)
        {
            bool hasBounds = false;
            bounds = default;

            unit.GetComponentsInChildren(true, _rendererBuffer);
            for (int i = 0; i < _rendererBuffer.Count; i++)
            {
                Renderer renderer = _rendererBuffer[i];
                if (renderer is SpriteRenderer) continue;

                GameObject target = renderer.gameObject;
                if (target.layer != _previewLayer)
                {
                    _layerObjects.Add(target);
                    _originalLayers.Add(target.layer);
                    target.layer = _previewLayer;
                }

                // 꺼져 있는 파츠(모듈형 캐릭터의 안 쓰는 부품)는 크기 계산에서 뺀다.
                if (renderer.enabled == false || target.activeInHierarchy == false) continue;

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

            _rendererBuffer.Clear();
            return hasBounds;
        }

        // 유닛 프리뷰 카메라가 잡을 중심과 크기를 정한다.
        // 공통 구도나 에디터에서 Idle 자세로 미리 구워 둔 값이 있으면 그대로 쓰고, 없는 유닛은 선택한 순간의 자세로 직접 잰다.
        void ApplyFraming(Unit unit, Bounds bounds, bool hasBounds)
        {
            if (TryGetFraming(unit.Data, out Vector3 center, out Vector3 extents, out float distanceScale))
            {
                _localCenter = center;
                _extents = extents;
                _distanceScale = distanceScale;
                return;
            }

            _distanceScale = distanceScale;

            // 렌더러를 찾지 못한 경우를 대비한 기본 크기. (사람 키 정도)
            if (hasBounds == false) bounds = new Bounds(unit.transform.position + Vector3.up, new Vector3(1f, 2f, 1f));

            UnitPreviewFraming measured = UnitPreviewFraming.FromLocalBounds(unit.transform.InverseTransformPoint(bounds.center), bounds.extents);
            _localCenter = measured.Center;
            _extents = measured.Extents;
        }

        // 쓸 프레이밍을 정한다.
        // 공통 구도를 켜고 공통 값이 있으면 공통 중심·크기를 쓰고, 거리 배율은 공통 값에 유닛별 값을 곱한다.
        // 아니면 유닛에 구워 둔 값을 쓴다. 쓸 값이 없으면 false를 돌려주고, 이때 distanceScale은 유닛별 거리 배율이다.
        bool TryGetFraming(UnitData data, out Vector3 center, out Vector3 extents, out float distanceScale)
        {
            UnitPreviewFraming unitFraming = (data != null) ? data.PreviewFraming : default;
            distanceScale = unitFraming.EffectiveDistanceScale;

            if (_useSharedFraming && _sharedFraming.IsBaked)
            {
                center = _sharedFraming.Center;
                extents = _sharedFraming.Extents;
                distanceScale *= _sharedFraming.EffectiveDistanceScale;
                return true;
            }

            center = unitFraming.Center;
            extents = unitFraming.Extents;
            return unitFraming.IsBaked;
        }

        // 정해 둔 크기에 맞춰 카메라 거리와 시야각을 정한다. 값을 바꿀 때마다 다시 부를 수 있다.
        void ApplyCameraFit()
        {
            float halfHeight = _extents.y;
            float halfWidth = Mathf.Max(_extents.x, _extents.z);
            float tanHalfFov = Mathf.Tan(_fieldOfView * 0.5f * Mathf.Deg2Rad);
            float aspect = (float)_texture.width / _texture.height;

            float fitHeight = halfHeight / tanHalfFov;
            float fitWidth = halfWidth / (tanHalfFov * aspect);
            _distance = Mathf.Max(MIN_DISTANCE, Mathf.Max(fitHeight, fitWidth) * _framePadding * _distanceScale);

            float radius = _extents.magnitude;
            _camera.fieldOfView = _fieldOfView;
            _camera.nearClipPlane = Mathf.Max(MIN_NEAR_CLIP, _distance - radius * 2f);
            _camera.farClipPlane = _distance + radius * 2f;
        }

        // Inspector 값을 카메라와 렌더 텍스처에 다시 반영한다. 프리뷰를 보여주는 중이면 화면도 바로 갱신된다.
        void ApplyLiveSettings()
        {
            _camera.backgroundColor = _backgroundColor;

            int width = Mathf.Max(16, _textureSize.x);
            int height = Mathf.Max(16, _textureSize.y);
            int antiAliasing = Mathf.Clamp(Mathf.ClosestPowerOfTwo(_antiAliasing), 1, 8);
            if (_texture.width != width || _texture.height != height || _texture.antiAliasing != antiAliasing)
                RecreateTexture();

            if (_isShowing == false) return;

            ApplyCameraFit();
            UpdateCameraPose();
        }

#if UNITY_EDITOR
        // 플레이 중에 UnitData나 공통 구도의 프리뷰 값(중심, 크기, 거리 배율)을 고치면 바로 보이게 한다. 에디터에서만 쓴다.
        void RefreshBakedFraming()
        {
            if (TryGetFraming(_unit.Data, out Vector3 center, out Vector3 extents, out float distanceScale) == false) return;
            if (center == _localCenter && extents == _extents && Mathf.Approximately(distanceScale, _distanceScale)) return;

            _localCenter = center;
            _extents = extents;
            _distanceScale = distanceScale;
            ApplyCameraFit();
        }
#endif

        // 해상도나 안티 앨리어싱이 바뀌면 렌더 텍스처를 새로 만들어 카메라와 RawImage에 다시 연결한다.
        void RecreateTexture()
        {
            _camera.targetTexture = null;
            _texture.Release();
            Destroy(_texture);

            CreateTexture();
            _camera.targetTexture = _texture;
            _rawImage.texture = _texture;
        }

        // 유닛이 바라보는 방향의 정면, 약간 위에서 유닛 중심을 바라보게 카메라를 놓는다. 드래그로 돌린 각도만큼 유닛 둘레를 돈다.
        void UpdateCameraPose()
        {
            Transform unitTransform = _unit.transform;
            Vector3 focus = unitTransform.TransformPoint(_localCenter);
            Vector3 direction = unitTransform.rotation * (Quaternion.Euler(-(_pitchAngle + _orbitPitch), _orbitYaw, 0f) * Vector3.forward);

            Transform cameraTransform = _camera.transform;
            cameraTransform.position = focus + direction * _distance;
            cameraTransform.rotation = Quaternion.LookRotation(focus - cameraTransform.position, Vector3.up);
        }

        // 옮겼던 오브젝트를 원래 레이어로 되돌린다. 풀 한도를 넘어 파괴된 오브젝트는 건너뛴다.
        void RestoreLayers()
        {
            for (int i = 0; i < _layerObjects.Count; i++)
            {
                if (_layerObjects[i] != null) _layerObjects[i].layer = _originalLayers[i];
            }

            _layerObjects.Clear();
            _originalLayers.Clear();
        }
    }
}