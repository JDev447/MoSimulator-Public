using System.Collections;
using System.Collections.Generic;
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
        // Only needed if mode is Cage
        public suPURDUEperClimber mainClimber;        
        
        // --- NEW: Public property for the main robot to read ---
        private readonly HashSet<Collider> detectedAlgae = new HashSet<Collider>();

        public bool HasDetectedObject => detectedAlgae.Count > 0;

        private void OnTriggerEnter(Collider other)
        {
            if (mode == SensorMode.Cage)
            {
                if (other.gameObject.CompareTag("Cage")) 
                {
                    if(mainClimber != null) mainClimber.NotifyTriggered(this, true);
                }
            }
            else if (mode == SensorMode.Algae)
            {
                // Check for Algae (Tag or Name fallback)
                if (IsAlgae(other))
                {
                    detectedAlgae.Add(other);
                }
            }
        }

        private void OnTriggerExit(Collider other) 
        {
            if (mode == SensorMode.Cage)
            {
                if (other.gameObject.CompareTag("Cage")) 
                {
                    if(mainClimber != null) mainClimber.NotifyTriggered(this, false);
                }
            }
            else if (mode == SensorMode.Algae)
            {
                if (IsAlgae(other))
                {
                    detectedAlgae.Remove(other);
                }
            }
        }

        // Helper to safely identify Algae
        private bool IsAlgae(Collider other)
        {
            for (Transform current = other.transform; current != null; current = current.parent)
            {
                if (current.CompareTag("Algae")) return true;
            }
            return false;
        }
    }
}
