using UnityEngine;
using VoyagerSim.Core;   // Body
using VoyagerSim.Utils;  // Vector3d, UnitScale

public class GravityAssistLogger : MonoBehaviour
{
    // Runtime-found bodies
    private Body voyager;
    private Body jupiter;
    private Body saturn;

    [Header("SOI (Unity units)")]
    public double soiRadiusJupiter = 10.0;   // ~10 million km (realistic close flyby)
    public double soiRadiusSaturn  = 10.0;   // ~10 million km  
    public double exitHysteresis   = 2.0;    // 100% larger for clean exit

    // Per-planet state
    private bool inSOI_J = false, inSOI_S = false;
    private Vector3d vin_J, vin_S;
    private double minDistJ = double.MaxValue, minDistS = double.MaxValue;
    
    // Distance tracking for approach detection
    private double lastDistJ = double.MaxValue;
    private double lastDistS = double.MaxValue;
    private bool approachingJ = false;
    private bool approachingS = false;

    void Start()
    {
        // Force correct values (overrides Inspector)
        soiRadiusJupiter = 10.0;
        soiRadiusSaturn = 10.0;
        exitHysteresis = 2.0;
        
        FindBodies(); // try once at start
    }

    void Update()
    {
        // If any are missing (spawned later), keep trying
        if (voyager == null || jupiter == null || saturn == null)
            FindBodies();
        if (voyager == null) return;

        CheckFlyby(jupiter, ref inSOI_J, ref vin_J, ref minDistJ, soiRadiusJupiter, "Jupiter", ref lastDistJ, ref approachingJ);
        CheckFlyby(saturn,  ref inSOI_S, ref vin_S, ref minDistS, soiRadiusSaturn,  "Saturn", ref lastDistS, ref approachingS);
    }

    void FindBodies()
    {
        // Prefer grabbing from the SolarSystem that spawned them
        var ss = FindObjectOfType<VoyagerSim.Core.SolarSystem>();
        if (ss != null)
        {
            if (jupiter == null) jupiter = ss.planets.Find(p => p.bodyName == "Jupiter");
            if (saturn  == null) saturn  = ss.planets.Find(p => p.bodyName == "Saturn");
            if (voyager == null)
            {
                // If you spawn Voyager via SimulationManager:
                var sim = FindObjectOfType<VoyagerSim.Core.SimulationManager>();
                if (sim != null && sim.voyager != null) voyager = sim.voyager.body;
            }
        }

        // Fallbacks if still null
        if (jupiter == null) jupiter = FindBodyByName("Jupiter");
        if (saturn  == null) saturn  = FindBodyByName("Saturn");
        if (voyager == null) voyager = FindBodyByName("Voyager");

        // Clean, null-safe logs
        if (jupiter == null) Debug.Log("GravityAssistLogger: Jupiter not found yet.");
        if (saturn  == null) Debug.Log("GravityAssistLogger: Saturn not found yet.");
        if (voyager == null) Debug.Log("GravityAssistLogger: Voyager not found yet.");
    }

    Body FindBodyByName(string name)
    {
        // Try exact scene object name first
        var go = GameObject.Find(name);
        if (go != null)
        {
            var b = go.GetComponent<Body>();
            if (b != null) return b;
        }
        // Fallback: search all Bodies by bodyName
        foreach (var b in FindObjectsOfType<Body>())
            if (b.bodyName == name) return b;

        return null;
    }

    void CheckFlyby(Body planet, ref bool insideSOI, ref Vector3d vin,
            ref double minDist, double soiRadiusU, string label, 
            ref double lastDist, ref bool approaching)
    {
        if (planet == null || voyager == null) return;
        
        double distFromSun = voyager.Position.magnitude;
        if (distFromSun < 250.0) return;

        double distU = (voyager.Position - planet.Position).magnitude;
        
        // Track if we're approaching or leaving
        if (lastDist < double.MaxValue)
        {
            if (distU < lastDist - 0.5) // Approaching
            {
                if (!approaching)
                {
                    approaching = true;
                    Debug.Log($"🚀 {label}: APPROACHING! Distance: {distU:F2} U (from {lastDist:F2} U)");
                }
            }
            else if (distU > lastDist + 0.5) // Leaving
            {
                if (approaching)
                {
                    approaching = false;
                    // LOG THE TRUE MINIMUM BEFORE IT GETS RESET!
                    Debug.Log($"📉 {label}: Moving away. Distance: {distU:F2} U. TRUE Min approach: {minDist:F2} U ({UnitScale.UnityToMeters(minDist)/1e6:F0} million km)");
                }
            }
        }
        lastDist = distU;

        // Enter SOI
        if (!insideSOI && distU <= soiRadiusU)
        {
            insideSOI = true;
            vin       = voyager.Velocity;
            minDist   = distU;

            double speedBefore = UnitScale.UnityPerSimSecToKmPerSec(vin.magnitude);
            Debug.Log($"=== {label.ToUpper()} ENCOUNTER ===");
            Debug.Log($"Entering SOI at distance: {distU:F2} U");
            Debug.Log($"Speed before: {speedBefore:F2} km/s");
        }

        if (!insideSOI) 
        {
            if (distU < minDist) minDist = distU;
            return;
        }

        // Track closest approach inside SOI
        if (distU < minDist) minDist = distU;

        // Exit SOI
        if (distU > soiRadiusU * exitHysteresis)
        {
            Vector3d vout = voyager.Velocity;

            double magIn  = vin.magnitude;
            double magOut = vout.magnitude;
            if (magIn > 1e-12 && magOut > 1e-12)
            {
                double dot = Vector3d.Dot(vin, vout) / (magIn * magOut);
                dot = System.Math.Max(-1.0, System.Math.Min(1.0, dot));
                double deflectDeg = System.Math.Acos(dot) * Mathf.Rad2Deg;

                double speedAfter = UnitScale.UnityPerSimSecToKmPerSec(magOut);
                Debug.Log($"=== {label.ToUpper()} EXIT ===");
                Debug.Log($"Speed after: {speedAfter:F2} km/s");
                Debug.Log($"Deflection: {deflectDeg:F1}° (heliocentric velocity change)");
                Debug.Log($"Closest approach: {minDist:F2} U ({UnitScale.UnityToMeters(minDist)/1e6:F0} million km)");
            }

            insideSOI = false;
            minDist = double.MaxValue;  // Reset AFTER logging
        }
    }
}