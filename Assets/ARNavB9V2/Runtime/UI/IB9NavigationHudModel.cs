using System;
using System.Collections.Generic;
using UnityEngine;

namespace ARNavB9V2.UI
{
    /// <summary>
    /// The navigation HUD as a view sees it: a state to draw, the destination list and the commands behind each
    /// button. <see cref="B9NavigationHud"/> implements it over the scene's controllers; tests use a fake.
    /// </summary>
    public interface IB9NavigationHudModel
    {
        B9NavigationHudState State { get; }

        /// <summary>Labels for the destination picker ("[Trong] B9 · B9-104", "[Ngoài] Thư viện").</summary>
        IReadOnlyList<string> DestinationChoices { get; }

        string SelectedDestination { get; }

        /// <summary>Codes of the HARMONY versions the research selector offers, in button order.</summary>
        IReadOnlyList<string> HarmonyVersionCodes { get; }

        /// <summary>Top-down campus or indoor map; null until the minimap camera renders.</summary>
        Texture MinimapTexture { get; }

        /// <summary>Raised after every refresh (five times a second) and after each command.</summary>
        event Action Changed;

        /// <summary>Picks a destination from <see cref="DestinationChoices"/> and starts guiding to it.</summary>
        void SelectDestination(string choice);

        /// <summary>"Bắt đầu / đổi điểm đến": starts again towards the selected destination.</summary>
        void StartNavigation();

        void CancelNavigation();

        void ExitBuilding();

        void RetryVps();

        void ToggleMinimap();

        void SelectHarmonyVersion(string versionCode);

        void AddResearchMarker();

        void ToggleTrial();

        void ExportLogs();
    }
}
