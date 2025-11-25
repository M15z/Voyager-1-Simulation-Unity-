using UnityEngine;
using VoyagerSim.Utils;

namespace VoyagerSim.Core
{
    public class Body : MonoBehaviour
    {
        [Header("Physical Properties")]
        public double mass = 1.0;
        public float visualRadius = 1.0f;

        [Header("Orbital Elements (for planets only)")]
        public double semiMajorAxis = 0;
        public double orbitalPeriod = 1;
        public float initialAngle = 0f;

        [Header("Identification")]
        public string bodyName = "Body";
        public Color bodyColor = Color.white;

        [Header("Runtime State (Read-Only)")]
        [SerializeField] private Vector3d _position;
        [SerializeField] private Vector3d _velocity;
        [SerializeField] private Vector3d _acceleration;

        public Vector3d Position { get => _position; set => _position = value; }
        public Vector3d Velocity { get => _velocity; set => _velocity = value; }
        public Vector3d Acceleration { get => _acceleration; set => _acceleration = value; }

        // Cache the *first* renderer it finds (root or child)
        private Renderer _renderer;
        private TrailRenderer _trailRenderer;

        void Awake()
        {
            _renderer = GetComponent<Renderer>();
            if (_renderer == null) _renderer = GetComponentInChildren<Renderer>(true);
            _trailRenderer = GetComponent<TrailRenderer>();
        }

        public void Initialize(Vector3d position, Vector3d velocity, double mass, string name)
        {
            _position = position;
            _velocity = velocity;
            this.mass = mass;
            bodyName = name;
            gameObject.name = name;
            UpdateVisualPosition();
        }

        public void InitializeOrbit(double semiMajorAxis, double orbitalPeriod, float initialAngleDeg)
        {
            this.semiMajorAxis = semiMajorAxis;
            this.orbitalPeriod = orbitalPeriod;
            this.initialAngle = initialAngleDeg;

            double angleRad = initialAngleDeg * Mathf.Deg2Rad;
            double rU = UnitScale.MetersToUnity(semiMajorAxis);

            _position = new Vector3d(
                rU * System.Math.Cos(angleRad),
                0,
                rU * System.Math.Sin(angleRad)
            );

            double sunGM = UnitScale.GetScaledGM(1.989e30);
            double vBase = System.Math.Sqrt(sunGM / rU);

            // If your PhysicsIntegrator already multiplies by a time-scale,
            // leave the next line as-is. If not, you can keep the SIM_TIME_SCALE boost.
            double v = vBase * UnitScale.SIM_TIME_SCALE;

            _velocity = new Vector3d(
                -v * System.Math.Sin(angleRad),
                0,
                v * System.Math.Cos(angleRad)
            );

            UpdateVisualPosition();
            LogPosVelAngle();

            double realKmPerSec = UnitScale.UnityPerSimSecToKmPerSec(v);
            Debug.Log($"{bodyName} initialized: r={rU:F2} U, v={v:F6} U/sim-s ({realKmPerSec:F2} km/s)");
        }

        public void UpdateVisualPosition() => transform.position = _position.ToVector3();

        public void SetColor(Color color)
        {
            bodyColor = color;
            if (_renderer == null) return;

            // Don’t overwrite textured materials (keeps your nice planet textures)
            var mat = _renderer.material; // instance, safe to tint
            if (mat != null && mat.mainTexture == null)
            {
                mat.color = color;
            }
        }

        public double GetSpeedKmPerSec() => UnitScale.UnityPerSimSecToKmPerSec(_velocity.magnitude);

        public double GetDistanceFromBody(Body other)
        {
            double distU = (_position - other._position).magnitude;
            double distM = UnitScale.UnityToMeters(distU);
            return distM / 1e9;
        }

        public void ResetAcceleration() => _acceleration = Vector3d.zero;
        public void AddAcceleration(Vector3d accel) => _acceleration += accel;

        private void LogPosVelAngle()
        {
            double denom = _position.magnitude * _velocity.magnitude;
            if (denom <= 1e-12) return;

            double dot = _position.x * _velocity.x + _position.z * _velocity.z;
            double cos = System.Math.Max(-1.0, System.Math.Min(1.0, dot / denom));
            double angleDeg = System.Math.Acos(cos) * (180.0 / System.Math.PI);
            Debug.Log($"{bodyName} pos·vel angle: {angleDeg:F2}° (≈90° for circular)");
        }
    }
}
