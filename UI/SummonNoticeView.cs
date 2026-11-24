using UnityEngine;

namespace Defense.Units
{
    /// <summary>
    /// 일반 소환이 막혔을 때(빈 칸 없음) 안내 글자를 잠깐 띄운다.
    /// 소환 버튼과 같은 화면의, 항상 켜져 있는 오브젝트에 붙인다.
    /// </summary>
    public class SummonNoticeView : ToastView
    {
        [SerializeField] SummonManager _manager;

        bool HasReferences => _manager != null && HasView;

        void Awake()
        {
            if (HasReferences == false)
                Debug.LogError("[SummonNoticeView] manager / frame / label 참조가 비어 있거나, frame이 이 오브젝트(또는 부모)이거나, label이 frame 안에 있지 않습니다.", this);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (HasReferences == false) return;

            _manager.NoSpace += ShowNoSpace;
            _manager.UnitLimitReached += ShowUnitLimit;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (HasReferences == false) return;

            _manager.NoSpace -= ShowNoSpace;
            _manager.UnitLimitReached -= ShowUnitLimit;
        }
    }
}