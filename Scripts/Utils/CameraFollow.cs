using UnityEngine;
using UnityEngine.InputSystem;

namespace VoyagerSim.Utils
{
    /// <summary>
    /// Cinematic camera follow for Voyager - immersive close-up view of the Grand Tour
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("The object to follow (Voyager)")]
        public Transform target;
        
        [Header("Cinematic Camera Settings")]
        [Tooltip("Distance behind Voyager")]
        [Range(1f, 100f)]
        public float distance = 12f;  // Close but can see Voyager
        
        [Tooltip("Height above Voyager")]
        [Range(-10f, 20f)]
        public float heightOffset = 4f;  // Slightly above for better view
        
        [Tooltip("Side offset (left/right)")]
        [Range(-20f, 20f)]
        public float sideOffset = 3f;  // Slightly to the side for cinematic angle
        
        [Tooltip("Camera smoothness (lower = smoother)")]
        [Range(0.01f, 0.5f)]
        public float smoothSpeed = 0.08f;  // Smooth cinematic motion
        
        [Tooltip("Camera tilt (degrees down from horizon)")]
        [Range(-30f, 60f)]
        public float tiltAngle = 8f;  // Looking slightly down at Voyager
        
        [Header("Rotation")]
        [Tooltip("Orbital angle around Voyager")]
        [Range(-180f, 180f)]
        public float rotationAngle = 25f;  // Angled view (not directly behind)
        
        [Tooltip("Enable Q/E rotation")]
        public bool allowRotation = true;
        
        [Tooltip("Rotation speed")]
        public float rotationSpeed = 40f;
        
        [Header("Zoom Control")]
        [Tooltip("Enable mouse wheel zoom")]
        public bool allowZoom = true;
        
        [Tooltip("Zoom speed")]
        public float zoomSpeed = 2f;  // Slower, more precise zoom
        
        [Tooltip("Closest zoom")]
        public float minDistance = 2f;  // Very close
        
        [Tooltip("Farthest zoom")]
        public float maxDistance = 200f;  // Overview distance
        
        private Vector3 velocity = Vector3.zero;
        
        void LateUpdate()
        {
            if (target == null) return;
            
            HandleInput();
            UpdateCameraPosition();
        }
        
        void HandleInput()
        {
            // Zoom with mouse wheel
            if (allowZoom && Mouse.current != null)
            {
                float scroll = Mouse.current.scroll.ReadValue().y;
                if (scroll != 0f)
                {
                    distance -= scroll * zoomSpeed * 0.01f;
                    distance = Mathf.Clamp(distance, minDistance, maxDistance);
                }
            }
            
            // Rotate with Q/E or arrow keys
            if (allowRotation && Keyboard.current != null)
            {
                if (Keyboard.current.qKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
                {
                    rotationAngle += rotationSpeed * Time.deltaTime;
                }
                if (Keyboard.current.eKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
                {
                    rotationAngle -= rotationSpeed * Time.deltaTime;
                }
                
                // Up/Down to adjust tilt
                if (Keyboard.current.upArrowKey.isPressed)
                {
                    tiltAngle = Mathf.Clamp(tiltAngle + 20f * Time.deltaTime, -30f, 60f);
                }
                if (Keyboard.current.downArrowKey.isPressed)
                {
                    tiltAngle = Mathf.Clamp(tiltAngle - 20f * Time.deltaTime, -30f, 60f);
                }
                
                // Reset view with R key
                if (Keyboard.current.rKey.wasPressedThisFrame)
                {
                    CinematicView();
                }
            }
        }
        
        void UpdateCameraPosition()
        {
            Vector3 targetPosition = target.position;
            
            // Calculate orbital position around Voyager
            float angleRad = rotationAngle * Mathf.Deg2Rad;
            float tiltRad = tiltAngle * Mathf.Deg2Rad;
            
            // Spherical coordinates for camera position
            float horizontalDist = distance * Mathf.Cos(tiltRad);
            float verticalDist = distance * Mathf.Sin(tiltRad);
            
            Vector3 offset = new Vector3(
                horizontalDist * Mathf.Sin(angleRad) + sideOffset,
                verticalDist + heightOffset,
                horizontalDist * Mathf.Cos(angleRad)
            );
            
            Vector3 desiredPosition = targetPosition - offset;
            
            // Smooth follow for cinematic feel
            transform.position = Vector3.SmoothDamp(
                transform.position, 
                desiredPosition, 
                ref velocity, 
                smoothSpeed
            );
            
            // Always look at Voyager
            transform.LookAt(targetPosition);
        }
        
        /// <summary>
        /// Set the target to follow
        /// </summary>
        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            
            if (target != null)
            {
                // Snap to target immediately
                float angleRad = rotationAngle * Mathf.Deg2Rad;
                float tiltRad = tiltAngle * Mathf.Deg2Rad;
                float horizontalDist = distance * Mathf.Cos(tiltRad);
                float verticalDist = distance * Mathf.Sin(tiltRad);
                
                Vector3 offset = new Vector3(
                    horizontalDist * Mathf.Sin(angleRad) + sideOffset,
                    verticalDist + heightOffset,
                    horizontalDist * Mathf.Cos(angleRad)
                );
                
                transform.position = target.position - offset;
                transform.LookAt(target.position);
            }
        }
        
        /// <summary>
        /// Cinematic close-up view (DEFAULT)
        /// </summary>
        public void CinematicView()
        {
            distance = 12f;
            heightOffset = 4f;
            sideOffset = 3f;
            tiltAngle = 8f;
            rotationAngle = 25f;
        }
        
        /// <summary>
        /// First-person "on board" view
        /// </summary>
        public void FirstPersonView()
        {
            distance = 2f;
            heightOffset = 0.5f;
            sideOffset = 0f;
            tiltAngle = 0f;
            rotationAngle = 0f;
        }
        
        /// <summary>
        /// Wide overview for seeing full trajectory
        /// </summary>
        public void OverviewView()
        {
            distance = 150f;
            heightOffset = 50f;
            sideOffset = 0f;
            tiltAngle = 35f;
            rotationAngle = 45f;
        }
        
        /// <summary>
        /// Top-down orbital view
        /// </summary>
        public void TopDownView()
        {
            distance = 100f;
            heightOffset = 0f;
            sideOffset = 0f;
            tiltAngle = 89f;
            rotationAngle = 0f;
        }
    }
}