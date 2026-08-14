using ARNav.Harmony;
using NUnit.Framework;
using UnityEngine;

public class HarmonyCoreTests
{
    private HarmonyConfig _config;

    [SetUp]
    public void SetUp()
    {
        _config = HarmonyConfig.CreateRuntimeDefaults();
        _config.vpsDwellSeconds = 1f;
        _config.gpsDwellSeconds = 1f;
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_config);
    }

    [Test]
    public void Evaluate_WhenVpsConfidenceUnavailable_ReportsSdkBlocker()
    {
        var evaluator = new LocalizationReliabilityEvaluator();
        HarmonyVpsSample vps = ValidVps(timestamp: 1d);
        vps.ConfidenceAvailable = false;

        HarmonyReliabilitySnapshot result = evaluator.Evaluate(
            ValidGps(1d),
            vps,
            1f,
            5f,
            HarmonyLocalizationSource.VPS,
            _config,
            1f);

        Assert.That(result.VpsReason, Is.EqualTo("VPS confidence unavailable from SDK"));
        Assert.That(result.Vps, Is.LessThan(_config.vpsEnterReliability));
    }

    [Test]
    public void Evaluate_WhenVpsIsStableAndVerified_BecomesReliableAfterDwell()
    {
        var evaluator = new LocalizationReliabilityEvaluator();
        evaluator.Evaluate(
            ValidGps(1d), ValidVps(1d), 1f, 5f,
            HarmonyLocalizationSource.VPS, _config, 1f);

        HarmonyReliabilitySnapshot result = evaluator.Evaluate(
            ValidGps(2d), ValidVps(2d), 1f, 5f,
            HarmonyLocalizationSource.VPS, _config, 2.1f);

        Assert.That(result.VpsStableSeconds, Is.GreaterThanOrEqualTo(1f));
        Assert.That(result.Vps, Is.GreaterThanOrEqualTo(_config.vpsEnterReliability));
        Assert.That(result.VpsReason, Does.StartWith("VPS reliability"));
    }

    [Test]
    public void Evaluate_WhenVpsPoseJumps_ReliabilityDropsAndDwellResets()
    {
        var evaluator = new LocalizationReliabilityEvaluator();
        evaluator.Evaluate(
            ValidGps(1d), ValidVps(1d), 1f, 5f,
            HarmonyLocalizationSource.VPS, _config, 1f);

        HarmonyVpsSample jumped = ValidVps(2d);
        jumped.CampusPosition = new Vector3(20f, 0f, 0f);
        HarmonyReliabilitySnapshot result = evaluator.Evaluate(
            ValidGps(2d), jumped, 1f, 5f,
            HarmonyLocalizationSource.VPS, _config, 2f);

        Assert.That(result.VpsPositionDeltaMeters, Is.GreaterThan(10f));
        Assert.That(result.VpsStableSeconds, Is.Zero);
        Assert.That(result.Vps, Is.LessThan(_config.vpsEnterReliability));
    }

    [Test]
    public void TryTransition_WhenModeDurationNotMet_BlocksSourceFlap()
    {
        var machine = new HarmonyStateMachine();
        machine.Initialize(0f);

        bool early = machine.TryTransition(
            HarmonyState.Indoor,
            "early",
            2f,
            0.25f,
            8f,
            changesLocalizationSource: true);
        bool accepted = machine.TryTransition(
            HarmonyState.Indoor,
            "stable",
            8.1f,
            0.25f,
            8f,
            changesLocalizationSource: true);
        bool flap = machine.TryTransition(
            HarmonyState.Outdoor,
            "flap",
            9f,
            0.25f,
            8f,
            changesLocalizationSource: true);

        Assert.That(early, Is.False);
        Assert.That(accepted, Is.True);
        Assert.That(flap, Is.False);
        Assert.That(machine.Current, Is.EqualTo(HarmonyState.Indoor));
    }

    private static HarmonyGpsSample ValidGps(double timestamp)
    {
        return new HarmonyGpsSample
        {
            IsValid = true,
            CampusPosition = Vector3.zero,
            CampusRotation = Quaternion.identity,
            HasHeading = true,
            HeadingDegrees = 0f,
            HorizontalAccuracyMeters = 2f,
            AgeSeconds = 0.1f,
            Timestamp = timestamp,
        };
    }

    private static HarmonyVpsSample ValidVps(double timestamp)
    {
        return new HarmonyVpsSample
        {
            IsValid = true,
            CampusPosition = Vector3.zero,
            CampusRotation = Quaternion.identity,
            MapLocalPosition = Vector3.zero,
            MapLocalRotation = Quaternion.identity,
            Confidence = 0.95f,
            ConfidenceAvailable = true,
            MapId = "actual-map-id",
            MapIdAvailable = true,
            MapMatchesBuilding = true,
            AgeSeconds = 0.1f,
            Timestamp = timestamp,
        };
    }
}
