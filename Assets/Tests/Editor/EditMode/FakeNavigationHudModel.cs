#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using ARNavB9V2.UI;
using UnityEngine;

namespace TestAR.Tests.Editor.EditMode
{
    /// <summary>Navigation HUD logic stand-in: a state the test writes, and a log of the commands the view sent.</summary>
    internal sealed class FakeNavigationHudModel : IB9NavigationHudModel
    {
        public readonly List<string> Commands = new List<string>();
        public List<string> Choices = new List<string> { "[Trong] B9 · B9-101", "[Trong] B9 · B9-104", "[Ngoài] Thư viện" };

        public B9NavigationHudState State { get; } = new B9NavigationHudState();
        public IReadOnlyList<string> DestinationChoices => Choices;
        public string SelectedDestination { get; set; } = "[Trong] B9 · B9-104";
        public IReadOnlyList<string> HarmonyVersionCodes { get; } = new[] { "V1", "V2", "V5" };
        public Texture MinimapTexture { get; set; }

        public event Action Changed;

        /// <summary>Lets the view redraw as it does when the scene's HUD refreshes.</summary>
        public void RaiseChanged()
        {
            Changed?.Invoke();
        }

        public void SelectDestination(string choice)
        {
            SelectedDestination = choice;
            Commands.Add("select:" + choice);
        }

        public void StartNavigation() => Commands.Add("start");
        public void CancelNavigation() => Commands.Add("cancel");
        public void ExitBuilding() => Commands.Add("exit");
        public void RetryVps() => Commands.Add("retry");
        public void ToggleMinimap() => Commands.Add("minimap");
        public void SelectHarmonyVersion(string versionCode) => Commands.Add("version:" + versionCode);
        public void AddResearchMarker() => Commands.Add("marker");
        public void ToggleTrial() => Commands.Add("trial");
        public void ExportLogs() => Commands.Add("export");
    }
}
#endif
