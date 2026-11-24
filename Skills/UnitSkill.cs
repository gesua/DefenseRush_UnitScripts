using System.Collections.Generic;
using UnityEngine;

namespace Defense.Units
{
    public enum SkillVisualKind { Critical, Heavy, Explosion, Slow, Plunder, Loot, Lightning, Slash, Buff, Frenzy, Shockwave, Bleed, ArmorBreak, Execute }

    /// <summary>평상시 바닥에 영향 범위를 표시하는 모양. Tile은 상시 효과(오라), Brackets는 확률로 걸리는 효과(일시 버프)에 쓴다.</summary>
    public enum FloorMarkStyle { Tile, Brackets }

    /// <summary>
    /// 유닛 스킬(패시브)의 공통 부모. 스킬 한 종류가 에셋 하나이고, 여러 유닛이 같은 에셋을 같이 쓴다.
    /// 그래서 스킬 에셋 안에는 유닛별 상태를 저장하지 않는다.
    /// 유닛의 UnitData.Skills에 넣으면 해당 유닛이 알아서 호출한다.
    /// </summary>
    public abstract class UnitSkill : ScriptableObject
    {
        /// <summary>성공한 스킬만 보내는 연출 전용 알림. 구독자가 없어도 실제 스킬은 그대로 작동한다.</summary>
        public static event System.Action<SkillVisualKind, Unit, IMonster, float> VisualTriggered;

        /// <summary>성공한 스킬의 연출을 위치로 알린다. 대상 몬스터가 이미 죽어 넘길 수 없을 때(시체 폭발) 쓴다. 구독자가 없어도 실제 스킬은 그대로 작동한다.</summary>
        public static event System.Action<SkillVisualKind, Unit, Vector3, float> VisualTriggeredAt;

        /// <summary>
        /// 스킬이 실제로 발동했을 때 부른다. 유물의 스킬 골드 획득 판정을 한다.
        /// 연출이 있는 스킬은 NotifyVisual이 대신 불러 주므로 따로 부르지 않는다. (연출이 없는 스킬만 직접 부른다)
        /// </summary>
        protected static void NotifyTriggered(Unit unit)
        {
            if (unit != null) unit.RollSkillGold();
        }

        protected static void NotifyVisual(SkillVisualKind kind, Unit unit, IMonster target = null, float radius = 0f)
        {
            // 연출이 있는 스킬은 성공했을 때만 여기로 오므로, 스킬 골드 판정도 함께 한다.
            NotifyTriggered(unit);

            // 임시 연출의 오류가 피해 계산이나 재화 지급을 중단시키지 않게 한다.
            try { VisualTriggered?.Invoke(kind, unit, target, radius); }
            catch (System.Exception exception) { Debug.LogException(exception); }
        }

        /// <summary>NotifyVisual의 위치 버전. 스킬 골드 판정도 함께 한다.</summary>
        protected static void NotifyVisualAt(SkillVisualKind kind, Unit unit, Vector3 position, float radius = 0f)
        {
            NotifyTriggered(unit);

            try { VisualTriggeredAt?.Invoke(kind, unit, position, radius); }
            catch (System.Exception exception) { Debug.LogException(exception); }
        }

        [SerializeField] string _displayName;
        [TextArea][SerializeField] string _description;
        [SerializeField] Sprite _icon;

        public string DisplayName => _displayName;
        public string Description => _description;
        public Sprite Icon => _icon;

        /// <summary>유닛이 새로 만들어진 직후 한 번 호출된다. (소환, 승급으로 만들어질 때)</summary>
        public virtual void OnSpawned(Unit unit) { }

        /// <summary>유닛이 공격할 때마다 호출된다. context.Damage를 바꾸면 최종 대미지에 반영된다.</summary>
        public virtual void OnAttack(AttackContext context) { }

        /// <summary>유닛이 적을 처치한 직후 호출된다. target은 이미 죽었으므로 Position에 접근하지 않는다. (광분 같은 처치 스킬용)</summary>
        public virtual void OnKill(Unit unit, IMonster target) { }

        /// <summary>
        /// 유닛이 적을 처치한 직후, 적이 죽은 자리(deathPosition)와 함께 호출된다. 처치한 자리가 필요한 스킬(시체 폭발)이 override한다.
        /// 기본 구현은 위치 없는 OnKill을 부르므로, 기존 처치 스킬(광분 등)은 그대로 작동한다.
        /// </summary>
        public virtual void OnKill(Unit unit, IMonster target, Vector3 deathPosition) => OnKill(unit, target);

        /// <summary>새 라운드가 시작될 때 필드에 있는 유닛마다 호출된다. (수확 같은 라운드 시작 스킬용) round는 시작한 라운드 번호.</summary>
        public virtual void OnRoundStart(Unit unit, int round) { }

        /// <summary>
        /// 칸 구성이 바뀔 때마다(소환, 이동, 판매, 승급) 호출된다. 이 유닛이 주는 상시 효과(오라)는 여기서
        /// 대상 유닛의 Modifiers.SetAura로 건다. 호출 전에 모든 유닛의 오라 보정이 지워지므로 매번 처음부터 건다.
        /// </summary>
        public virtual void ApplyAura(Unit unit) { }

        /// <summary>
        /// 평상시 바닥에 표시할 영향 범위를 구한다. 표시할 스킬만 override한다.
        /// 표시할 칸이 있으면 true를 돌려주고, 영향받는 칸을 results에 담는다. (results는 호출한 쪽이 비워서 넘긴다)
        /// 칸 구성이 바뀔 때마다 SlotAuraFloorPresenter가 호출하며, 실제 효과 계산에는 쓰이지 않는다.
        /// </summary>
        public virtual bool TryGetFloorMark(Unit unit, List<Slot> results, out Color color, out FloorMarkStyle style)
        {
            color = Color.white;
            style = FloorMarkStyle.Tile;
            return false;
        }

        // 바닥 표시 기본 색. 공격력은 주황, 공격속도는 하늘색, 둘 다 올리면 노랑이다. (승급 테두리의 청록과 구분)
        static readonly Color _attackFloorColor = new Color(1f, 0.55f, 0.15f, 1f);
        static readonly Color _speedFloorColor = new Color(0.2f, 0.8f, 1f, 1f);
        static readonly Color _bothFloorColor = new Color(1f, 0.9f, 0.2f, 1f);

        /// <summary>올려 주는 스탯에 따른 바닥 표시 기본 색. 투명도는 SlotAuraFloorPresenter가 정한다.</summary>
        protected static Color GetStatFloorColor(float attackPowerRatio, float attackSpeedRatio)
        {
            bool raisesAttack = attackPowerRatio > 0f;
            bool raisesSpeed = attackSpeedRatio > 0f;

            if (raisesAttack && raisesSpeed) return _bothFloorColor;
            return (raisesSpeed) ? _speedFloorColor : _attackFloorColor;
        }
    }
}