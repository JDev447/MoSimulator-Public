using MoSimCore.Enums;
using RobotFramework.Controllers.GamePieceSystem;
using Games.Reefscape.Enums;
using Games.Reefscape.GamePieceSystem;
using MoSimCore.BaseClasses.GameManagement;
using UnityEngine;

namespace Prefabs.Reefscape.Robots.Mods.IndianaModpack._7457
{
    public class suPURDUEperLEDS : MonoBehaviour
    {
        [Header("LED Components")]
        public GameObject[] leds;
        public Shader shaderGraphShader;
        private Material _ledMaterial;

        [Header("Auto-Align Custom Lighting")]
        [Tooltip("Distance in meters to consider the robot 'aligned' to the reef node.")]
        [SerializeField] private float alignmentDistanceTolerance = 0.15f; 
        [Tooltip("Angle in degrees to consider the robot 'aligned' to the reef node.")]
        [SerializeField] private float alignmentAngleTolerance = 3.0f;

        [Header("LED Textures")]
        public Texture L1Mode;        // Drag your Green texture here
        public Texture AutoAligning;  // Drag your Red/Orange texture here
        public Texture AutoAligned;   // Drag your Blue texture here

        // Internal References
        private RobotGamePieceControllerBase _gamePieceManager;
        private Games.Reefscape.Robots.ReefscapeRobotBase _robotBase;

        private void Start()
        {
            _gamePieceManager = GetComponent<RobotGamePieceControllerBase>();
            _robotBase = GetComponent<Games.Reefscape.Robots.ReefscapeRobotBase>();
            
            if (shaderGraphShader != null)
            {
                _ledMaterial = new Material(shaderGraphShader);
                foreach (var led in leds)
                {
                    if (led != null && led.TryGetComponent<Renderer>(out var ren))
                    {
                        ren.material = _ledMaterial;
                    }
                }
            }
        }
        
        private void Update()
        {
            // If the game is paused, disabled, or missing components, turn the lights off
            if (_ledMaterial == null || _robotBase == null || _gamePieceManager == null || BaseGameManager.Instance.RobotState == RobotState.Disabled)
            {
                TurnLedsOff();
                return;
            }

            // --- 1. CHECK AUTO-AIMING ---
            bool isAutoAligning = _robotBase.AutoAlignLeftAction.IsPressed() || _robotBase.AutoAlignRightAction.IsPressed();
            
            if (isAutoAligning)
            {
                if (IsRobotAlignedToReef())
                {
                    // ALIGNED & READY TO SCORE: Turn ON (Blue texture)
                    ApplyLedState(0f, 0.0f, 20f, AutoAligned);
                }
                else
                {
                    // ALIGNING IN PROGRESS: Turn ON (Red/Orange texture)
                    ApplyLedState(0f, 0.0f, 20f, AutoAligning);
                }
                return; // Stop reading here so we stay lit while aiming!
            }

            // --- 2. CHECK L1 MODE ---
            if (_robotBase.CurrentIntakeMode == ReefscapeIntakeMode.L1)
            {
                // IN L1 MODE: Turn ON (Green texture)
                ApplyLedState(0f, 0.0f, 20f, L1Mode);
                return;
            }

            // --- 3. IDLE / STANDARD DRIVING ---
            // If we aren't auto-aiming and aren't in L1 mode, turn the lights completely OFF!
            TurnLedsOff();
        }

        private void ApplyLedState(float x, float y, float intensity, Texture tex)
        {
            _ledMaterial.SetFloat("_X", x);
            _ledMaterial.SetFloat("_Y", y);
            _ledMaterial.SetFloat("_intensity", intensity);
            if (tex != null) _ledMaterial.SetTexture("_Texture2D", tex);
        }

        private void TurnLedsOff()
        {
            // Setting intensity to 0 kills the emission glow completely, turning the lights off
            _ledMaterial.SetFloat("_intensity", 0f);
        }

        private bool IsRobotAlignedToReef()
        {
            if (_robotBase.TryGetComponent<Rigidbody>(out var rb))
            {
                if (rb.velocity.magnitude > alignmentDistanceTolerance || rb.angularVelocity.magnitude > alignmentAngleTolerance)
                {
                    return false;
                }
            }
            return true;
        }
    }
}