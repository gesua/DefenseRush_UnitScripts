#if UNITY_EDITOR
using UnityEngine;

namespace Defense.Units
{
    /// <summary>개발용 유닛 지정 소환 치트를 모은다. 에디터 플레이 중 Inspector 버튼에서만 쓴다.</summary>
    public partial class SummonManager
    {
        /// <summary>
        /// 고른 유닛을 재화·확률 없이 count마리 소환한다. 실제로 소환한 마릿수를 돌려준다.
        /// 마릿수 제한과 칸 자리는 일반 소환과 같은 규칙을 따르며, 막히면 같은 안내 이벤트를 보내고 거기서 멈춘다.
        /// </summary>
        public int CheatSummon(UnitData data, int count)
        {
            if (Application.isPlaying == false || isActiveAndEnabled == false) return 0;
            if (_grid == null || data == null || data.Prefab == null) return 0;

            int summoned = 0;
            for (int i = 0; i < count; i++)
            {
                if (_grid.IsUnitLimitReached)
                {
                    UnitLimitReached?.Invoke();
                    break;
                }

                // 같은 종류가 쌓일 칸을 먼저, 없으면 첫 빈 칸. 일반 소환과 같은 기준이다.
                Slot slot = _grid.FindSlotFor(data);
                if (slot == null)
                {
                    NoSpace?.Invoke();
                    break;
                }

                if (_grid.SpawnUnit(data, slot) == null) break;
                summoned++;
            }
            return summoned;
        }
    }
}
#endif
