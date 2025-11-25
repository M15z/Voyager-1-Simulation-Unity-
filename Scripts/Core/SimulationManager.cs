using UnityEngine;
using VoyagerSim.Vehicles;
using VoyagerSim.Utils;

namespace VoyagerSim.Core
{
    /// <summary>
    /// Manages the overall simulation state, UI controls, and Voyager spawning.
    /// </summary>
    public class SimulationManager : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("SolarSystem component")]
        public SolarSystem solarSystem;

        [Tooltip("PhysicsIntegrator component")]
        public PhysicsIntegrator physicsIntegrator;

        [Tooltip("Voyager prefab to spawn")]
        public GameObject voyagerPrefab;

        [Tooltip("Camera follow script")]
        public CameraFollow cameraFollow;

        [Header("Launch Settings")]
        [Tooltip("Initial launch speed in km/s")]
        [Range(5f, 50f)]
        public float launchSpeed = 17.0f;

        [Tooltip("Launch angle relative to Earth's velocity (degrees)")]
        [Range(-180f, 180f)]
        public float launchAngle = 0f;

        [Header("Runtime")]
        public VoyagerController voyager;

        void Start()
        {
            // Auto-find references if not set
            if (solarSystem == null)
            {
                solarSystem = GetComponent<SolarSystem>();
            }

            if (physicsIntegrator == null)
            {
                physicsIntegrator = GetComponent<PhysicsIntegrator>();
            }

            // Auto-find camera if not set
            if (cameraFollow == null)
            {
                Camera mainCam = Camera.main;
                if (mainCam != null)
                {
                    cameraFollow = mainCam.GetComponent<CameraFollow>();
                }
            }

            // Validate references
            if (solarSystem == null)
            {
                Debug.LogError("SimulationManager: SolarSystem reference missing!");
            }

            if (physicsIntegrator == null)
            {
                Debug.LogError("SimulationManager: PhysicsIntegrator reference missing!");
            }

            if (voyagerPrefab == null)
            {
                Debug.LogError("SimulationManager: Voyager prefab missing!");
            }

            // Spawn Voyager after a short delay to ensure planets are initialized
            Invoke(nameof(SpawnVoyager), 0.5f);
        }

        /// <summary>
        /// Spawn and launch Voyager from Earth
        /// </summary>
        void SpawnVoyager()
        {
            if (voyagerPrefab == null || solarSystem == null)
            {
                Debug.LogError("SimulationManager: Cannot spawn Voyager - missing references!");
                return;
            }

            // Find Earth
            Body earth = solarSystem.planets.Find(p => p.bodyName == "Earth");
            if (earth == null)
            {
                Debug.LogError("SimulationManager: Earth not found in solar system!");
                return;
            }

            // Instantiate Voyager
            GameObject voyagerObj = Instantiate(voyagerPrefab, solarSystem.transform);
            voyagerObj.name = "Voyager";

            voyager = voyagerObj.GetComponent<VoyagerController>();
            if (voyager == null)
            {
                Debug.LogError("SimulationManager: Voyager prefab missing VoyagerController!");
                Destroy(voyagerObj);
                return;
            }

            // Launch with current settings
            voyager.LaunchFromEarth(earth, launchSpeed, launchAngle);
            Debug.Log("SimulationManager: Voyager spawned and launched!");

            // Set camera to follow Voyager
            if (cameraFollow != null)
            {
                cameraFollow.SetTarget(voyager.transform);
                Debug.Log("Camera now following Voyager!");
            }

            // Try to connect to encounter detector if it exists
            // This will be used in future steps when EncounterDetector is implemented
            var encounterDetector = GetComponent<EncounterDetector>();
            if (encounterDetector != null)
            {
                encounterDetector.voyager = voyager;
                Debug.Log("Encounter detector connected!");
            }
        }

        /// <summary>
        /// Update launch speed (called from UI slider)
        /// </summary>
        public void SetLaunchSpeed(float speedKmS)
        {
            launchSpeed = speedKmS;
            Debug.Log($"Launch speed set to: {speedKmS:F1} km/s");
        }

        /// <summary>
        /// Update launch angle (called from UI slider)
        /// </summary>
        public void SetLaunchAngle(float angleDeg)
        {
            launchAngle = angleDeg;
            Debug.Log($"Launch angle set to: {angleDeg:F1}°");
        }

        /// <summary>
        /// Reset and relaunch Voyager with new settings
        /// </summary>
        public void RelaunchVoyager()
        {
            if (voyager != null)
            {
                Destroy(voyager.gameObject);
                voyager = null;
            }

            SpawnVoyager();
        }

        /// <summary>
        /// Toggle time scale (called from UI button)
        /// </summary>
        public void SetTimeScale(float scale)
        {
            if (physicsIntegrator != null)
            {
                physicsIntegrator.timeScale = scale;
                Debug.Log($"Time scale set to: {scale}x");
            }
        }

        /// <summary>
        /// Toggle pause (called from UI button)
        /// </summary>
        public void TogglePause()
        {
            if (physicsIntegrator != null)
            {
                physicsIntegrator.isPaused = !physicsIntegrator.isPaused;
                Debug.Log($"Simulation {(physicsIntegrator.isPaused ? "PAUSED" : "RESUMED")}");
            }
        }
    }

    /// <summary>
    /// Placeholder for EncounterDetector - will be implemented in future steps
    /// </summary>
    public class EncounterDetector : MonoBehaviour
    {
        public VoyagerController voyager;

        // This will be implemented in Step 4 when we add planetary encounters
        void Start()
        {
            Debug.Log("EncounterDetector: Ready (full implementation coming in Step 4)");
        }
    }
}