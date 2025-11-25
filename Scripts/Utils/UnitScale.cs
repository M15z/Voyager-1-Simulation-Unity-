using UnityEngine;

namespace VoyagerSim.Utils
{
    /// <summary>
    /// Handles all unit conversions between real astronomical units and Unity's coordinate space.
    /// Critical for avoiding floating-point precision issues at planetary distances.
    /// </summary>
    public static class UnitScale
    {
        // === SCALE FACTORS ===
        
        /// <summary>
        /// Distance scale: 1 Unity unit = 1 billion meters (1 Gm)
        /// Example: Earth's orbit (150 million km) = 150 Unity units
        /// </summary>
        public const double DISTANCE_SCALE = 1e-9; // Unity units per meter
        
        /// <summary>
        /// Simulation time scaling factor
        /// 1 simulation second = SIM_TIME_SCALE real seconds
        /// This makes orbital motion visible without extreme time warp
        /// </summary>
        public const double SIM_TIME_SCALE = 386400.0; // 1 sim-second = 1 day
        
        /// <summary>
        /// Gravitational constant in SI units (m³ kg⁻¹ s⁻²)
        /// </summary>
        public const double G = 6.67430e-11;
        
        // === CONVERSION METHODS ===
        
        /// <summary>
        /// Convert real-world meters to Unity units
        /// </summary>
        public static double MetersToUnity(double meters)
        {
            return meters * DISTANCE_SCALE;
        }
        
        /// <summary>
        /// Convert Unity units to real-world meters
        /// </summary>
        public static double UnityToMeters(double unityUnits)
        {
            return unityUnits / DISTANCE_SCALE;
        }
        
        /// <summary>
        /// Convert Vector3 position from meters to Unity units
        /// </summary>
        public static Vector3 MetersToUnity(Vector3d metersPos)
        {
            return new Vector3(
                (float)(metersPos.x * DISTANCE_SCALE),
                (float)(metersPos.y * DISTANCE_SCALE),
                (float)(metersPos.z * DISTANCE_SCALE)
            );
        }
        
        /// <summary>
        /// Convert km/s to Unity units per sim-second (for velocities)
        /// </summary>
        public static double KmPerSecToUnityPerSimSec(double kmPerSec)
        {
            // km/s → m/s → Unity/s → Unity/sim-second
            return kmPerSec * 1000.0 * DISTANCE_SCALE * SIM_TIME_SCALE;
        }
        
        /// <summary>
        /// Convert Unity units per sim-second to km/s (for display)
        /// </summary>
        public static double UnityPerSimSecToKmPerSec(double unityPerSimSec)
        {
            return unityPerSimSec / (1000.0 * DISTANCE_SCALE * SIM_TIME_SCALE);
        }
        
        /// <summary>
        /// Get scaled gravitational parameter (G * M) in Unity units³/sim-second²
        /// Accounts for both distance and time scaling
        /// </summary>
        public static double GetScaledGM(double massKg)
        {
            // G * M in SI: m³/s²
            // Convert to Unity³/s² by multiplying by DISTANCE_SCALE³
            return G * massKg * DISTANCE_SCALE * DISTANCE_SCALE * DISTANCE_SCALE;
        }
    }
    
    /// <summary>
    /// Double-precision Vector3 for astronomical calculations
    /// Unity's Vector3 uses float (7 decimal digits precision)
    /// We need double (15-17 digits) for planet positions
    /// </summary>
    [System.Serializable]
    public struct Vector3d
    {
        public double x, y, z;
        
        public Vector3d(double x, double y, double z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }
        
        public static Vector3d zero => new Vector3d(0, 0, 0);
        
        public double magnitude => System.Math.Sqrt(x * x + y * y + z * z);
        
        public double sqrMagnitude => x * x + y * y + z * z;
        
        public Vector3d normalized
        {
            get
            {
                double mag = magnitude;
                if (mag > 1e-10)
                    return this / mag;
                return zero;
            }
        }
        
        // Operators
        public static Vector3d operator +(Vector3d a, Vector3d b)
        {
            return new Vector3d(a.x + b.x, a.y + b.y, a.z + b.z);
        }
        
        public static Vector3d operator -(Vector3d a, Vector3d b)
        {
            return new Vector3d(a.x - b.x, a.y - b.y, a.z - b.z);
        }
        
        public static Vector3d operator *(Vector3d a, double scalar)
        {
            return new Vector3d(a.x * scalar, a.y * scalar, a.z * scalar);
        }
        
        public static Vector3d operator *(double scalar, Vector3d a)
        {
            return new Vector3d(a.x * scalar, a.y * scalar, a.z * scalar);
        }
        
        public static Vector3d operator /(Vector3d a, double scalar)
        {
            return new Vector3d(a.x / scalar, a.y / scalar, a.z / scalar);
        }
        /// <summary>
        /// Dot product of two 3D vectors
        /// </summary>
        public static double Dot(Vector3d a, Vector3d b)
        {
            return a.x * b.x + a.y * b.y + a.z * b.z;
        }
        // Convert to Unity Vector3 (loses precision, only for rendering)
        public Vector3 ToVector3()
        {
            return new Vector3((float)x, (float)y, (float)z);
        }
        
        // Create from Unity Vector3
        public static Vector3d FromVector3(Vector3 v)
        {
            return new Vector3d(v.x, v.y, v.z);
        }
        
        public override string ToString()
        {
            return $"({x:F2}, {y:F2}, {z:F2})";
        }
    }
}