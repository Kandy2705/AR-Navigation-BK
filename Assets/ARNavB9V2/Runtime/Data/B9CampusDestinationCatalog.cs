using System;
using System.Collections.Generic;
using UnityEngine;

namespace ARNavB9V2.Data
{
    [CreateAssetMenu(
        fileName = "B9CampusDestinationCatalog",
        menuName = "AR Navigation V2/Campus Destination Catalog")]
    public sealed class B9CampusDestinationCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Destination
        {
            [SerializeField] private string id;
            [SerializeField] private string displayName;
            [SerializeField] private double latitude;
            [SerializeField] private double longitude;
            [SerializeField] private bool indoorNavigationAvailable;
            [SerializeField, Min(2f)] private float arrivalRadiusMeters = 12f;

            public string Id => id;
            public string DisplayName => displayName;
            public double Latitude => latitude;
            public double Longitude => longitude;
            public bool IndoorNavigationAvailable => indoorNavigationAvailable;
            public float ArrivalRadiusMeters => arrivalRadiusMeters;

            public Destination(
                string destinationId,
                string label,
                double destinationLatitude,
                double destinationLongitude,
                bool hasIndoorNavigation,
                float arrivalRadius)
            {
                id = destinationId;
                displayName = label;
                latitude = destinationLatitude;
                longitude = destinationLongitude;
                indoorNavigationAvailable = hasIndoorNavigation;
                arrivalRadiusMeters = Mathf.Max(2f, arrivalRadius);
            }
        }

        [SerializeField] private List<Destination> destinations = new List<Destination>();

        public IReadOnlyList<Destination> Destinations => destinations;

        public void SetDestinations(IReadOnlyList<Destination> values)
        {
            destinations = values != null
                ? new List<Destination>(values)
                : new List<Destination>();
        }

        public bool TryGet(string id, out Destination destination)
        {
            destination = null;
            if (string.IsNullOrWhiteSpace(id))
                return false;

            for (int i = 0; i < destinations.Count; i++)
            {
                Destination candidate = destinations[i];
                if (candidate != null && string.Equals(
                        candidate.Id,
                        id.Trim(),
                        StringComparison.OrdinalIgnoreCase))
                {
                    destination = candidate;
                    return true;
                }
            }
            return false;
        }
    }
}
