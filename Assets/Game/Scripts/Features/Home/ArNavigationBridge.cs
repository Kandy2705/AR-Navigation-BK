using System;
using UnityEngine;

namespace Game.Features.Home
{
    /// <summary>
    /// Connects the app shell's AR page (<see cref="ArHudUI"/>, AppShell scene) with the guidance overlay
    /// (<see cref="NavigationHudUI"/>, B9 navigation scene). The two live in different scenes, so neither holds a
    /// reference to the other: the HUD publishes the destination, the AR page asks to clear the route, and the
    /// HUD hides while another app page covers the camera.
    /// </summary>
    public static class ArNavigationBridge
    {
        /// <summary>True while an AR page exists (the app shell is running), false when the B9 scene runs on its own.</summary>
        public static bool ShellAttached { get; private set; }

        public static bool ArPageVisible { get; private set; }

        /// <summary>Room or place being guided to; empty without an active route.</summary>
        public static string Destination { get; private set; } = string.Empty;

        /// <summary>The guidance overlay shows on its own, or inside the shell while the AR page is open.</summary>
        public static bool HudVisible => !ShellAttached || ArPageVisible;

        /// <summary>Raised when <see cref="ShellAttached"/> or <see cref="ArPageVisible"/> changes.</summary>
        public static event Action VisibilityChanged;

        public static event Action<string> DestinationChanged;

        /// <summary>The AR page's "Xóa chỉ đường" was tapped: the navigation scene stops guiding.</summary>
        public static event Action ClearRouteRequested;

        public static void AttachShell()
        {
            if (ShellAttached)
            {
                return;
            }

            ShellAttached = true;
            VisibilityChanged?.Invoke();
        }

        public static void DetachShell()
        {
            if (!ShellAttached && !ArPageVisible)
            {
                return;
            }

            ShellAttached = false;
            ArPageVisible = false;
            VisibilityChanged?.Invoke();
        }

        public static void SetArPageVisible(bool visible)
        {
            if (ArPageVisible == visible)
            {
                return;
            }

            ArPageVisible = visible;
            VisibilityChanged?.Invoke();
        }

        public static void PublishDestination(string destination)
        {
            string value = destination?.Trim() ?? string.Empty;
            if (value == Destination)
            {
                return;
            }

            Destination = value;
            DestinationChanged?.Invoke(value);
        }

        public static void RequestClearRoute()
        {
            ClearRouteRequested?.Invoke();
        }

        /// <summary>Forgets the state and every listener (tests, and entering Play mode without a domain reload).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset()
        {
            ShellAttached = false;
            ArPageVisible = false;
            Destination = string.Empty;
            VisibilityChanged = null;
            DestinationChanged = null;
            ClearRouteRequested = null;
        }
    }
}
