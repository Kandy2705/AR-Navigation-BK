using UnityEngine;

namespace ARNavB9V2.Outdoor
{
    [DisallowMultipleComponent]
    public sealed class B9OutdoorDestinationAnchor : MonoBehaviour
    {
        [SerializeField] private string destinationId;
        [SerializeField] private string displayName;
        [SerializeField] private double latitude;
        [SerializeField] private double longitude;
        [SerializeField] private float arrivalRadiusMeters = 12f;

        public string DestinationId => destinationId;
        public string DisplayName => displayName;
        public double Latitude => latitude;
        public double Longitude => longitude;
        public float ArrivalRadiusMeters => arrivalRadiusMeters;

        public void Configure(
            string id,
            string label,
            double destinationLatitude,
            double destinationLongitude,
            float arrivalRadius)
        {
            destinationId = id;
            displayName = label;
            latitude = destinationLatitude;
            longitude = destinationLongitude;
            arrivalRadiusMeters = Mathf.Max(2f, arrivalRadius);
        }
    }
}
