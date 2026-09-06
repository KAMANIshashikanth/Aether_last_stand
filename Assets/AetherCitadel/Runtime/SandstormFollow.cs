using UnityEngine;

namespace Aether.Citadel
{
    /// <summary>
    /// Keeps a world-space particle volume centred on the viewer so the storm
    /// travels with the player instead of only existing over the city. Position is
    /// snapped to a grid so drifting particles never visibly jump.
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("Aether/Sandstorm Follow")]
    public class SandstormFollow : MonoBehaviour
    {
        [Tooltip("Grid the rig snaps to, in metres. Larger values move it less often.")]
        public float snap = 10f;

        [Tooltip("Height the rig sits at, independent of the camera.")]
        public float height = 0f;

        [Tooltip("Offset along the wind so particles have room to blow in from upwind.")]
        public float upwindLead = 60f;

        [Tooltip("Direction the wind blows towards, in world space.")]
        public Vector3 windDirection = new Vector3(0.72f, 0f, 0.69f);

        void LateUpdate()
        {
            Transform viewer = Viewer();
            if (viewer == null) return;

            Vector3 wind = windDirection.sqrMagnitude < 0.0001f ? Vector3.forward : windDirection.normalized;
            Vector3 p = viewer.position - wind * upwindLead;

            float s = Mathf.Max(0.5f, snap);
            p.x = Mathf.Round(p.x / s) * s;
            p.z = Mathf.Round(p.z / s) * s;
            p.y = height;

            transform.position = p;
        }

        /// <summary>
        /// Whose eyes the storm should surround. In play mode that is the game camera;
        /// while editing it is the Scene view, otherwise the rig sits wherever Main Camera
        /// happens to be parked and the storm looks like it is missing entirely.
        /// </summary>
        Transform Viewer()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                var sv = UnityEditor.SceneView.lastActiveSceneView;
                if (sv != null && sv.camera != null) return sv.camera.transform;
            }
#endif
            Camera cam = Camera.main;
            if (cam != null) return cam.transform;
            var all = Camera.allCameras;
            return all.Length > 0 ? all[0].transform : null;
        }
    }
}
