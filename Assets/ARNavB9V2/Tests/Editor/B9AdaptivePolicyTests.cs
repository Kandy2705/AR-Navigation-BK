using System;
using System.IO;
using ARNavB9V2.Adaptive;
using ARNavB9V2.Experiment;
using ARNavB9V2.Reliability;
using NUnit.Framework;
using UnityEngine;

namespace ARNavB9V2.Tests
{
    /// <summary>
    /// Parity with the Python training pipeline. The golden file is written by
    /// export_policy_unity.py together with the shipped policy JSON.
    /// </summary>
    public sealed class B9AdaptivePolicyTests
    {
        private const string PolicyPath =
            "ARNavB9V2/Resources/ARNavB9V2Policy/b9_handover_policy.json";
        private const string GoldenPath = "ARNavB9V2/Tests/Editor/Data/b9_policy_golden.json";
        private const float ActionTolerance = 1e-4f;
        private const float ParameterTolerance = 1e-4f;
        private const float ObservationTolerance = 1e-5f;

        [Serializable]
        private sealed class Golden
        {
            public string policyVersion;
            public ForwardCase[] forwardCases;
            public ObservationCase[] observationCases;
        }

        [Serializable]
        private sealed class ForwardCase
        {
            public int episode;
            public int step;
            public float[] observation;
            public float[] action;
            public float[] parameters;
        }

        [Serializable]
        private sealed class ObservationCase
        {
            public int trial;
            public int row;
            public string state;
            public string activeSource;
            public float[] raw;
            public float[] observation;
        }

        private static B9PolicyModel LoadPolicy()
        {
            return B9PolicyModel.FromJson(File.ReadAllText(Path.Combine(Application.dataPath, PolicyPath)));
        }

        private static Golden LoadGolden()
        {
            var golden = JsonUtility.FromJson<Golden>(
                File.ReadAllText(Path.Combine(Application.dataPath, GoldenPath)));
            Assert.IsNotEmpty(golden.forwardCases);
            Assert.IsNotEmpty(golden.observationCases);
            return golden;
        }

        [Test]
        public void PolicyModel_WhenLoadedWithGolden_HasSameVersion()
        {
            Assert.AreEqual(LoadGolden().policyVersion, LoadPolicy().policyVersion);
        }

        [Test]
        public void PolicyModel_WhenRunOnGoldenObservations_MatchesPythonActions()
        {
            B9PolicyModel model = LoadPolicy();
            float worst = 0f;
            foreach (ForwardCase c in LoadGolden().forwardCases)
            {
                float[] action = model.Forward(c.observation);
                Assert.AreEqual(c.action.Length, action.Length);
                for (int i = 0; i < action.Length; i++)
                    worst = Mathf.Max(worst, Mathf.Abs(action[i] - c.action[i]));
            }
            Assert.Less(worst, ActionTolerance, $"max |C# - Python| action difference {worst:E2}");
        }

        [Test]
        public void ActionDecoder_WhenReplayingGoldenEpisodes_MatchesPythonParameters()
        {
            var decoder = new B9PolicyActionDecoder(LoadPolicy());
            B9HandoverGateParameters fallback = B9HandoverGateParameters.FromProfile(
                B9HarmonyExperimentProfile.For(B9HarmonyVersion.V6_AdaptivePolicy));
            int episode = -1;
            float worst = 0f;
            foreach (ForwardCase c in LoadGolden().forwardCases)
            {
                if (c.episode != episode)
                {
                    decoder.Reset();
                    episode = c.episode;
                }
                B9HandoverGateParameters p = decoder.Decode(c.action, fallback);
                worst = Mathf.Max(worst, Mathf.Abs(p.GpsThreshold - c.parameters[0]));
                worst = Mathf.Max(worst, Mathf.Abs(p.VpsThreshold - c.parameters[1]));
                worst = Mathf.Max(worst, Mathf.Abs(p.GpsDwellSeconds - c.parameters[2]));
                worst = Mathf.Max(worst, Mathf.Abs(p.VpsDwellSeconds - c.parameters[3]));
            }
            Assert.Less(worst, ParameterTolerance, $"max parameter difference {worst:E2}");
        }

        [Test]
        public void ObservationBuilder_WhenReplayingLoggedRows_MatchesPythonObservations()
        {
            var builder = new B9PolicyObservationBuilder();
            int trial = -1;
            foreach (ObservationCase c in LoadGolden().observationCases)
            {
                if (c.trial != trial)
                {
                    builder.Reset();
                    trial = c.trial;
                }
                float[] obs = builder.Build(ToSample(c));
                for (int i = 0; i < obs.Length; i++)
                {
                    Assert.AreEqual(c.observation[i], obs[i], ObservationTolerance,
                        $"trial {c.trial} row {c.row} feature {i}");
                }
            }
        }

        [Test]
        public void ObservationBuilder_WhenStateIsExitingWithPdr_EncodesNoStateLikeTraining()
        {
            var builder = new B9PolicyObservationBuilder();
            float[] obs = builder.Build(new B9PolicySample
            {
                state = B9NavigationState.ExitingWithPdr.ToString(),
                activeSource = B9PoseSource.Pdr.ToString(),
            });
            for (int i = 18; i < 24; i++)
                Assert.AreEqual(0f, obs[i], $"state one-hot {i}");
            Assert.AreEqual(1f, obs[25], "Pdr source one-hot");
        }

        [Test]
        public void ActionDecoder_WhenActionOutOfRange_StaysInsideSafeBounds()
        {
            B9PolicyModel model = LoadPolicy();
            var decoder = new B9PolicyActionDecoder(model);
            var action = new float[model.actionSize];
            for (int i = 0; i < action.Length; i++)
                action[i] = 50f;
            B9HandoverGateParameters p = decoder.Decode(action, default);
            Assert.LessOrEqual(p.GpsThreshold, model.gpsThresholdRange[1]);
            Assert.LessOrEqual(p.VpsThreshold, model.vpsThresholdRange[1]);
            Assert.LessOrEqual(p.GpsDwellSeconds, model.gpsDwellRange[1]);
            Assert.LessOrEqual(p.VpsDwellSeconds, model.vpsDwellRange[1]);
        }

        [Test]
        public void AdaptivePolicy_WhenNotActivated_ReturnsProfileParameters()
        {
            var root = new GameObject("Adaptive policy fixture");
            try
            {
                var policy = root.AddComponent<B9AdaptiveHandoverPolicy>();
                B9HarmonyExperimentProfile profile =
                    B9HarmonyExperimentProfile.For(B9HarmonyVersion.V6_AdaptivePolicy);
                B9HandoverGateParameters p = policy.GetGateParameters(profile);
                Assert.AreEqual(profile.VpsEnterReliability, p.VpsThreshold);
                Assert.AreEqual(profile.GpsExitReliability, p.GpsThreshold);
                Assert.AreEqual(profile.VpsDwellSeconds, p.VpsDwellSeconds);
                Assert.AreEqual(profile.GpsDwellSeconds, p.GpsDwellSeconds);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Profile_WhenTunedFixed_UsesValTunedGateConstants()
        {
            var profile = B9HarmonyExperimentProfile.For(B9HarmonyVersion.TF_TunedFixed);
            Assert.AreEqual("TF", profile.VersionCode);
            Assert.AreEqual(0.82f, profile.VpsEnterReliability);
            Assert.AreEqual(3.5f, profile.VpsDwellSeconds);
            Assert.AreEqual(0.75f, profile.GpsExitReliability);
            Assert.AreEqual(5f, profile.GpsDwellSeconds);
            Assert.AreEqual(2f, profile.VpsDwellScoreSeconds);
            Assert.AreEqual(3f, profile.GpsDwellScoreSeconds);
            Assert.IsFalse(profile.UsesAdaptivePolicy);
        }

        [TestCase(B9HarmonyVersion.V1_FixedGeometric)]
        [TestCase(B9HarmonyVersion.V2_ReliableHandover)]
        [TestCase(B9HarmonyVersion.V3_NoDwellTime)]
        [TestCase(B9HarmonyVersion.V4_NoMapIdCheck)]
        [TestCase(B9HarmonyVersion.V5_FullHarmony)]
        [TestCase(B9HarmonyVersion.BQ_QualityThreshold)]
        [TestCase(B9HarmonyVersion.BT_QualityDwell)]
        public void Profile_WhenExistingVersion_KeepsReliabilityDwellNormalizer(B9HarmonyVersion version)
        {
            var profile = B9HarmonyExperimentProfile.For(version);
            Assert.AreEqual(profile.VpsDwellSeconds, profile.VpsDwellScoreSeconds);
            Assert.AreEqual(profile.GpsDwellSeconds, profile.GpsDwellScoreSeconds);
            Assert.IsFalse(profile.UsesAdaptivePolicy);
        }

        private static B9PolicySample ToSample(ObservationCase c)
        {
            // Column order: export_policy_unity.RAW_COLUMNS.
            float[] r = c.raw;
            return new B9PolicySample
            {
                elapsedSeconds = r[0],
                gpsAccuracyMeters = r[1],
                gpsAgeSeconds = r[2],
                gpsReliability = r[3],
                gpsStableSeconds = r[4],
                gpsValid = r[5] > 0.5f,
                pdrSteps = r[6],
                pdrConfidence = r[7],
                vpsValid = r[8] > 0.5f,
                vpsReliability = r[9],
                vpsConfidence = r[10],
                vpsConfidenceAvailable = r[11] > 0.5f,
                vpsMapMatches = r[12] > 0.5f,
                vpsStableSeconds = r[13],
                vpsScanSeconds = r[14],
                sourceToggleCount = Mathf.RoundToInt(r[15]),
                state = c.state,
                activeSource = c.activeSource,
            };
        }
    }
}
