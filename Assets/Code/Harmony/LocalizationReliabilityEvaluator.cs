using UnityEngine;

namespace ARNav.Harmony
{
    public sealed class LocalizationReliabilityEvaluator
    {
        private HarmonyGpsSample _previousGps;
        private HarmonyVpsSample _previousVps;
        private bool _hasPreviousGps;
        private bool _hasPreviousVps;
        private float _gpsStableSince = -1f;
        private float _vpsStableSince = -1f;

        public HarmonyReliabilitySnapshot Evaluate(
            HarmonyGpsSample gps,
            HarmonyVpsSample vps,
            float distanceToTransitionMeters,
            float transitionRadiusMeters,
            HarmonyLocalizationSource activeSource,
            HarmonyConfig config,
            float now)
        {
            float gpsMotionScore = EvaluateGpsMotion(gps, config, now);
            float vpsMotionScore = EvaluateVpsMotion(
                vps, config, now, out float vpsPositionDelta, out float vpsHeadingDelta);

            float gpsAccuracy = gps.IsValid
                ? DescendingScore(
                    gps.HorizontalAccuracyMeters,
                    config.gpsExcellentAccuracyMeters,
                    config.gpsRejectedAccuracyMeters)
                : 0f;
            float gpsFreshness = gps.IsValid
                ? DescendingScore(
                    gps.AgeSeconds,
                    config.gpsFreshAgeSeconds,
                    config.gpsStaleAgeSeconds)
                : 0f;
            float transitionScore = transitionRadiusMeters > 0f &&
                                    distanceToTransitionMeters <= transitionRadiusMeters
                ? config.gpsNearTransitionScore
                : 1f;
            float gpsDwell = StableScore(_gpsStableSince, config.gpsDwellSeconds, now);

            HarmonyConfig.ReliabilityWeights gw = config.gpsWeights;
            float gpsReliability =
                (gpsAccuracy * gw.accuracyOrConfidence +
                 gpsFreshness * gw.freshnessOrValidity +
                 gpsMotionScore * gw.motionStability +
                 transitionScore * gw.transitionOrMapMatch +
                 gpsDwell * gw.dwellStability) / gw.Sum;

            float validityScore = vps.IsValid
                ? DescendingScore(
                    vps.AgeSeconds,
                    config.vpsFreshAgeSeconds,
                    config.vpsStaleAgeSeconds)
                : 0f;
            
            float mapScore = vps.MapIdAvailable && vps.MapMatchesBuilding ? 1f : 0f;
            float vpsDwell = StableScore(_vpsStableSince, config.vpsDwellSeconds, now);

            HarmonyConfig.ReliabilityWeights vw = config.vpsWeights;
            
            float availableWeightSum = vw.Sum;
            float totalWeightedScore = validityScore * vw.freshnessOrValidity + 
                                       vpsMotionScore * vw.motionStability;

            if (vps.IsValid && vps.ConfidenceAvailable)
            {
                float confidenceScore = Mathf.InverseLerp(config.minimumVpsConfidence, 1f, vps.Confidence);
                totalWeightedScore += confidenceScore * vw.accuracyOrConfidence;
            }
            // Confidence is a required quality signal in the current HARMONY model.
            // If the SDK does not expose it, keep its weight in the denominator and
            // score it as zero. Renormalizing the missing signal away can make an
            // unverified VPS sample exceed the indoor-entry reliability threshold.

            if (config.RequireMapIdMatch)
            {
                totalWeightedScore += mapScore * vw.transitionOrMapMatch;
            }
            else
            {
                availableWeightSum -= vw.transitionOrMapMatch;
            }

            if (config.RequireVpsDwell)
            {
                totalWeightedScore += vpsDwell * vw.dwellStability;
            }
            else
            {
                availableWeightSum -= vw.dwellStability;
            }

            availableWeightSum = Mathf.Max(0.0001f, availableWeightSum);
            float vpsReliability = totalWeightedScore / availableWeightSum;

            gpsReliability = Mathf.Clamp01(gpsReliability);
            vpsReliability = Mathf.Clamp01(vpsReliability);
            float active = activeSource switch
            {
                HarmonyLocalizationSource.GPS => gpsReliability,
                HarmonyLocalizationSource.VPS => vpsReliability,
                HarmonyLocalizationSource.LastTrusted => Mathf.Max(gpsReliability, vpsReliability),
                _ => 0f,
            };

            return new HarmonyReliabilitySnapshot
            {
                Gps = gpsReliability,
                Vps = vpsReliability,
                Active = active,
                Band = GetBand(active, config),
                GpsStableSeconds = StableSeconds(_gpsStableSince, now),
                VpsStableSeconds = StableSeconds(_vpsStableSince, now),
                VpsPositionDeltaMeters = vpsPositionDelta,
                VpsHeadingDeltaDegrees = vpsHeadingDelta,
                GpsReason = BuildGpsReason(gps, gpsReliability, config),
                VpsReason = BuildVpsReason(vps, vpsReliability, config),
            };
        }

        public void Reset()
        {
            _hasPreviousGps = false;
            _hasPreviousVps = false;
            _gpsStableSince = -1f;
            _vpsStableSince = -1f;
        }

        public static HarmonyReliabilityBand GetBand(float reliability, HarmonyConfig config)
        {
            if (reliability >= config.highReliabilityThreshold)
                return HarmonyReliabilityBand.High;
            if (reliability >= config.mediumReliabilityThreshold)
                return HarmonyReliabilityBand.Medium;
            return HarmonyReliabilityBand.Low;
        }

        private float EvaluateGpsMotion(HarmonyGpsSample sample, HarmonyConfig config, float now)
        {
            if (!sample.IsValid || sample.JumpRejected)
            {
                _gpsStableSince = -1f;
                CaptureGps(sample);
                return 0f;
            }

            float score = 1f;
            if (_hasPreviousGps &&
                sample.Timestamp > _previousGps.Timestamp + 0.0001d)
            {
                float dt = Mathf.Max(0.01f, (float)(sample.Timestamp - _previousGps.Timestamp));
                float speed = HorizontalDistance(
                    sample.CampusPosition, _previousGps.CampusPosition) / dt;
                score = DescendingScore(
                    speed,
                    config.gpsMaxPlausibleSpeedMetersPerSecond * 0.5f,
                    config.gpsMaxPlausibleSpeedMetersPerSecond);
            }

            if (score >= 0.75f)
            {
                if (_gpsStableSince < 0f) _gpsStableSince = now;
            }
            else
            {
                _gpsStableSince = -1f;
            }

            CaptureGps(sample);
            return score;
        }

        private float EvaluateVpsMotion(
            HarmonyVpsSample sample,
            HarmonyConfig config,
            float now,
            out float positionDelta,
            out float headingDelta)
        {
            positionDelta = 0f;
            headingDelta = 0f;
            if (!sample.IsValid)
            {
                _vpsStableSince = -1f;
                CaptureVps(sample);
                return 0f;
            }

            float positionScore = 1f;
            float headingScore = 1f;
            if (_hasPreviousVps &&
                sample.Timestamp > _previousVps.Timestamp + 0.0001d)
            {
                positionDelta = HorizontalDistance(
                    sample.CampusPosition, _previousVps.CampusPosition);
                headingDelta = Mathf.Abs(Mathf.DeltaAngle(
                    _previousVps.CampusRotation.eulerAngles.y,
                    sample.CampusRotation.eulerAngles.y));
                positionScore = DescendingScore(
                    positionDelta,
                    config.vpsStablePositionDeltaMeters,
                    config.vpsRejectedPositionDeltaMeters);
                headingScore = DescendingScore(
                    headingDelta,
                    config.vpsStableHeadingDeltaDegrees,
                    config.vpsRejectedHeadingDeltaDegrees);
            }

            float score = Mathf.Min(positionScore, headingScore);
            if (score >= 0.75f)
            {
                if (_vpsStableSince < 0f) _vpsStableSince = now;
            }
            else
            {
                _vpsStableSince = -1f;
            }

            CaptureVps(sample);
            return score;
        }

        private void CaptureGps(HarmonyGpsSample sample)
        {
            if (!_hasPreviousGps || sample.Timestamp > _previousGps.Timestamp)
            {
                _previousGps = sample;
                _hasPreviousGps = true;
            }
        }

        private void CaptureVps(HarmonyVpsSample sample)
        {
            if (!_hasPreviousVps || sample.Timestamp > _previousVps.Timestamp)
            {
                _previousVps = sample;
                _hasPreviousVps = true;
            }
        }

        private static float StableScore(float since, float requiredSeconds, float now)
        {
            if (since < 0f) return 0f;
            if (requiredSeconds <= 0f) return 1f;
            return Mathf.Clamp01((now - since) / requiredSeconds);
        }

        private static float StableSeconds(float since, float now)
        {
            return since < 0f ? 0f : Mathf.Max(0f, now - since);
        }

        private static float DescendingScore(float value, float good, float bad)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return 0f;
            if (bad <= good) return value <= good ? 1f : 0f;
            return 1f - Mathf.InverseLerp(good, bad, value);
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            return Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
        }

        private static string BuildGpsReason(
            HarmonyGpsSample gps,
            float reliability,
            HarmonyConfig config)
        {
            if (!gps.IsValid) return "GPS pose unavailable/stale";
            if (gps.JumpRejected) return $"GPS jump rejected ({gps.RejectedJumpMeters:0.0}m)";
            if (gps.HorizontalAccuracyMeters > config.gpsRejectedAccuracyMeters)
                return $"GPS accuracy {gps.HorizontalAccuracyMeters:0.0}m";
            return $"GPS reliability {reliability:0.00}";
        }

        private static string BuildVpsReason(
            HarmonyVpsSample vps,
            float reliability,
            HarmonyConfig config)
        {
            if (!vps.IsValid) return "VPS pose unavailable/stale";
            if (!vps.ConfidenceAvailable) return "VPS confidence unavailable from SDK";
            if (vps.Confidence < config.minimumVpsConfidence)
                return $"VPS confidence {vps.Confidence:0.00} < {config.minimumVpsConfidence:0.00}";
            if (!vps.MapIdAvailable) return "VPS map ID unavailable";
            if (!vps.MapMatchesBuilding) return $"VPS map ID mismatch ({vps.MapId})";
            return $"VPS reliability {reliability:0.00}";
        }
    }
}
