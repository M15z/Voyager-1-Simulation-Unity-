using System.Collections.Generic;
using UnityEngine;
using VoyagerSim.Utils;
using VoyagerSim.Core;

namespace VoyagerSim.Core
{
    /// <summary>
    /// Manages all celestial bodies in the simulation.
    /// Loads planet data, spawns bodies, and coordinates updates.
    /// </summary>
    public class SolarSystem : MonoBehaviour
    {
        [Header("Data Source")]
        [Tooltip("JSON file with planet data")]
        public TextAsset planetDataJSON;

        [Header("Prefabs")]
        [Tooltip("Prefab for planets (sphere with Body component)")]
        public GameObject planetPrefab;

        // ---- DESIGN-ONLY: per-planet prefab overrides (single definition) ----
        [System.Serializable]
        public class PrefabOverride
        {
            public string name;       // must match JSON "name"
            public GameObject prefab; // custom prefab for that body
        }
        [Tooltip("Optional: map body name -> custom prefab. Falls back to Planet Prefab.")]
        public List<PrefabOverride> prefabOverrides = new List<PrefabOverride>();
        // ---------------------------------------------------------------------

        [Header("Runtime Bodies")]
        public Body sun;
        public List<Body> planets = new List<Body>();

        [Header("Simulation State")]
        public double simulationTime = 0;

        // ----- JSON data shapes -----
        [System.Serializable]
        private class BodyData
        {
            public string name;
            public double mass;
            public double radius;
            public double semiMajorAxis;
            public double orbitalPeriod;  // in sim-seconds (your JSON should match)
            public ColorData color;
            public float visualRadius;
        }

        [System.Serializable]
        private class ColorData
        {
            public float r, g, b, a;
            public Color ToColor() => new Color(r, g, b, a);
        }

        [System.Serializable]
        private class PlanetDatabase
        {
            public List<BodyData> bodies;
        }
        // -----------------------------

        void Awake() => LoadAndSpawnBodies();

        void LoadAndSpawnBodies()
        {
            if (planetDataJSON == null)
            {
                Debug.LogError("SolarSystem: No planet data JSON assigned!");
                return;
            }
            if (planetPrefab == null)
            {
                Debug.LogError("SolarSystem: No planet prefab assigned!");
                return;
            }

            PlanetDatabase database = JsonUtility.FromJson<PlanetDatabase>(planetDataJSON.text);
            if (database == null || database.bodies == null)
            {
                Debug.LogError("SolarSystem: Failed to parse planet data JSON!");
                return;
            }

            // prevent lingering serialized entries from Inspector
            planets.Clear();
            sun = null;

            Debug.Log($"SolarSystem: Loaded {database.bodies.Count} bodies from JSON");

            foreach (BodyData data in database.bodies)
                SpawnBody(data);

            Debug.Log($"SolarSystem: Spawned {planets.Count} planets and 1 sun");
        }

        // pick override prefab if present
        GameObject GetPrefabFor(string bodyName)
        {
            if (prefabOverrides != null)
            {
                var ov = prefabOverrides.Find(p => p != null && p.name == bodyName);
                if (ov != null && ov.prefab != null) return ov.prefab;
            }
            return planetPrefab;
        }

        /// <summary>
        /// Instantiate a body GameObject from data
        /// </summary>
        void SpawnBody(BodyData data)
        {
            // ONLY DESIGN CHANGE: use per-planet prefab when available
            GameObject bodyObj = Instantiate(GetPrefabFor(data.name), transform);
            bodyObj.name = data.name;
            bodyObj.transform.localScale = Vector3.one * data.visualRadius;

            Body body = bodyObj.GetComponent<Body>();
            if (body == null)
            {
                Debug.LogError($"Prefab for {data.name} missing Body component!");
                Destroy(bodyObj);
                return;
            }

            body.mass = data.mass;
            body.visualRadius = data.visualRadius;
            body.bodyName = data.name;

            // only tint if no texture, so your custom textures stay intact
            var rend = bodyObj.GetComponentInChildren<Renderer>();
            if (rend == null || (rend.sharedMaterial != null && rend.sharedMaterial.mainTexture == null))
                body.SetColor(data.color.ToColor());

            if (data.name == "Sun")
            {
                body.Initialize(Vector3d.zero, Vector3d.zero, data.mass, data.name);
                sun = body;
            }
            else
            {
                float startAngle = 0f;

                if (data.name == "Jupiter")
                {
                    // original alignment math (unchanged)
                    double mu  = UnitScale.GetScaledGM(1.989e30);
                    double r1  = UnitScale.MetersToUnity(149.6e9);   // Earth SMA
                    double r2  = UnitScale.MetersToUnity(778.57e9);  // Jupiter SMA
                    double aH  = 0.5 * (r1 + r2);
                    double vH  = System.Math.Sqrt(mu * (2.0 / r1 - 1.0 / aH));
                    double vPeri = vH;

                    startAngle = (float)RequiredPhaseDeg(r1, r2, mu, data.orbitalPeriod, vPeri);
                    Debug.Log($"Jupiter positioned at {startAngle:F2}° for Voyager 1 encounter");
                }
                else if (data.name == "Saturn")
                {
                    // null-safe lookup (prevents NRE; logic unchanged)
                    Body jupiter = planets.Find(p => p != null && p.bodyName == "Jupiter");
                    if (jupiter != null)
                    {
                        double mu  = UnitScale.GetScaledGM(1.989e30);
                        double r1  = UnitScale.MetersToUnity(149.6e9);
                        double r2  = UnitScale.MetersToUnity(778.57e9);
                        double aH  = 0.5 * (r1 + r2);
                        double vH  = System.Math.Sqrt(mu * (2.0 / r1 - 1.0 / aH));
                        double vPeri = vH * 1.05;
                        float jupiterAngle = (float)RequiredPhaseDeg(r1, r2, mu, jupiter.orbitalPeriod, vPeri);

                        startAngle = jupiterAngle + 29.65f;
                        Debug.Log($"Saturn positioned at {startAngle:F2}° (Jupiter at {jupiterAngle:F2}°) for Grand Tour trajectory");
                    }
                    else
                    {
                        startAngle = 145f;  // Fallback position
                    }
                }
                else
                {
                    // Simple fixed angles for the others
                    switch (data.name)
                    {
                        case "Mercury": startAngle = 45f;  break;
                        case "Venus":   startAngle = 290f; break;
                        case "Earth":   startAngle = 0f;   break;
                        default:        startAngle = Random.Range(0f, 360f); break;
                    }
                }

                body.semiMajorAxis = data.semiMajorAxis;
                body.orbitalPeriod = data.orbitalPeriod;
                body.InitializeOrbit(data.semiMajorAxis, data.orbitalPeriod, startAngle);
                planets.Add(body);
            }

            Debug.Log($"Spawned {data.name} at {body.Position} with mass {data.mass:E2} kg");
        }

        public List<Body> GetAllBodies()
        {
            List<Body> all = new List<Body>();
            if (sun != null) all.Add(sun);
            foreach (var p in planets) if (p != null) all.Add(p);

            var sim = GetComponent<SimulationManager>();
            if (sim != null && sim.voyager != null && sim.voyager.body != null)
                all.Add(sim.voyager.body);

            return all;
        }

        public void AdvanceTime(double dt) => simulationTime += dt;

        // ---- helper: phase angle for arbitrary perihelion speed (matches 1.05× logic) ----
        static double RequiredPhaseDeg(double r1U, double r2U, double mu, double T2_sim, double v_periU)
        {
            double a = 1.0 / (2.0 / r1U - (v_periU * v_periU) / mu);
            double e = 1.0 - r1U / a; // rp = r1

            // true anomaly at r = r2
            double cosf = (a * (1.0 - e * e) / r2U - 1.0) / e;
            cosf = System.Math.Max(-1.0, System.Math.Min(1.0, cosf));
            double f = System.Math.Acos(cosf);

            // eccentric anomaly
            double cosE = (e + System.Math.Cos(f)) / (1.0 + e * System.Math.Cos(f));
            double sinE = System.Math.Sqrt(1.0 - e * e) * System.Math.Sin(f) / (1.0 + e * System.Math.Cos(f));
            double E    = System.Math.Atan2(sinE, cosE);

            // time-of-flight to r2
            double n   = System.Math.Sqrt(mu / (a * a * a));
            double M   = E - e * System.Math.Sin(E);
            double TOF = M / n;

            // Jupiter mean motion and required lead
            double n2  = 2.0 * System.Math.PI / T2_sim;
            double phi = f - n2 * TOF;
            phi = (phi % (2.0 * System.Math.PI) + 2.0 * System.Math.PI) % (2.0 * System.Math.PI);

            return phi * Mathf.Rad2Deg;
        }
    }
}
