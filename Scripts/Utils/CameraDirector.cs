using System.Collections;
using UnityEngine;

namespace VoyagerSim.Utils
{
    public class CameraDirector : MonoBehaviour
    {
        [Header("Refs")]
        public CameraFollow cam;
        public Transform voyager, sun;

        [Header("Finale")]
        public float finaleSunDistanceMKm = 20000f;
        public string screenshotFile = "SolarSystemFinale.png";
        public int superSize = 2;

        [Header("Timing (s)")]
        public float moveLerp = 0.9f;   // blend between shots
        public float holdShort = 2.0f;  // quick cuts
        public float holdMed   = 3.0f;
        public float holdLong  = 4.0f;

        Camera _unityCam;

        void Start()
        {
            if (!cam) cam = Camera.main.GetComponent<CameraFollow>();
            _unityCam = Camera.main;
            StartCoroutine(Sequence());
        }

        IEnumerator Sequence()
        {
            if (!cam || !voyager) yield break;
            cam.allowRotation = cam.allowZoom = false;

            // --- All shots stay on Voyager ---
            cam.SetTarget(voyager);

            // 1) Low rear 3/4, slight shake (Ignition vibe)
            yield return VoyagerShot(dist:10f, tilt:5f, rot:20f, side:2f, fov:55f, hold:holdMed, shake:0.08f);

            // 2) Fast side track (whoosh by)
            yield return VoyagerShot(dist:8f, tilt:6f, rot:-70f, side:0.5f, fov:60f, hold:holdShort, shake:0.05f);

            // 3) Engine close-up (very close, dramatic)
            yield return VoyagerShot(dist:3.2f, tilt:-2f, rot:190f, side:0f, fov:65f, hold:holdShort, shake:0.10f);

            // 4) Front-lead (camera ahead, Voyager chasing lens)
            yield return VoyagerShot(dist:7.5f, tilt:4f, rot:0f, side:-2f, fov:58f, hold:holdMed, shake:0.04f);

            // 5) Spiral pull-up (roll around and climb)
            yield return SpiralAroundVoyager(duration:6f, startRot:cam.rotationAngle, sweep:140f, startTilt:8f, endTilt:28f, startDist:9f, endDist:14f);

            // Wait until Voyager is “out”, then one wide
            yield return WaitUntilVoyagerFar();

            // Finale wide of the whole system
            cam.OverviewView();
            cam.SetTarget(sun ? sun : voyager);
            cam.distance = 400f; cam.tiltAngle = 35f; cam.rotationAngle = 45f;
            yield return new WaitForSeconds(moveLerp + 0.5f);
            yield return new WaitForSeconds(1f);
            ScreenCapture.CaptureScreenshot(screenshotFile, superSize);

            cam.allowRotation = cam.allowZoom = true;
        }

        // ---------- Shot helpers ----------
        IEnumerator VoyagerShot(float dist, float tilt, float rot, float side, float fov, float hold, float shake)
        {
            // blend to target params
            yield return StartCoroutine(BlendCam(dist, tilt, rot, side, fov, moveLerp));
            // hold with subtle shake
            float t = 0f;
            while (t < hold)
            {
                t += Time.deltaTime;
                if (shake > 0f) ApplyShake(shake);
                yield return null;
            }
            ClearShake();
        }

        IEnumerator SpiralAroundVoyager(float duration, float startRot, float sweep, float startTilt, float endTilt, float startDist, float endDist)
        {
            float t = 0f;
            float baseFov = _unityCam ? _unityCam.fieldOfView : 60f;
            while (t < duration)
            {
                float u = t / duration;
                cam.rotationAngle = Mathf.Lerp(startRot, startRot + sweep, EaseInOut(u));
                cam.tiltAngle = Mathf.Lerp(startTilt, endTilt, EaseInOut(u));
                cam.distance = Mathf.Lerp(startDist, endDist, EaseInOut(u));
                if (_unityCam) _unityCam.fieldOfView = Mathf.Lerp(baseFov, baseFov + 4f, u);
                ApplyShake(0.03f);
                t += Time.deltaTime;
                yield return null;
            }
            ClearShake();
        }

        IEnumerator BlendCam(float dist, float tilt, float rot, float side, float fov, float time)
        {
            float sd0 = cam.distance, tl0 = cam.tiltAngle, rt0 = cam.rotationAngle, so0 = cam.sideOffset;
            float f0 = _unityCam ? _unityCam.fieldOfView : 60f;
            float t = 0f;
            while (t < time)
            {
                float u = EaseInOut(t / time);
                cam.distance      = Mathf.Lerp(sd0, dist, u);
                cam.tiltAngle     = Mathf.Lerp(tl0, tilt, u);
                cam.rotationAngle = Mathf.Lerp(rt0, rot, u);
                cam.sideOffset    = Mathf.Lerp(so0, side, u);
                if (_unityCam) _unityCam.fieldOfView = Mathf.Lerp(f0, fov, u);
                t += Time.deltaTime;
                yield return null;
            }
        }

        // per-frame micro shake (adds tiny roll + jitter)
        float _shakeTime;
        void ApplyShake(float amp)
        {
            _shakeTime += Time.deltaTime * 3f;
            float jitter = (Mathf.PerlinNoise(_shakeTime, 0.37f) - 0.5f) * 2f * amp;
            float roll   = Mathf.Sin(_shakeTime * 2.1f) * amp * 2f; // degrees
            var t = cam.transform;
            t.position += cam.transform.right * jitter * 0.2f + cam.transform.up * jitter * 0.1f;
            t.Rotate(Vector3.forward, roll, Space.Self);
        }
        void ClearShake() { /* no persistent state to clear */ }

        static float EaseInOut(float x) => x < 0.5f ? 2f*x*x : 1f - Mathf.Pow(-2f*x + 2f, 2f)/2f;

        IEnumerator WaitUntilVoyagerFar()
        {
            var sb = sun ? sun.GetComponent<VoyagerSim.Core.Body>() : null;
            var vb = voyager.GetComponent<VoyagerSim.Core.Body>();
            while (true)
            {
                if (sb && vb)
                {
                    if (vb.GetDistanceFromBody(sb) >= finaleSunDistanceMKm) break;
                }
                else if (sun && Vector3.Distance(voyager.position, sun.position) >= finaleSunDistanceMKm) break;
                yield return null;
            }
        }
    }
}
