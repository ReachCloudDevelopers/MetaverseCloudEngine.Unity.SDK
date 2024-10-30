using System;
using System.Collections.Generic;
using CesiumForUnity;
using Google.XR.ARCoreExtensions.GeospatialCreator;
using Unity.Mathematics;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.ARCoreExtensions
{
    /// <summary>
    /// Exposes the methods that are internalized by Google ARCoreExtensions for use at runtime.
    /// </summary>
    public static class MVGeoSpatialUtils
    {
        // Equatorial radius in meters
        private const double Wgs84EllipsoidSemiMajorAxis = 6378137.0;

        // Polar radius in meters
        private const double Wgs84EllipsoidSemiMinorAxis = 6356752.314245;

        public static double3 ECEFToLongitudeLatitudeHeight(double3 ecef)
        {
            double latitude, longitude, altitude;
            ECEFToGeodetic(ecef, out latitude, out longitude, out altitude);
            return new double3(longitude, latitude, altitude);
        }

        public static double4x4 CalculateEnuToEcefTransform((double Latitude, double Longitude, double Altitude) originPoint)
        {
            return math.inverse(CalculateEcefToEnuTransform(originPoint));
        }

        public static double4x4 CalculateEcefToEnuTransform((double Latitude, double Longitude, double Altitude) originPoint)
        {
            // :TODO b/277370107: This could be optimized by only changing the position if the
            // object or origin has moved
            var positionInEcef = GeoCoordinateToECEF(originPoint);

            // Rotate from y up to z up and flip X. References:
            //   https://github.com/CesiumGS/3d-tiles/tree/main/specification#transforms
            //   https://stackoverflow.com/questions/1263072/changing-a-matrix-from-right-handed-to-left-handed-coordinate-system
            //   https://en.wikipedia.org/wiki/Geographic_coordinate_conversion#From_ECEF_to_ENU
            var matrixStack = new MatrixStack();
            matrixStack.PushMatrix();

            double latSin, latCos;
            math.sincos(originPoint.Latitude / 180 * Math.PI, out latSin, out latCos);
            double lngSin, lngCos;
            math.sincos(originPoint.Longitude / 180 * Math.PI, out lngSin, out lngCos);
            var ECEFToENURot = new double4x4(
                -lngSin,
                lngCos,
                0.0,
                0.0,
                -latSin * lngCos,
                -latSin * lngSin,
                latCos,
                0.0,
                latCos * lngCos,
                latCos * lngSin,
                latSin,
                0.0,
                0.0,
                0.0,
                0.0,
                1.0);

            matrixStack.MultMatrix(
                MatrixStack.Translate(
                    new double3(-positionInEcef.x, -positionInEcef.y, -positionInEcef.z)));
            matrixStack.MultMatrix(ECEFToENURot);
            return matrixStack.GetMatrix();
        }
        
        public static double3 GeoCoordinateToECEF((double Latitude, double Longitude, double Altitude) coor)
        {
            var ret = new double3();

            const double a2 = Wgs84EllipsoidSemiMajorAxis * Wgs84EllipsoidSemiMajorAxis;
            const double b2 = Wgs84EllipsoidSemiMinorAxis * Wgs84EllipsoidSemiMinorAxis;
            const double f = 1 - (Wgs84EllipsoidSemiMinorAxis / Wgs84EllipsoidSemiMajorAxis);
            const double e2 = 1 - (b2 / a2);
            const double neg1f2 = (1 - f) * (1 - f);
            const double neg1e2 = 1 - e2;

            var rlong = Math.PI * coor.Longitude / 180.0;
            var rlat = Math.PI * coor.Latitude / 180.0;
            var coslong = Math.Cos(rlong);
            var sinlong = Math.Sin(rlong);
            var coslat = Math.Cos(rlat);
            var sinlat = Math.Sin(rlat);
            var n_2 = a2 / Math.Sqrt((a2 * (coslat * coslat)) + ((b2) * (sinlat * sinlat)));
            var n = Wgs84EllipsoidSemiMajorAxis / Math.Sqrt(1 - (e2 * sinlat * sinlat));
            var x = (n + coor.Altitude) * coslat * coslong;
            var y = (n + coor.Altitude) * coslat * sinlong;
            var z = (neg1f2 * n + coor.Altitude) * sinlat;
            var z_2 = (neg1e2 * n + coor.Altitude) * sinlat;

            // x y z are in meters
            ret.x = x;
            ret.y = y;
            ret.z = z;
            return ret;
        }

        // Conversion between geodetic decimal degrees and earth-centered, earth-fixed (ECEF)
        // coordinates. Ref https://en.wikipedia.org/wiki/Geographic_coordinate_conversion.
        public static void ECEFToGeodetic(
            double3 ecef, out double latitude, out double longitude, out double altitude)
        {
            const double a2 = Wgs84EllipsoidSemiMajorAxis * Wgs84EllipsoidSemiMajorAxis;
            const double b2 = Wgs84EllipsoidSemiMinorAxis * Wgs84EllipsoidSemiMinorAxis;

            var p = Math.Sqrt(ecef.x * ecef.x + ecef.y * ecef.y); // Temporary value
            var q = Math.Atan2((ecef.z * Wgs84EllipsoidSemiMajorAxis), (p * Wgs84EllipsoidSemiMinorAxis)); // Temporary value

            // special case of north/south pole
            const double epsilon = 1e-9;
            double latRad, lonRad;
            if (p < epsilon)
            {
                lonRad = 0.0;
                var zSign = (ecef.z < 0) ? -1 : 1;
                latRad = (Math.PI / 2.0) * zSign;
                altitude = Math.Sqrt(ecef.z * ecef.z) - Wgs84EllipsoidSemiMinorAxis;
            }
            else
            {
                lonRad = Math.Atan2(ecef.y, ecef.x);
                latRad = Math.Atan2(
                    (ecef.z + ((a2 - b2) / Wgs84EllipsoidSemiMinorAxis) * Math.Pow(Math.Sin(q), 3.0)),
                    (p - ((a2 - b2) / Wgs84EllipsoidSemiMajorAxis) * Math.Pow(Math.Cos(q), 3.0)));
                var n = Wgs84EllipsoidSemiMajorAxis /
                        Math.Sqrt(1.0 - (1.0 - b2 / a2) * Math.Sin(latRad) * Math.Sin(latRad));

                altitude = Math.Sqrt(ecef.x * ecef.x + ecef.y * ecef.y) / Math.Cos(latRad) - n;
            }

            latitude = latRad * 180.0 / Math.PI;
            longitude = lonRad * 180.0 / Math.PI;
        }
        
        public static double3 GetGeospatialLatitudeLongitudeHeight(
            this ARGeospatialCreatorOrigin originPoint, Transform transform)
        {
            var enuToECef = CalculateEnuToEcefTransform((originPoint.Latitude, originPoint.Longitude, originPoint.Altitude));
            var eun = new double3(transform.position.x, transform.position.y, transform.position.z);
            var enu = new double3(eun.x, eun.z, eun.y);
            var eCef = MatrixStack.MultPoint(enuToECef, enu);
            var llh = ECEFToLongitudeLatitudeHeight(eCef);
            var lon = llh.x;
            var lat = llh.y;
            var height = llh.z;
            return new double3(lat, lon, height);
        }
        
#if MV_CESIUM
        public static double3 GetGeospatialLatitudeLongitudeHeight(
            this CesiumGeoreference originPoint, Transform transform)
        {
            var enuToECef = CalculateEnuToEcefTransform((originPoint.latitude, originPoint.longitude, originPoint.height));
            var eun = new double3(transform.position.x, transform.position.y, transform.position.z);
            var enu = new double3(eun.x, eun.z, eun.y);
            var eCef = MatrixStack.MultPoint(enuToECef, enu);
            var llh = ECEFToLongitudeLatitudeHeight(eCef);
            var lon = llh.x;
            var lat = llh.y;
            var height = llh.z;
            return new double3(lat, lon, height);
        }

#endif

        private class MatrixStack
        {
            private readonly List<double4x4> _stack = new();

            public MatrixStack()
            {
                _stack.Add(double4x4.identity);
            }
            
            public static Quaternion GetRotation(double4x4 m)
            {
                Vector3 forward;
                forward.x = (float)m.c2.x;
                forward.y = (float)m.c2.y;
                forward.z = (float)m.c2.z;

                Vector3 upwards;
                upwards.x = (float)m.c1.x;
                upwards.y = (float)m.c1.y;
                upwards.z = (float)m.c1.z;

                return Quaternion.LookRotation(forward, upwards);
            }

            public static double3 MultPoint(double4x4 mat, double3 a)
            {
                double4 v = new double4(a[0], a[1], a[2], 1.0);
                double4 ret = math.mul(mat, v);
                return new double3(ret.x, ret.y, ret.z);
            }

            public static double4x4 YupToZupTest()
            {
                return new double4x4(
                    -1.0,
                    0.0,
                    0.0,
                    0.0,
                    0.0,
                    0.0,
                    1.0,
                    0.0,
                    0.0,
                    -1.0,
                    0.0,
                    0.0,
                    0.0,
                    0.0,
                    0.0,
                    1.0);
            }

            public static double4x4 YupToZup()
            {
                return new double4x4(
                    1.0,
                    0.0,
                    0.0,
                    0.0,
                    0.0,
                    0.0,
                    -1.0,
                    0.0,
                    0.0,
                    1.0,
                    0.0,
                    0.0,
                    0.0,
                    0.0,
                    0.0,
                    1.0);
            }

            public static double4x4 RotateX(double angle)
            {
                // {{1, 0, 0}, {0, c_0, -s_0}, {0, s_0, c_0}}
                double s,
                    c;
                math.sincos(angle, out s, out c);
                return new double4x4(
                    1.0,
                    0.0,
                    0.0,
                    0.0,
                    0.0,
                    c,
                    -s,
                    0.0,
                    0.0,
                    s,
                    c,
                    0.0,
                    0.0,
                    0.0,
                    0.0,
                    1.0);
            }

            /// <summary>Returns a double4x4 matrix that rotates around the y-axis by a given number of radians.</summary>
            /// <param name="angle">The clockwise rotation angle when looking along the y-axis towards the origin in radians.</param>
            /// <returns>The double4x4 rotation matrix that rotates around the y-axis.</returns>
            public static double4x4 RotateY(double angle)
            {
                // {{c_1, 0, s_1}, {0, 1, 0}, {-s_1, 0, c_1}}
                double s,
                    c;
                math.sincos(angle, out s, out c);
                return new double4x4(
                    c,
                    0.0,
                    s,
                    0.0,
                    0.0,
                    1.0,
                    0.0,
                    0.0,
                    -s,
                    0.0,
                    c,
                    0.0,
                    0.0,
                    0.0,
                    0.0,
                    1.0);
            }

            /// <summary>Returns a double4x4 matrix that rotates around the z-axis by a given number of radians.</summary>
            /// <param name="angle">The clockwise rotation angle when looking along the z-axis towards the origin in radians.</param>
            /// <returns>The double4x4 rotation matrix that rotates around the z-axis.</returns>
            public static double4x4 RotateZ(double angle)
            {
                // {{c_2, -s_2, 0}, {s_2, c_2, 0}, {0, 0, 1}}
                double s,
                    c;
                math.sincos(angle, out s, out c);
                return new double4x4(
                    c,
                    -s,
                    0.0,
                    0.0,
                    s,
                    c,
                    0.0,
                    0.0,
                    0.0,
                    0.0,
                    1.0,
                    0.0,
                    0.0,
                    0.0,
                    0.0,
                    1.0);
            }

            /// <summary>Returns a double4x4 scale matrix given 3 axis scales.</summary>
            /// <param name="s">The uniform scaling factor.</param>
            /// <returns>The double4x4 matrix that represents a uniform scale.</returns>
            public static double4x4 Scale(double3 s)
            {
                return new double4x4(
                    s.x,
                    0.0,
                    0.0,
                    0.0,
                    0.0,
                    s.y,
                    0.0,
                    0.0,
                    0.0,
                    0.0,
                    s.z,
                    0.0,
                    0.0,
                    0.0,
                    0.0,
                    1.0);
            }

            /// <summary>Returns a double4x4 translation matrix given a double3 translation vector.</summary>
            /// <param name="vector">The translation vector.</param>
            /// <returns>The double4x4 translation matrix.</returns>
            public static double4x4 Translate(double3 vector)
            {
                return new double4x4(
                    new double4(1.0, 0.0, 0.0, 0.0),
                    new double4(0.0, 1.0, 0.0, 0.0),
                    new double4(0.0, 0.0, 1.0, 0.0),
                    new double4(vector.x, vector.y, vector.z, 1.0));
            }

            public void PushMatrix()
            {
                _stack.Add(_stack[_stack.Count - 1]);
            }

            public void PushIdentityMatrix()
            {
                _stack.Add(double4x4.identity);
            }

            public void PopMatrix()
            {
                Debug.Assert(_stack.Count >= 2);
                _stack.RemoveAt(_stack.Count - 1);
            }

            public double4x4 GetMatrix()
            {
                Debug.Assert(_stack.Count >= 1);
                return _stack[_stack.Count - 1];
            }

            public Quaternion GetRotation()
            {
                double4x4 m = _stack[_stack.Count - 1];

                Vector3 forward;
                forward.x = (float)m.c2.x;
                forward.y = (float)m.c2.y;
                forward.z = (float)m.c2.z;

                Vector3 upwards;
                upwards.x = (float)m.c1.x;
                upwards.y = (float)m.c1.y;
                upwards.z = (float)m.c1.z;

                return Quaternion.LookRotation(forward, upwards);
            }

            public void Transpose()
            {
                Debug.Assert(_stack.Count >= 1);
                _stack[_stack.Count - 1] = math.transpose(_stack[_stack.Count - 1]);
            }

            // Pre multiply
            public void PreMultMatrix(double4x4 m)
            {
                Debug.Assert(_stack.Count >= 1);
                _stack[_stack.Count - 1] = math.mul(_stack[_stack.Count - 1], m);
            }

            // Post multiply
            public void MultMatrix(double4x4 m)
            {
                Debug.Assert(_stack.Count >= 1);
                _stack[_stack.Count - 1] = math.mul(m, _stack[_stack.Count - 1]);
            }

            public void MultMatrix(List<double> a)
            {
                Debug.Assert(_stack.Count >= 1);
                Debug.Assert(a.Count >= 16);

                // load column-major order
                double4x4 m = new double4x4(
                    a[0],
                    a[4],
                    a[8],
                    a[12],
                    a[1],
                    a[5],
                    a[9],
                    a[13],
                    a[2],
                    a[6],
                    a[10],
                    a[14],
                    a[3],
                    a[7],
                    a[11],
                    a[15]);
                _stack[_stack.Count - 1] = math.mul(_stack[_stack.Count - 1], m);
            }

            public double4 MultPoints(List<double> a)
            {
                Debug.Assert(_stack.Count >= 1);
                Debug.Assert(a.Count >= 4);
                double4 v = new double4(a[0], a[1], a[2], 1.0);
                return math.mul(_stack[_stack.Count - 1], v);
            }

            public double3 MultPoint(double3 a)
            {
                Debug.Assert(_stack.Count >= 1);
                double4 v = new double4(a[0], a[1], a[2], 1.0);
                double4 ret = math.mul(_stack[_stack.Count - 1], v);
                return new double3(ret.x, ret.y, ret.z);
            }

            public double4 MultPoint(double4 v)
            {
                Debug.Assert(_stack.Count >= 1);
                return math.mul(_stack[_stack.Count - 1], v);
            }
        }
    }
}