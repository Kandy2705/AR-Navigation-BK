using System;

namespace ARNavB9V2.Reliability
{
    public enum B9NavigationState
    {
        OutdoorGps,
        EnteringWithPdr,
        VpsScanning,
        IndoorVps,
        ExitingWithPdr,
        VpsFailed,
    }

    public enum B9PoseSource
    {
        Gps,
        Pdr,
        Vps,
    }

    public readonly struct B9ReliabilityTransition
    {
        public B9ReliabilityTransition(
            B9NavigationState previous,
            B9NavigationState current,
            B9PoseSource source,
            string reason,
            float timestamp)
        {
            Previous = previous;
            Current = current;
            Source = source;
            Reason = reason ?? string.Empty;
            Timestamp = timestamp;
        }

        public B9NavigationState Previous { get; }
        public B9NavigationState Current { get; }
        public B9PoseSource Source { get; }
        public string Reason { get; }
        public float Timestamp { get; }
    }

    public sealed class B9NavigationStateMachine
    {
        public B9NavigationState Current { get; private set; } = B9NavigationState.OutdoorGps;

        public bool TryTransition(B9NavigationState next)
        {
            if (next == Current || !IsAllowed(Current, next))
                return false;

            Current = next;
            return true;
        }

        private static bool IsAllowed(B9NavigationState current, B9NavigationState next)
        {
            return current switch
            {
                B9NavigationState.OutdoorGps => next == B9NavigationState.EnteringWithPdr,
                B9NavigationState.EnteringWithPdr => next == B9NavigationState.OutdoorGps
                                                     || next == B9NavigationState.VpsScanning,
                B9NavigationState.VpsScanning => next == B9NavigationState.IndoorVps
                                                 || next == B9NavigationState.VpsFailed
                                                 || next == B9NavigationState.OutdoorGps,
                B9NavigationState.VpsFailed => next == B9NavigationState.VpsScanning
                                               || next == B9NavigationState.IndoorVps
                                               || next == B9NavigationState.OutdoorGps,
                B9NavigationState.IndoorVps => next == B9NavigationState.ExitingWithPdr,
                B9NavigationState.ExitingWithPdr => next == B9NavigationState.OutdoorGps,
                _ => false,
            };
        }
    }
}
