using System.Collections.Generic;
using UnityEngine;

namespace Defense.Units
{
    /// <summary>
    /// 유닛이 공격 대상으로 보는 몬스터의 최소 계약. (몬스터/시스템 담당과 합의해서 확정할 것)
    /// 죽은 몬스터는 IsDead가 true여야 하고, IsDead일 때는 Position에 접근하지 않는다.
    /// </summary>
    public interface IMonster
    {
        Vector3 Position { get; }

        /// <summary>경로 진행도. 클수록 출구에 가깝다. (타겟 우선순위용, 구현 방식은 자유)</summary>
        float PathProgress { get; }

        bool IsDead { get; }

        void TakeDamage(float damage);
    }

    /// <summary>현재 살아있는 몬스터 목록을 읽기 전용으로 제공한다. (몬스터/시스템 쪽 매니저가 구현)</summary>
    public interface IMonsterRegistry
    {
        IReadOnlyList<IMonster> ActiveMonsters { get; }
    }
}
