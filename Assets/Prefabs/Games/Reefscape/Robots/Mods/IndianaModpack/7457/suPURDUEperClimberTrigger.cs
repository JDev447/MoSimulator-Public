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
        public bool HasDetectedObject { get; private set; } = false;

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
                    HasDetectedObject = true;
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
                    HasDetectedObject = false;
                }
            }
        }

        // Helper to safely identify Algae
        private bool IsAlgae(Collider other)
        {
            // Tries multiple checks to be safe
            if (other.CompareTag("GamePiece")) return other.name.Contains("Algae");
            if (other.CompareTag("Algae")) return true;
            if (other.gameObject.layer == LayerMask.NameToLayer("GamePiece") && other.name.Contains("Algae")) return true;
            
            return false;
        }
    }
}