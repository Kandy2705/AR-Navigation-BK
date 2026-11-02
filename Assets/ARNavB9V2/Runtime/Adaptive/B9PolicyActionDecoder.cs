using ARNavB9V2.Reliability;
using UnityEngine;

namespace ARNavB9V2.Adaptive
{
    /// <summary>
    /// Maps a raw policy action to bounded gate parameters and smooths them with
    /// the EMA used in training (replay_env_v3._decode_action):
    /// first value = raw, then filtered = (1 - beta) * filtered + beta * raw.
    /// The full action layout is 10 weight logits, 2 thresholds (GPS, VPS) and
    /// 2 dwell times (GPS, VPS); a policy outputs only the groups it controls.
    /// Groups it does not control keep the profile value, as in training.
    /// </summary>
    public sealed class B9PolicyActionDecoder
    {
        private const int WeightCount = 10;
        private readonly B9PolicyModel model;
        private float gpsThreshold;
        private float vpsThreshold;
        private float gpsDwell;
        private float vpsDwell;

        public B9PolicyActionDecoder(B9PolicyModel model)
        {
            this.model = model;
        }

        public bool HasValue { get; private set; }

        public B9HandoverGateParameters Current => new B9HandoverGateParameters(
            gpsThreshold, vpsThreshold, gpsDwell, vpsDwell);

        public static int ExpectedActionSize(string[] groups)
        {
            if (groups == null)
                return 0;
            int size = 0;
            foreach (string group in groups)
                size += group == "w" ? WeightCount : group == "thr" || group == "dwell" ? 2 : 0;
            return size;
        }

        public void Reset()
        {
            HasValue = false;
        }

        public B9HandoverGateParameters Decode(float[] action, B9HandoverGateParameters fallback)
        {
            int index = model.ControlsGroup("w") ? WeightCount : 0;
            float rawGpsThreshold = fallback.GpsThreshold;
            float rawVpsThreshold = fallback.VpsThreshold;
            float rawGpsDwell = fallback.GpsDwellSeconds;
            float rawVpsDwell = fallback.VpsDwellSeconds;
            if (model.ControlsGroup("thr"))
            {
                rawGpsThreshold = Bounded(action[index], model.gpsThresholdRange);
                rawVpsThreshold = Bounded(action[index + 1], model.vpsThresholdRange);
                index += 2;
            }
            if (model.ControlsGroup("dwell"))
            {
                rawGpsDwell = Bounded(action[index], model.gpsDwellRange);
                rawVpsDwell = Bounded(action[index + 1], model.vpsDwellRange);
            }

            if (!HasValue)
            {
                gpsThreshold = rawGpsThreshold;
                vpsThreshold = rawVpsThreshold;
                gpsDwell = rawGpsDwell;
                vpsDwell = rawVpsDwell;
                HasValue = true;
            }
            else
            {
                float beta = model.smoothingBeta;
                gpsThreshold = Smooth(gpsThreshold, rawGpsThreshold, beta);
                vpsThreshold = Smooth(vpsThreshold, rawVpsThreshold, beta);
                gpsDwell = Smooth(gpsDwell, rawGpsDwell, beta);
                vpsDwell = Smooth(vpsDwell, rawVpsDwell, beta);
            }
            return Current;
        }

        private static float Bounded(float u, float[] range)
        {
            float x = Mathf.Clamp(u, -1f, 1f);
            return range[0] + (x + 1f) * 0.5f * (range[1] - range[0]);
        }

        private static float Smooth(float filtered, float raw, float beta)
        {
            return (1f - beta) * filtered + beta * raw;
        }
    }
}
