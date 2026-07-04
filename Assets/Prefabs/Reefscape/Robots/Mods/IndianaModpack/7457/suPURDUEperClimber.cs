using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Prefabs.Reefscape.Robots.Mods.IndianaModpack._7457
{
    public class suPURDUEperClimber : MonoBehaviour
    {
        [Header("Sensors")]
        public suPURDUEperClimberTrigger LeftTrigger;
        public suPURDUEperClimberTrigger RightTrigger;

        [Header("The Hooks")]
        // Drag your Mesh Colliders (the C-shapes) here
        public Collider[] HookColliders; 

        // --- NEW: Audio ---
        [Header("Audio")]
        public AudioSource LatchAudio; // Drag an AudioSource here
        public AudioClip LatchClip;    // Drag your "Clunk" sound here
        // ------------------

        // The Signal for the Main Robot
        public bool IsReadyToClimb { get; private set; }

        private bool _leftTriggered;
        private bool _rightTriggered;
        private bool _hasPlayedLatchSound = false; // Prevents spamming

        private void Start()
        {
            // START GHOSTED: Colliders OFF so we can drive "through" the bar
            SetColliders(HookColliders, false);
            IsReadyToClimb = false;
            
            // Audio Setup
            if (LatchAudio != null)
            {
                LatchAudio.loop = false;
                LatchAudio.playOnAwake = false;
            }
        }

        public void NotifyTriggered(suPURDUEperClimberTrigger sensor, bool isTriggered)
        {
            if (sensor == LeftTrigger) _leftTriggered = isTriggered;
            if (sensor == RightTrigger) _rightTriggered = isTriggered;

            CheckClimbState();
        }

        private void CheckClimbState()
        {
            // If BOTH hooks are deep enough to hit the trigger...
            if (_leftTriggered && _rightTriggered)
            {
                // SOLIDIFY: Turn the colliders ON to trap the bar
                SetColliders(HookColliders, true);
                
                // SIGNAL: Tell the main robot we are locked in!
                IsReadyToClimb = true;

                // PLAY SOUND: Only once when we first latch
                if (LatchAudio != null && LatchClip != null && !_hasPlayedLatchSound)
                {
                    LatchAudio.PlayOneShot(LatchClip);
                    _hasPlayedLatchSound = true;
                }
            }
            else
            {
                // Optional: If you want logic for "slipping off" the bar
                // You would reset _hasPlayedLatchSound = false; here
            }
        }

        private void SetColliders(Collider[] colliders, bool state)
        {
            foreach (var col in colliders)
            {
                if (col != null) col.enabled = state;
            }
        }
    }
}