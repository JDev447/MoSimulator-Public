using UnityEngine;

namespace Prefabs.Reefscape.Robots.Mods.IndianaModpack._7457
{
    public enum SensorMode
    {
        Cage,
        Algae
    }

    public class suPURDUEperClimberTrigger : MonoBehaviour
    {
        [Header("Settings")]
        public SensorMode mode = SensorMode.Cage;

        [Header("Cage Mode References")]
        public suPURDUEperClimber mainClimber;

        private BoxCollider algaeDetector;

        public bool HasDetectedObject
        {
            get
            {
                if (mode != SensorMode.Algae) return false;
                if (algaeDetector == null) algaeDetector = GetComponent<BoxCollider>();
                if (algaeDetector == null || !algaeDetector.enabled) return false;

                Transform sensor = algaeDetector.transform;
                Vector3 scale = sensor.lossyScale;
                Vector3 halfExtents = Vector3.Scale(
                    algaeDetector.size,
                    new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z))) * 0.5f;
                Collider[] overlaps = Physics.OverlapBox(
                    sensor.TransformPoint(algaeDetector.center),
                    halfExtents,
                    sensor.rotation,
                    ~0,
                    QueryTriggerInteraction.Collide);

                foreach (Collider other in overlaps)
                {
                    if (IsAlgae(other)) return true;
                }
                return false;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (mode == SensorMode.Cage && other.CompareTag("Cage") && mainClimber != null)
                mainClimber.NotifyTriggered(this, true);
        }

        private void OnTriggerExit(Collider other)
        {
            if (mode == SensorMode.Cage && other.CompareTag("Cage") && mainClimber != null)
                mainClimber.NotifyTriggered(this, false);
        }

        private static bool IsAlgae(Collider other)
        {
            for (Transform current = other.transform; current != null; current = current.parent)
            {
                if (current.CompareTag("Algae")) return true;
            }
            return false;
        }
    }
}
