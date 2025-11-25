using System.Collections.Generic;
using UnityEngine;
using VoyagerSim.Core;
using VoyagerSim.Utils;

public class PhysicsDebugger : MonoBehaviour
{
    public PhysicsIntegrator integrator;
    public SolarSystem solarSystem;

    [Header("Monitoring")]
    public bool logOrbitalData = true;
    public float logInterval = 5f;

    private double initialEnergy;
    private bool recordedInitial = false;
    private float timer = 0f;

    void Update()
    {
        if (solarSystem == null || integrator == null) return;
        if (!logOrbitalData) return;

        // Record initial energy after first frame
        if (!recordedInitial && Time.time > 1f)
        {
            initialEnergy = GetTotalEnergy();
            recordedInitial = true;
            Debug.Log($"=== Initial System Energy: {initialEnergy:E4} ===");
        }

        // Print debug info at intervals
        timer += Time.deltaTime;
        if (timer > logInterval && recordedInitial)
        {
            timer = 0f;
            LogSystemState();
        }
    }

    void LogSystemState()
    {
        double currentEnergy = GetTotalEnergy();

        double energyDrift = 0.0;
        if (System.Math.Abs(initialEnergy) > double.Epsilon)
        {
            energyDrift = System.Math.Abs((currentEnergy - initialEnergy) / initialEnergy) * 100.0;
        }

        Debug.Log($"=== t={solarSystem.simulationTime:F0}s | Energy Drift: {energyDrift:F3}% ===");

        // Log each planet
        foreach (Body planet in solarSystem.planets)
        {
            double distFromSun = (planet.Position - solarSystem.sun.Position).magnitude;
            double distAU = UnitScale.UnityToMeters(distFromSun) / 1.496e11; // AU
            double speedKmS = planet.GetSpeedKmPerSec();

            // Calculate expected orbital radius for comparison
            double expectedRadiusUnity = UnitScale.MetersToUnity(planet.semiMajorAxis);
            double radiusError = 0.0;
            if (expectedRadiusUnity > double.Epsilon)
            {
                radiusError = System.Math.Abs(distFromSun - expectedRadiusUnity) / expectedRadiusUnity * 100.0;
            }

            Debug.Log($"  {planet.bodyName}: {distAU:F3} AU (error: {radiusError:F2}%), v={speedKmS:F2} km/s");
        }
    }

    /// <summary>
    /// Calculate total system energy (kinetic + potential)
    /// </summary>
    double GetTotalEnergy()
    {
        List<Body> bodies = solarSystem.GetAllBodies();
        double totalKE = 0.0;
        double totalPE = 0.0;

        // Kinetic energy: KE = 0.5 * m * v²
        foreach (Body body in bodies)
        {
            double speed = body.Velocity.magnitude;
            double kineticEnergy = 0.5 * body.mass * speed * speed;
            totalKE += kineticEnergy;
        }

        // Potential energy: PE = -G * m1 * m2 / r (pairwise)
        for (int i = 0; i < bodies.Count; i++)
        {
            for (int j = i + 1; j < bodies.Count; j++)
            {
                Body bodyA = bodies[i];
                Body bodyB = bodies[j];

                double distUnity = (bodyB.Position - bodyA.Position).magnitude;
                if (distUnity < 1e-10) continue;

                // Convert distance back to meters for energy calculation
                double distMeters = UnitScale.UnityToMeters(distUnity);

                // PE in SI units
                double potentialEnergy = -UnitScale.G * bodyA.mass * bodyB.mass / distMeters;
                totalPE += potentialEnergy;
            }
        }

        return totalKE + totalPE;
    }
}
