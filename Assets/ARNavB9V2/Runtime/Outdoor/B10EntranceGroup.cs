using System.Collections.Generic;
using UnityEngine;

namespace ARNavB9V2.Outdoor
{
    /// <summary>Outdoor campus-space anchors for the measured B10 entrances.</summary>
    [DisallowMultipleComponent]
    public sealed class B10EntranceGroup : MonoBehaviour
    {
        [SerializeField] private List<Transform> entrances = new List<Transform>();
        [SerializeField, Min(2f)] private float arrivalRadiusMeters = 6f;

        public IReadOnlyList<Transform> Entrances => entrances;
        public float ArrivalRadiusMeters => arrivalRadiusMeters;

        public void Configure(IReadOnlyList<Transform> entranceAnchors, float arrivalRadius = 6f)
        {
            entrances = entranceAnchors != null
                ? new List<Transform>(entranceAnchors)
                : new List<Transform>();
            arrivalRadiusMeters = Mathf.Max(2f, arrivalRadius);
        }

        public bool ValidateConfiguration(out string reason)
        {
            if (entrances == null || entrances.Count == 0)
            {
                reason = "B10 has no entrance anchors";
                return false;
            }

            var unique = new HashSet<Transform>();
            for (int i = 0; i < entrances.Count; i++)
            {
                if (entrances[i] == null)
                {
                    reason = $"B10 entrance {i + 1} is missing";
                    return false;
                }
                if (!unique.Add(entrances[i]))
                {
                    reason = $"Duplicate B10 entrance: {entrances[i].name}";
                    return false;
                }
            }

            reason = string.Empty;
            return true;
        }

        private void OnDrawGizmos()
        {
            if (entrances == null)
                return;

            Gizmos.color = new Color(1f, 0.72f, 0.05f, 0.95f);
            for (int i = 0; i < entrances.Count; i++)
            {
                Transform entrance = entrances[i];
                if (entrance == null)
                    continue;
                Gizmos.DrawWireSphere(entrance.position, 0.8f);
                Gizmos.DrawRay(entrance.position, entrance.forward * 1.5f);
            }
        }
    }
}
