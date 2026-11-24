using System;

namespace Defense.Units
{
    /// <summary>
    /// 현재 보유 재화와 변경 알림을 제공하는 쪽(시스템 쪽 지갑). 강화 버튼이 살 수 있는지 표시할 때 읽는다.
    /// 지출은 IWallet이 담당하고, 이 인터페이스는 읽기 전용이다.
    /// </summary>
    public interface IBalanceSource
    {
        int Balance { get; }

        /// <summary>재화가 바뀔 때마다 호출된다.</summary>
        event Action Changed;
    }
}
