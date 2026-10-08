using Fusion;
using Game.Core;
using Game.Networking;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Combat.Player
{
    public sealed class PlayerBuffable : ServerAuthoritativeBehaviour, IBuffable
    {
        private class RuntimeBuffInfo
        {
            public BuffType Type;
            public int Value;
            public int DurationTicks;
            public bool Cleansable;
            public int SourceId;
            public bool IsActive;
        }

        private Dictionary<BuffType, RuntimeBuffInfo> buffInfos = new();
        private Dictionary<BuffType, Action<BuffInfo>> Buff_ApplyHandlers;
        private Dictionary<BuffType, Action> Buff_TickHandlers;

        private PlayerLifeCycle _lifeCycle;
        private bool _wasDead;

        private int PlayerId => Object.InputAuthority.PlayerId;

        public override void Spawned()
        {
            if (!HasStateAuthority) return;

            _lifeCycle = GetComponent<PlayerLifeCycle>();

            Buff_ApplyHandlers = new()
            {
                { BuffType.Heal, Buff_ApplyHandler_Heal },
                { BuffType.DamageIncrese, Buff_ApplyHandler_DamageIncrease }
            };
            Buff_TickHandlers = new()
            {
                { BuffType.DamageIncrese, Buff_TickHandler_DamageIncrease }
            };
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;

            bool dead = _lifeCycle != null && _lifeCycle.IsDead;
            if (dead)
            {
                if (!_wasDead)
                {
                    foreach (var key in buffInfos.Keys)
                    {
                        if (buffInfos[key].IsActive == false) continue;
                        buffInfos[key].IsActive = false;

                        RPC_NotifyBuffRemoved(PlayerId, key);
                    }
                    _wasDead = true;
                }
                return;
            }
            _wasDead = false;

            foreach (var key in Buff_TickHandlers.Keys)
            {
                if (!buffInfos.TryGetValue(key, out var info) || !info.IsActive) continue;
                Buff_TickHandlers[key]?.Invoke();
            }
        }

        public void ApplyBuff(in BuffInfo info)
        {
            if (!HasStateAuthority) return;
            if (_lifeCycle != null && _lifeCycle.IsDead) return;

            Buff_ApplyHandlers[info.Type]?.Invoke(info);
        }

        private void Buff_ApplyHandler_Heal(BuffInfo info)
        {
            if (_lifeCycle != null)
            {
                _lifeCycle.Heal(info.Value);
                // 힐은 지속 버프 아님
                //RPC_NotifyBuffApplied(PlayerId, info.Type, info.Value, info.DurationTicks, info.Cleansable, info.SourceId);
            }
        }

        private void Buff_ApplyHandler_DamageIncrease(BuffInfo info)
        {
            var runtimeInfo = EnsureBuffInfo(info.Type);
            runtimeInfo.IsActive = true;
            runtimeInfo.Value = info.Value;
            runtimeInfo.DurationTicks = info.DurationTicks;
            runtimeInfo.Cleansable = info.Cleansable;
            runtimeInfo.SourceId = info.SourceId;

            // 적용은 PlayerCombatStats가 받아서 한다. 수명은 여기(버프 소유자)가 갖고,
            // 만료 시 값 0으로 한 번 더 쏴서 걷는다 — 타이머를 양쪽에 두면 소유자가 둘이 된다.
            GameplayEvents.RaiseTemporaryStatRequested(
                PlayerId,
                new StatModifier(StatType.AttackPower, info.Value),
                info.DurationTicks);

            RPC_NotifyBuffApplied(PlayerId, info.Type, info.Value, info.DurationTicks, info.Cleansable, info.SourceId);
        }

        private void Buff_TickHandler_DamageIncrease()
        {
            var info = buffInfos[BuffType.DamageIncrese];
            if (!info.IsActive) return;

            if (--info.DurationTicks <= 0)
            {
                info.IsActive = false;

                // 지속 0 = 걷으라는 뜻. 적용한 쪽(PlayerCombatStats)에 만료를 알린다.
                GameplayEvents.RaiseTemporaryStatRequested(
                    PlayerId, new StatModifier(StatType.AttackPower, 0), 0);

                RPC_NotifyBuffRemoved(PlayerId, BuffType.DamageIncrese);
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_NotifyBuffApplied(int targetId, BuffType type, int value, int durationTicks, bool cleansable, int sourceId)
            => GameplayEvents.RaiseBuffApplied(targetId, new BuffInfo(type, value, durationTicks, cleansable, sourceId));

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_NotifyBuffRemoved(int targetId, BuffType type)
            => GameplayEvents.RaiseBuffRemoved(targetId, type);

        private RuntimeBuffInfo EnsureBuffInfo(BuffType type)
        {
            if (buffInfos.ContainsKey(type))
                return buffInfos[type];

            var b = new RuntimeBuffInfo() { Type = type };
            buffInfos.Add(type, b);
            return b;
        }
    }
}
