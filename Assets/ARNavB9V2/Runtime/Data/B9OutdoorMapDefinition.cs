using System;
using UnityEngine;

namespace ARNavB9V2.Data
{
    /// <summary>
    /// Calibrated coordinate definition for the SchoolGround outdoor map.
    /// World X is East, world Z is North, and one Unity unit is one metre.
    /// </summary>
    [CreateAssetMenu(fileName = "B9OutdoorMapDefinition", menuName = "AR Navigation V2/B9 Outdoor Map Definition")]
    public sealed class B9OutdoorMapDefinition : ScriptableObject
    {
        private const double Wgs84SemiMajorAxisMeters = 6378137.0d;
        private const double Wgs84Flattening = 1.0d / 298.257223563d;
        private const double Wgs84EccentricitySquared =
            Wgs84Flattening * (2.0d - Wgs84Flattening);

        [SerializeField] private string mapId = "SchoolGround";
        [SerializeField] private double originLatitude;
        [SerializeField] private double originLongitude;
        [SerializeField] private double entranceLatitude;
        [SerializeField] private double entranceLongitude;
        [SerializeField] private Vector3 entranceCampusPosition;
        [SerializeField] private Vector3 schoolGroundBoundsCenter;
        [SerializeField] private Vector3 schoolGroundBoundsSize;
        [SerializeField] private Vector3 editorMockStartCampusPosition;
        [Min(1f)] [SerializeField] private float arrivalRadiusMeters = 15f;

        public string MapId => mapId;
        public double OriginLatitude => originLatitude;
        public double OriginLongitude => originLongitude;
        public double EntranceLatitude => entranceLatitude;
        public double EntranceLongitude => entranceLongitude;
        public Vector3 EntranceCampusPosition => entranceCampusPosition;
        public Bounds SchoolGroundBounds => new Bounds(schoolGroundBoundsCenter, schoolGroundBoundsSize);
        public Vector3 EditorMockStartCampusPosition => editorMockStartCampusPosition;
        public float ArrivalRadiusMeters => arrivalRadiusMeters;

        public void Configure(
            double mapOriginLatitude,
            double mapOriginLongitude,
            Vector3 entrancePosition,
            Bounds mapBounds,
            Vector3 mockStart,
            float arrivalRadius)
        {
            mapId = "SchoolGround";
            originLatitude = mapOriginLatitude;
            originLongitude = mapOriginLongitude;
            entranceCampusPosition = entrancePosition;
            CampusToGps(entrancePosition, out entranceLatitude, out entranceLongitude);
            schoolGroundBoundsCenter = mapBounds.center;
            schoolGroundBoundsSize = mapBounds.size;
            editorMockStartCampusPosition = mockStart;
            arrivalRadiusMeters = Mathf.Max(1f, arrivalRadius);
        }

        public Vector3 GpsToCampus(double latitude, double longitude)
        {
            GeodeticToEcef(
                originLatitude,
                originLongitude,
                0d,
                out double originX,
                out double originY,
                out double originZ);
            GeodeticToEcef(
                latitude,
                longitude,
                0d,
                out double positionX,
                out double positionY,
                out double positionZ);

            double latitudeRadians = DegreesToRadians(originLatitude);
            double longitudeRadians = DegreesToRadians(originLongitude);
            double sinLatitude = Math.Sin(latitudeRadians);
            double cosLatitude = Math.Cos(latitudeRadians);
            double sinLongitude = Math.Sin(longitudeRadians);
            double cosLongitude = Math.Cos(longitudeRadians);
            double deltaX = positionX - originX;
            double deltaY = positionY - originY;
            double deltaZ = positionZ - originZ;

            // WGS84 ECEF -> local tangent ENU. Unity X is East and Z is North.
            double east = -sinLongitude * deltaX + cosLongitude * deltaY;
            double north = -sinLatitude * cosLongitude * deltaX
                           - sinLatitude * sinLongitude * deltaY
                           + cosLatitude * deltaZ;
            return new Vector3((float)east, 0f, (float)north);
        }

        public void CampusToGps(Vector3 campusPosition, out double latitude, out double longitude)
        {
            GeodeticToEcef(
                originLatitude,
                originLongitude,
                0d,
                out double originX,
                out double originY,
                out double originZ);

            double latitudeRadians = DegreesToRadians(originLatitude);
            double longitudeRadians = DegreesToRadians(originLongitude);
            double sinLatitude = Math.Sin(latitudeRadians);
            double cosLatitude = Math.Cos(latitudeRadians);
            double sinLongitude = Math.Sin(longitudeRadians);
            double cosLongitude = Math.Cos(longitudeRadians);
            double east = campusPosition.x;
            double north = campusPosition.z;

            // Local tangent ENU -> WGS84 ECEF. Outdoor navigation is 2D, so Up is zero.
            double positionX = originX - sinLongitude * east
                               - sinLatitude * cosLongitude * north;
            double positionY = originY + cosLongitude * east
                               - sinLatitude * sinLongitude * north;
            double positionZ = originZ + cosLatitude * north;
            EcefToGeodetic(positionX, positionY, positionZ, out latitude, out longitude);
        }

        public static double DistanceMeters(
            double latitudeA,
            double longitudeA,
            double latitudeB,
            double longitudeB)
        {
            const double earthRadius = 6378137.0;
            double latA = latitudeA * Math.PI / 180.0;
            double latB = latitudeB * Math.PI / 180.0;
            double deltaLat = (latitudeB - latitudeA) * Math.PI / 180.0;
            double deltaLon = (longitudeB - longitudeA) * Math.PI / 180.0;
            double sinLat = Math.Sin(deltaLat * 0.5d);
            double sinLon = Math.Sin(deltaLon * 0.5d);
            double h = sinLat * sinLat + Math.Cos(latA) * Math.Cos(latB) * sinLon * sinLon;
            return earthRadius * 2.0d * Math.Atan2(Math.Sqrt(h), Math.Sqrt(1.0d - h));
        }

        private static void GeodeticToEcef(
            double latitudeDegrees,
            double longitudeDegrees,
            double altitudeMeters,
            out double x,
            out double y,
            out double z)
        {
            double latitudeRadians = DegreesToRadians(latitudeDegrees);
            double longitudeRadians = DegreesToRadians(longitudeDegrees);
            double sinLatitude = Math.Sin(latitudeRadians);
            double cosLatitude = Math.Cos(latitudeRadians);
            double sinLongitude = Math.Sin(longitudeRadians);
            double cosLongitude = Math.Cos(longitudeRadians);
            double primeVerticalRadius = Wgs84SemiMajorAxisMeters
                                         / Math.Sqrt(
                                             1.0d
                                             - Wgs84EccentricitySquared
                                             * sinLatitude
                                             * sinLatitude);

            x = (primeVerticalRadius + altitudeMeters) * cosLatitude * cosLongitude;
            y = (primeVerticalRadius + altitudeMeters) * cosLatitude * sinLongitude;
            z = (primeVerticalRadius * (1.0d - Wgs84EccentricitySquared) + altitudeMeters)
                * sinLatitude;
        }

        private static void EcefToGeodetic(
            double x,
            double y,
            double z,
            out double latitudeDegrees,
            out double longitudeDegrees)
        {
            double semiMinorAxis = Wgs84SemiMajorAxisMeters * (1.0d - Wgs84Flattening);
            double secondEccentricitySquared =
                (Wgs84SemiMajorAxisMeters * Wgs84SemiMajorAxisMeters
                 - semiMinorAxis * semiMinorAxis)
                / (semiMinorAxis * semiMinorAxis);
            double horizontalRadius = Math.Sqrt(x * x + y * y);
            double bowringAngle = Math.Atan2(
                z * Wgs84SemiMajorAxisMeters,
                horizontalRadius * semiMinorAxis);
            double sinAngle = Math.Sin(bowringAngle);
            double cosAngle = Math.Cos(bowringAngle);
            double latitudeRadians = Math.Atan2(
                z + secondEccentricitySquared * semiMinorAxis * sinAngle * sinAngle * sinAngle,
                horizontalRadius
                - Wgs84EccentricitySquared
                * Wgs84SemiMajorAxisMeters
                * cosAngle
                * cosAngle
                * cosAngle);

            latitudeDegrees = RadiansToDegrees(latitudeRadians);
            longitudeDegrees = RadiansToDegrees(Math.Atan2(y, x));
        }

        private static double DegreesToRadians(double degrees)
        {
            return degrees * Math.PI / 180.0d;
        }

        private static double RadiansToDegrees(double radians)
        {
            return radians * 180.0d / Math.PI;
        }
    }
}
