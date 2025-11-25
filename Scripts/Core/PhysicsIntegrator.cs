using System.Collections.Generic;
using UnityEngine;
using VoyagerSim.Utils;

namespace VoyagerSim.Core
{
    /// <summary>
    /// Handles gravitational physics calculations and numerical integration.
    /// Uses semi-implicit (symplectic) Euler method with sub-stepping for stability.
    /// </summary>
    public class PhysicsIntegrator : MonoBehaviour
    {
        [Header("Playback Control (Visual Only)")]
        [Tooltip("Visual playback speed multiplier - does NOT affect physics!")]
        [Range(0.1f, 100f)]
        public float playbackSpeed = 1.0f;

        [Header("Simulation Control")]
        [Tooltip("Time warp multiplier (1 = real-time)")]
        public double timeScale = 1.0;
        
        [Tooltip("Pause the simulation")]
        public bool isPaused = false;
        
        [Tooltip("Gravity strength multiplier (for experimentation)")]
        [Range(0.2f, 2.0f)]
        public float gravityScale = 1.0f;
        
        [Header("Integration Settings")]
        [Tooltip("Maximum timestep PER SUB-STEP (seconds)")]
        public double maxDeltaTime = 50.0;  // Increased for large time scales

        [Tooltip("Maximum number of sub-steps per frame")]
        [Range(1, 1000)]
        public int maxSubSteps = 20;  // Reduced since we use larger dt
        
        private const double FIXED_DT = 0.02; // 50 Hz in real seconds
        
        [Header("References")]
        public SolarSystem solarSystem;
        
        [Header("Debug Info")]
        [SerializeField] private double currentDt;
        [SerializeField] private int actualSubSteps;
        
        [Header("Time Warp Mapping")]
        [Tooltip("Physical seconds advanced per real second when Time Scale = 1")]
        public double baseSecondsPerRealSecond = 864; // 864 s ⇒ at 100 => 86,400 s = 1 day per second

        private int frameCount = 0;
        
        void Start()
        {
            if (solarSystem == null)
            {
                solarSystem = GetComponent<SolarSystem>();
                if (solarSystem == null)
                {
                    Debug.LogError("PhysicsIntegrator: No SolarSystem found!");
                    enabled = false;
                    return;
                }
            }
            
            Debug.Log($"PhysicsIntegrator: Initialized with {solarSystem.GetAllBodies().Count} bodies");
            Debug.Log($"PhysicsIntegrator: 1 sim-second = {UnitScale.SIM_TIME_SCALE} real seconds");
            
            List<Body> bodies = solarSystem.GetAllBodies();
            foreach (Body body in bodies)
            {
                Debug.Log($"{body.bodyName} initial velocity = {body.Velocity.magnitude:F6} Unity/sim-sec");
            }
        }
        
        void FixedUpdate()
        {
            Time.timeScale = playbackSpeed;
            if (isPaused || solarSystem == null) return;
            
            // Calculate total simulation time to advance this frame
            // Apply time warp directly to physics timestep
            double simDt = FIXED_DT * timeScale * baseSecondsPerRealSecond;

            
            // Break into sub-steps if needed
            int numSubSteps = Mathf.Max(1, Mathf.CeilToInt((float)(simDt / maxDeltaTime)));
            numSubSteps = Mathf.Min(numSubSteps, maxSubSteps);
            
            double subDt = simDt / numSubSteps;
            actualSubSteps = numSubSteps;
            currentDt = subDt;
            
            // Perform physics steps
            for (int i = 0; i < numSubSteps; i++)
            {
                Step(subDt);
            }
            
            // Update visuals
            UpdateVisuals();
            
            // Debug output
            if (frameCount % 50 == 0)
            {
                Debug.Log($"Frame {frameCount}: TimeScale={timeScale}, SubSteps={numSubSteps}, SubDt={subDt:F6} sim-sec");
            }
            frameCount++;
        }

        void Update()
        {
            // Keyboard controls for playback speed
            if (Input.GetKeyDown(KeyCode.Plus) || Input.GetKeyDown(KeyCode.Equals))
            {
                playbackSpeed = Mathf.Min(playbackSpeed * 2f, 100f);
                Debug.Log($"Playback speed: {playbackSpeed}x");
            }
            
            if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.Underscore))
            {
                playbackSpeed = Mathf.Max(playbackSpeed / 2f, 0.1f);
                Debug.Log($"Playback speed: {playbackSpeed}x");
            }
            
            if (Input.GetKeyDown(KeyCode.Space))
            {
                isPaused = !isPaused;
                Debug.Log($"Simulation {(isPaused ? "PAUSED" : "RESUMED")}");
            }
        }
        

        /// <summary>
        /// Perform one physics integration step
        /// dt is in simulation seconds
        /// RK4 integration step - much more accurate and stable
        /// </summary>
        public void Step(double dt)
        {
            List<Body> bodies = solarSystem.GetAllBodies();
            if (bodies.Count == 0) return;
            
            double scaledDt = dt / UnitScale.SIM_TIME_SCALE;
            
            // Store initial state
            Vector3d[] k1_vel = new Vector3d[bodies.Count];
            Vector3d[] k1_acc = new Vector3d[bodies.Count];
            Vector3d[] initialPos = new Vector3d[bodies.Count];
            Vector3d[] initialVel = new Vector3d[bodies.Count];
            
            for (int i = 0; i < bodies.Count; i++)
            {
                initialPos[i] = bodies[i].Position;
                initialVel[i] = bodies[i].Velocity;
            }
            
            // K1
            CalculateGravitationalForces(bodies);
            for (int i = 0; i < bodies.Count; i++)
            {
                k1_vel[i] = bodies[i].Velocity;
                k1_acc[i] = bodies[i].Acceleration;
            }
            
            // K2 - evaluate at midpoint
            for (int i = 0; i < bodies.Count; i++)
            {
                if (bodies[i] == solarSystem.sun) continue;
                bodies[i].Position = initialPos[i] + k1_vel[i] * (scaledDt * 0.5);
                bodies[i].Velocity = initialVel[i] + k1_acc[i] * (scaledDt * 0.5);
            }
            CalculateGravitationalForces(bodies);
            Vector3d[] k2_vel = new Vector3d[bodies.Count];
            Vector3d[] k2_acc = new Vector3d[bodies.Count];
            for (int i = 0; i < bodies.Count; i++)
            {
                k2_vel[i] = bodies[i].Velocity;
                k2_acc[i] = bodies[i].Acceleration;
            }
            
            // K3 - evaluate at midpoint with K2
            for (int i = 0; i < bodies.Count; i++)
            {
                if (bodies[i] == solarSystem.sun) continue;
                bodies[i].Position = initialPos[i] + k2_vel[i] * (scaledDt * 0.5);
                bodies[i].Velocity = initialVel[i] + k2_acc[i] * (scaledDt * 0.5);
            }
            CalculateGravitationalForces(bodies);
            Vector3d[] k3_vel = new Vector3d[bodies.Count];
            Vector3d[] k3_acc = new Vector3d[bodies.Count];
            for (int i = 0; i < bodies.Count; i++)
            {
                k3_vel[i] = bodies[i].Velocity;
                k3_acc[i] = bodies[i].Acceleration;
            }
            
            // K4 - evaluate at endpoint
            for (int i = 0; i < bodies.Count; i++)
            {
                if (bodies[i] == solarSystem.sun) continue;
                bodies[i].Position = initialPos[i] + k3_vel[i] * scaledDt;
                bodies[i].Velocity = initialVel[i] + k3_acc[i] * scaledDt;
            }
            CalculateGravitationalForces(bodies);
            Vector3d[] k4_vel = new Vector3d[bodies.Count];
            Vector3d[] k4_acc = new Vector3d[bodies.Count];
            for (int i = 0; i < bodies.Count; i++)
            {
                k4_vel[i] = bodies[i].Velocity;
                k4_acc[i] = bodies[i].Acceleration;
            }
            
            // Combine using RK4 formula
            for (int i = 0; i < bodies.Count; i++)
            {
                if (bodies[i] == solarSystem.sun) continue;
                
                bodies[i].Position = initialPos[i] + (k1_vel[i] + 2.0 * k2_vel[i] + 2.0 * k3_vel[i] + k4_vel[i]) * (scaledDt / 6.0);
                bodies[i].Velocity = initialVel[i] + (k1_acc[i] + 2.0 * k2_acc[i] + 2.0 * k3_acc[i] + k4_acc[i]) * (scaledDt / 6.0);
            }
            
            solarSystem.AdvanceTime(dt * UnitScale.SIM_TIME_SCALE);
        }
        
        /// <summary>
        /// Calculate N-body gravitational forces
        /// </summary>
        void CalculateGravitationalForces(List<Body> bodies)
        {
              // DEBUG: Check on first call
            if (frameCount == 0 && solarSystem.sun != null)
            {
                double sunGM = UnitScale.GetScaledGM(solarSystem.sun.mass);
                Debug.Log($"DEBUG: Sun's scaled GM = {sunGM:E6} Unity³/sim-sec²");
                
                // For Earth at ~150 Unity units, acceleration should be:
                // a = GM / r² = sunGM / (150²) ≈ 7.7e-5
                double expectedAccel = sunGM / (149.6 * 149.6);
                Debug.Log($"DEBUG: Expected Earth acceleration = {expectedAccel:E6} Unity/sim-sec²");
            }
            // Reset accelerations
            foreach (Body body in bodies)
            {
                body.ResetAcceleration();
            }
            
            // Pairwise forces
            for (int i = 0; i < bodies.Count; i++)
            {
                Body bodyA = bodies[i];
                
                for (int j = i + 1; j < bodies.Count; j++)
                {
                    Body bodyB = bodies[j];
                    
                    Vector3d r = bodyB.Position - bodyA.Position;
                    double distSqr = r.sqrMagnitude;
                    
                    if (distSqr < 1e-10) continue;
                    
                    double dist = System.Math.Sqrt(distSqr);
                    Vector3d rHat = r / dist;
                    
                    // Get scaled GM (already in correct units)
                    double gmA = UnitScale.GetScaledGM(bodyA.mass) * UnitScale.SIM_TIME_SCALE * UnitScale.SIM_TIME_SCALE;
                    double gmB = UnitScale.GetScaledGM(bodyB.mass) * UnitScale.SIM_TIME_SCALE * UnitScale.SIM_TIME_SCALE;

                    
                    // Apply gravity scale setting
                    double scale = gravityScale;
                    
                    // AFTER (attractive toward A)
                    double accelMagB = scale * gmA / distSqr;
                    bodyB.AddAcceleration(rHat * (-accelMagB));
                    
                    double accelMagA = scale * gmB / distSqr;
                    bodyA.AddAcceleration(rHat * (-accelMagA));
                }
            }
        }
        
        /// <summary>
        /// Update visual transforms
        /// </summary>
        void UpdateVisuals()
        {
            foreach (Body body in solarSystem.GetAllBodies())
            {
                body.UpdateVisualPosition();
            }
        }
        
        /// <summary>
        /// Reset simulation
        /// </summary>
        public void ResetSimulation()
        {
            Debug.Log("Reset - will implement in Step 5");
        }
    }
}