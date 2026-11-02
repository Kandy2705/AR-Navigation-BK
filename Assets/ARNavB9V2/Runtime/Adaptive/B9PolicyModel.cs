using System;
using UnityEngine;

namespace ARNavB9V2.Adaptive
{
    [Serializable]
    public sealed class B9PolicyLayer
    {
        public int inputs;
        public int outputs;
        public string activation;
        public float[] weights;
        public float[] biases;
    }

    /// <summary>
    /// Actor network of the PPO handover policy, exported by
    /// export_policy_unity.py (RL repository). Forward returns the deterministic
    /// action (distribution mean) clipped to the action space, exactly what
    /// stable-baselines3 predict(deterministic=True) returns.
    /// </summary>
    [Serializable]
    public sealed class B9PolicyModel
    {
        public const string SupportedFormat = "arnav-b9-policy/1";

        public string format;
        public string policyVersion;
        public string sourceModel;
        public string sourceSha256;
        public string mode;
        public string[] actionGroups;
        public int observationSize;
        public int actionSize;
        public float actionLow;
        public float actionHigh;
        public float[] gpsThresholdRange;
        public float[] vpsThresholdRange;
        public float[] gpsDwellRange;
        public float[] vpsDwellRange;
        public float smoothingBeta;
        public float inferenceHz;
        public B9PolicyLayer[] layers;

        [NonSerialized] private double[][] activations;

        public static B9PolicyModel FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new InvalidOperationException("Policy JSON is empty");
            B9PolicyModel model = JsonUtility.FromJson<B9PolicyModel>(json);
            model.Validate();
            return model;
        }

        public float[] Forward(float[] observation)
        {
            if (observation == null || observation.Length != observationSize)
            {
                throw new ArgumentException(
                    $"Observation must have {observationSize} values", nameof(observation));
            }

            activations ??= AllocateActivations();
            double[] input = activations[0];
            for (int i = 0; i < observationSize; i++)
                input[i] = observation[i];

            for (int l = 0; l < layers.Length; l++)
            {
                B9PolicyLayer layer = layers[l];
                double[] x = activations[l];
                double[] y = activations[l + 1];
                bool tanh = layer.activation == "tanh";
                for (int o = 0; o < layer.outputs; o++)
                {
                    double sum = layer.biases[o];
                    int row = o * layer.inputs;
                    for (int i = 0; i < layer.inputs; i++)
                        sum += layer.weights[row + i] * x[i];
                    y[o] = tanh ? Math.Tanh(sum) : sum;
                }
            }

            double[] output = activations[layers.Length];
            var action = new float[actionSize];
            for (int i = 0; i < actionSize; i++)
                action[i] = (float)Math.Min(actionHigh, Math.Max(actionLow, output[i]));
            return action;
        }

        public bool ControlsGroup(string group)
        {
            return actionGroups != null && Array.IndexOf(actionGroups, group) >= 0;
        }

        private double[][] AllocateActivations()
        {
            var buffers = new double[layers.Length + 1][];
            buffers[0] = new double[observationSize];
            for (int l = 0; l < layers.Length; l++)
                buffers[l + 1] = new double[layers[l].outputs];
            return buffers;
        }

        private void Validate()
        {
            if (format != SupportedFormat)
                throw new InvalidOperationException($"Unsupported policy format '{format}'");
            if (layers == null || layers.Length == 0)
                throw new InvalidOperationException("Policy has no layers");
            if (!IsRange(gpsThresholdRange) || !IsRange(vpsThresholdRange)
                || !IsRange(gpsDwellRange) || !IsRange(vpsDwellRange))
                throw new InvalidOperationException("Policy parameter ranges are invalid");
            if (smoothingBeta <= 0f || smoothingBeta > 1f)
                throw new InvalidOperationException("Policy smoothing beta must be in (0, 1]");
            if (inferenceHz <= 0f)
                throw new InvalidOperationException("Policy inference rate must be positive");
            if (actionSize != B9PolicyActionDecoder.ExpectedActionSize(actionGroups))
                throw new InvalidOperationException("Policy action size does not match its action groups");

            int expectedInputs = observationSize;
            foreach (B9PolicyLayer layer in layers)
            {
                if (layer.inputs != expectedInputs
                    || layer.weights == null || layer.weights.Length != layer.inputs * layer.outputs
                    || layer.biases == null || layer.biases.Length != layer.outputs)
                    throw new InvalidOperationException("Policy layer shapes are inconsistent");
                if (layer.activation != "tanh" && layer.activation != "linear")
                    throw new InvalidOperationException($"Unsupported activation '{layer.activation}'");
                expectedInputs = layer.outputs;
            }
            if (expectedInputs != actionSize)
                throw new InvalidOperationException("Policy output size does not match its action size");
        }

        private static bool IsRange(float[] range)
        {
            return range != null && range.Length == 2 && range[0] < range[1];
        }
    }
}
