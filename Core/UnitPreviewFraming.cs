using UnityEngine;

namespace Defense.Units
{
    /// <summary>
    /// 유닛 프리뷰 카메라가 유닛을 어떻게 잡을지 정한 값. UnitData에 저장되고, 에디터의 Unit Preview Framing Baker가 Idle 자세로 재서 채운다.
    /// 거리가 아니라 중심과 크기를 저장하므로, 시야각·여유 배율·RawImage 비율을 바꿔도 다시 굽지 않아도 된다. (거리는 UnitPreviewController가 계산한다)
    /// </summary>
    [System.Serializable]
    public struct UnitPreviewFraming
    {
        [Tooltip("프리뷰 카메라가 바라볼 중심. 유닛 로컬 좌표이고, 가로(x, z)는 발 위치(0)에 고정한다.")]
        public Vector3 Center;

        [Tooltip("유닛을 감싸는 영역의 반 크기(월드 단위). 모두 0이면 아직 굽지 않은 것이라, 프리뷰가 선택한 순간의 자세로 직접 잰다.")]
        public Vector3 Extents;

        [Tooltip("거리 보정 배율. 1보다 크면 유닛이 작게, 작으면 크게 보인다. 굽기를 다시 해도 이 값은 유지된다. (유닛별 손보정용)")]
        [Min(0.1f)] public float DistanceScale;

        /// <summary>미리 구워 둔 값이 있으면 true. 아니면 프리뷰가 실행 중에 직접 잰다.</summary>
        public bool IsBaked => Extents.sqrMagnitude > 0f;

        /// <summary>실제로 쓰는 거리 배율. 기존 에셋처럼 값이 비어 있으면(0) 1로 본다.</summary>
        public float EffectiveDistanceScale => (DistanceScale > 0f) ? DistanceScale : 1f;

        /// <summary>
        /// 유닛 로컬 좌표의 영역(중심과 반 크기)에서 프레이밍을 만든다.
        /// 가로 중심은 유닛 발 위치(피벗)에 고정한다. 공격 모션 중에 팔이나 무기가 한쪽으로 뻗은 상태로 재도, 카메라가 몸 중심을 벗어나지 않게 하기 위해서다.
        /// 세로 중심만 잰 값을 쓰고, 가로로 벗어난 만큼은 화면 폭 계산에 더해 잘리지 않게 한다.
        /// </summary>
        public static UnitPreviewFraming FromLocalBounds(Vector3 localCenter, Vector3 extents)
        {
            return new UnitPreviewFraming
            {
                Center = new Vector3(0f, localCenter.y, 0f),
                Extents = new Vector3(extents.x + Mathf.Abs(localCenter.x), extents.y, extents.z + Mathf.Abs(localCenter.z)),
                DistanceScale = 1f
            };
        }
    }
}
