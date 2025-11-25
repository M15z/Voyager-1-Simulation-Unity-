using UnityEngine;
using VoyagerSim.Core;
using VoyagerSim.Utils;

namespace VoyagerSim.Vehicles
{
    /// <summary>
    /// Controls the Voyager spacecraft.
    /// Handles initialization with user-defined velocity and provides trajectory visualization.
    /// </summary>
    public class VoyagerController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The Body component on this GameObject")]
        public Body body;

        [Tooltip("Reference to Earth for launch position")]
        public Body earth;

        [Header("Launch Parameters")]
        [Tooltip("Initial speed in km/s")]
        [Range(5f, 50f)]
        public float initialSpeedKmS = 17.0f;

        [Tooltip("Launch direction angle in degrees (0 = along Earth's velocity)")]
        [Range(-180f, 180f)]
        public float launchAngleDeg = 0f;

        [Tooltip("Distance from Earth at launch (Unity units)")]
        public double launchDistanceFromEarth = 5.0;

        [Header("Encounter Detection")]
        private bool hasEncounteredJupiter = false;
        private bool hasEncounteredSaturn = false;
        private double saturnEncounterDistance = 5.0; // Unity units (~5 Gm = close flyby)

        [Header("Display Info")]
        [SerializeField]
        private double currentSpeedKmS;

        [SerializeField]
        private double distanceFromSunMKm;

        [SerializeField]
        private double distanceFromEarthMKm;

        private TrailRenderer trail;
        public bool isLaunched = false;

        void Awake()
        {
            body = GetComponent<Body>();
            trail = GetComponent<TrailRenderer>();

            if (body == null)
            {
                Debug.LogError("VoyagerController: No Body component found!");
            }
        }

        /// <summary>
        /// Initialize Voyager at launch position with specified velocity
        /// Called by SimulationManager
        /// </summary>
        public void LaunchFromEarth(Body earthBody, double speedKmS, double angleDeg)
        {
            if (earthBody == null)
            {
                Debug.LogError("VoyagerController: Earth reference is null!");
                return;
            }

            earth = earthBody;
            initialSpeedKmS = (float)speedKmS;
            launchAngleDeg = (float)angleDeg;

            Vector3d earthPos = earth.Position;
            Vector3d earthVel = earth.Velocity;
            Vector3d earthVelDir = earthVel.normalized;

            // Launch position
            Vector3d launchPosition = earthPos + earthVelDir * launchDistanceFromEarth;

            // ========== CORRECT CALCULATION ==========
            // Find Jupiter
            SolarSystem solarSystem = FindObjectOfType<SolarSystem>();
            Body jupiter = solarSystem.planets.Find(p => p.bodyName == "Jupiter");

            Vector3d launchVelocity;

            if (jupiter != null)
            {
                // Calculate required heliocentric velocity to reach Jupiter
                double r1 = earthPos.magnitude; // Earth's distance from Sun
               //  double r2 = jupiter.Position.magnitude; // Jupiter's distance from Sun
                double r2 = UnitScale.MetersToUnity(jupiter.semiMajorAxis); // CORRECT!
                // Sun's GM (scaled)
                double sunMass = 1.989e30;
                double GM = UnitScale.GetScaledGM(sunMass) * UnitScale.SIM_TIME_SCALE * UnitScale.SIM_TIME_SCALE;
               // double GM = UnitScale.GetScaledGM(sunMass); // CORRECT!
                // Earth's current orbital speed
                double v_earth = earthVel.magnitude;

                // To reach Jupiter's orbit, we need aphelion at r2
                // Using vis-viva: v² = GM(2/r - 1/a)
                // At perihelion (Earth's orbit): a = (r1 + r2) / 2
                double a_transfer = (r1 + r2) / 2.0;
                double v_perihelion = System.Math.Sqrt(GM * (2.0 / r1 - 1.0 / a_transfer));

                // This is the ABSOLUTE speed needed
                // Add 20% for faster intercept
                double targetSpeed = v_perihelion * 1.097; // Just 10% faster for intercept timing

                // Launch velocity in Earth's direction
                launchVelocity = earthVelDir * targetSpeed;

                double deltaV = targetSpeed - v_earth;

                Debug.Log($"=== LAUNCH CALCULATION ===");
                Debug.Log($"Earth orbital speed: {UnitScale.UnityPerSimSecToKmPerSec(v_earth):F2} km/s");
                Debug.Log($"Required speed at Earth orbit: {UnitScale.UnityPerSimSecToKmPerSec(v_perihelion):F2} km/s");
                Debug.Log($"Actual launch speed: {UnitScale.UnityPerSimSecToKmPerSec(targetSpeed):F2} km/s");
                Debug.Log($"Delta-V from Earth: {UnitScale.UnityPerSimSecToKmPerSec(deltaV):F2} km/s");
                Debug.Log($"Transfer orbit semi-major axis: {a_transfer:F2} Unity");
            }
            else
            {
                Debug.LogError("Jupiter not found!");
                double launchSpeedUnity = UnitScale.KmPerSecToUnityPerSimSec(speedKmS);
                launchVelocity = earthVel + earthVelDir * launchSpeedUnity;
            }

            // Initialize Body
            body.Initialize(launchPosition, launchVelocity, body.mass, "Voyager");

            // Configure trail
            if (trail != null)
            {
                trail.Clear();
                trail.emitting = true;
            }

            isLaunched = true;

            double actualSpeedKmS = UnitScale.UnityPerSimSecToKmPerSec(launchVelocity.magnitude);
            Debug.Log($"Voyager launched with absolute heliocentric velocity: {actualSpeedKmS:F2} km/s");
        }

        /// <summary>
        /// Update display information
        /// </summary>
        void Update()
        {
            if (!isLaunched || body == null) return;
            
            currentSpeedKmS = body.GetSpeedKmPerSec();
            distanceFromSunMKm = body.Position.magnitude * 1000.0 / UnitScale.DISTANCE_SCALE / 1e9;
            
            if (earth != null)
            {
                distanceFromEarthMKm = body.GetDistanceFromBody(earth);
            }
            
            // Check for Saturn encounter
            CheckSaturnEncounter();
        }

        void CheckSaturnEncounter()
        {
            if (hasEncounteredSaturn) return;
            
            SolarSystem solarSystem = FindObjectOfType<SolarSystem>();
            if (solarSystem == null) return;
            
            Body saturn = solarSystem.planets.Find(p => p.bodyName == "Saturn");
            if (saturn == null) return;
            
            // Check distance to Saturn
            Vector3d toSaturn = saturn.Position - body.Position;
            double distanceToSaturn = toSaturn.magnitude;
            
            // If within encounter distance and approaching
            if (distanceToSaturn < saturnEncounterDistance && !hasEncounteredSaturn)
            {
                // Check if we're at closest approach (velocity perpendicular to position vector)
                Vector3d relativeVel = body.Velocity - saturn.Velocity;
                double approachRate = Vector3d.Dot(relativeVel, toSaturn.normalized);
                
                // If we're past closest approach (now moving away)
                if (approachRate > 0)
                {
                    ApplySaturnDeflection(saturn);
                    hasEncounteredSaturn = true;
                }
            }
        }

        void ApplySaturnDeflection(Body saturn)
        {
            Debug.Log("=== SATURN ENCOUNTER ===");
            Debug.Log($"Distance: {(saturn.Position - body.Position).magnitude:F2} Unity units");
            Debug.Log($"Speed before: {UnitScale.UnityPerSimSecToKmPerSec(body.Velocity.magnitude):F2} km/s");
            
            // Get current velocity
            Vector3d currentVel = body.Velocity;
            double currentSpeed = currentVel.magnitude;
            
            // Direction relative to Saturn
            Vector3d toSaturn = (saturn.Position - body.Position).normalized;
            
            // Current direction
            Vector3d currentDir = currentVel.normalized;
            
            // Calculate deflection angle (35° upward)
            double deflectionAngleDeg = 35.0;
            double deflectionAngleRad = deflectionAngleDeg * Mathf.Deg2Rad;
            
            // Create upward deflection (+Y direction)
            // We want to rotate the velocity vector 35° toward +Y
            
            // Get the "forward" component (in XZ plane)
            Vector3d forwardXZ = new Vector3d(currentVel.x, 0, currentVel.z);
            double speedXZ = forwardXZ.magnitude;
            
            // After deflection:
            // XZ component = cos(35°) * original speed
            // Y component = sin(35°) * original speed
            double newSpeedXZ = currentSpeed * System.Math.Cos(deflectionAngleRad);
            double newSpeedY = currentSpeed * System.Math.Sin(deflectionAngleRad);
            
            // Keep the same XZ direction, but reduce magnitude and add Y
            Vector3d newVelXZ = forwardXZ.normalized * newSpeedXZ;
            Vector3d newVel = new Vector3d(newVelXZ.x, newSpeedY, newVelXZ.z);
            
            body.Velocity = newVel;
            
            Debug.Log($"Speed after: {UnitScale.UnityPerSimSecToKmPerSec(body.Velocity.magnitude):F2} km/s");
            Debug.Log($"Deflection: {deflectionAngleDeg}° above ecliptic");
            Debug.Log($"Y-velocity component: {UnitScale.UnityPerSimSecToKmPerSec(body.Velocity.y):F2} km/s");
        }

        /// <summary>
        /// Reset Voyager for relaunch
        /// </summary>
        public void Reset()
        {
            isLaunched = false;

            if (trail != null)
            {
                trail.Clear();
                trail.emitting = false;
            }
        }

        /// <summary>
        /// Get current speed for UI display
        /// </summary>
        public double GetSpeedKmS()
        {
            return currentSpeedKmS;
        }

        /// <summary>
        /// Get distance from Sun for UI display
        /// </summary>
        public double GetDistanceFromSunMKm()
        {
            return distanceFromSunMKm;
        }
    }
}