using System;

namespace ARNav.Harmony
{
    public sealed class HarmonyStateMachine
    {
        public HarmonyState Current { get; private set; } = HarmonyState.Outdoor;
        public float StateEnteredAt { get; private set; }
        public float LastModeChangedAt { get; private set; }
        public string LastReason { get; private set; } = "boot";

        public event Action<HarmonyStateTransition> Changed;

        public void Initialize(float now)
        {
            Current = HarmonyState.Outdoor;
            StateEnteredAt = now;
            LastModeChangedAt = now;
            LastReason = "boot";
        }

        public bool TryTransition(
            HarmonyState next,
            string reason,
            float now,
            float minimumStateDuration,
            float minimumModeDuration,
            bool changesLocalizationSource,
            bool force = false)
        {
            if (next == Current) return false;
            if (!force && now - StateEnteredAt < minimumStateDuration) return false;
            if (!force && changesLocalizationSource &&
                now - LastModeChangedAt < minimumModeDuration)
            {
                return false;
            }

            HarmonyState previous = Current;
            Current = next;
            StateEnteredAt = now;
            LastReason = reason ?? string.Empty;
            if (changesLocalizationSource) LastModeChangedAt = now;
            Changed?.Invoke(new HarmonyStateTransition(previous, next, LastReason, now));
            return true;
        }

        public float StateAge(float now) => Math.Max(0f, now - StateEnteredAt);
        public float ModeAge(float now) => Math.Max(0f, now - LastModeChangedAt);
    }
}
