using MoSimCore.Enums;
using RobotFramework.Controllers.GamePieceSystem;
using Games.Reefscape.Enums;
using Games.Reefscape.GamePieceSystem;
using MoSimCore.BaseClasses.GameManagement;
using UnityEngine;
using System.Reflection; 

namespace Prefabs.Reefscape.Robots.Mods.IndianaModpack._1501
{
    public class TeamTHRUSTLEDS : MonoBehaviour
    {
        [Header("LED Components")]
        public GameObject[] leds;
        public Shader shaderGraphShader;
        private Material _ledMaterial;

        [Header("Alliance Settings")]
        [Tooltip("Select which alliance color to default to. Can be flipped in the Inspector for testing!")]
        public Alliance activeAlliance = Alliance.Blue;

        [Header("1501 Custom LED Textures")]
        [Tooltip("Default idle color when playing on the Red Alliance")]
        public Texture RedAllianceDefault;
        
        [Tooltip("Default idle color when playing on the Blue Alliance")]
        public Texture BlueAllianceDefault;
        
        [Tooltip("Color when Algae Mode is toggled on (Green)")]
        public Texture AlgaeMode;
        
        [Tooltip("Color when actively intaking a Coral at the station (White/Blue)")]
        public Texture Intaking;

        [Tooltip("Color when holding a Coral (Purple)")]
        public Texture HasCoral;

        // Internal References
        private RobotGamePieceControllerBase _gamePieceManager;
        private Games.Reefscape.Robots.ReefscapeRobotBase _robotBase;
        private RobotGamePieceController<ReefscapeGamePiece, ReefscapeGamePieceData>.GamePieceControllerNode _coralController;

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
            
            // Connect to 1501's coral controller
            var robotGamePieceController = GetComponent<RobotGamePieceController<ReefscapeGamePiece, ReefscapeGamePieceData>>();
            if (robotGamePieceController != null)
            {
                _coralController = robotGamePieceController.GetPieceByName(ReefscapeGamePieceType.Coral.ToString());
            }

            // Attempt safe auto-detection of alliance without causing compiler errors
            TryAutoDetectAlliance();
        }
        
        private void Update()
        {
            // 1. If disabled or missing components, cleanly fall back to your Alliance Color default!
            if (_ledMaterial == null || _robotBase == null || _gamePieceManager == null || BaseGameManager.Instance.RobotState == RobotState.Disabled)
            {
                ApplyLedState(0f, 0.0f, 20f, GetDefaultTexture());
                return;
            }

            bool hasCoralPiece = _coralController != null && _coralController.HasPiece();
            bool isIntaking = _robotBase.IntakeAction.IsPressed();

            // --- 2. HOLDING CORAL (Highest Priority) ---
            if (hasCoralPiece)
            {
                ApplyLedState(0f, 0.0f, 20f, HasCoral);
                return;
            }

            // --- 3. ALGAE MODE ACTIVE ---
            // Placed above Intaking so that grabbing an algae keeps the lights solid Green!
            if (_robotBase.CurrentRobotMode == ReefscapeRobotMode.Algae)
            {
                ApplyLedState(0f, 0.0f, 20f, AlgaeMode);
                return;
            }

            // --- 4. ACTIVE CORAL INTAKING ---
            // Only triggers when holding intake AND physically at the Intake setpoint in Coral Mode!
            if (isIntaking && _robotBase.CurrentSetpoint == ReefscapeSetpoints.Intake)
            {
                ApplyLedState(0f, 0.0f, 20f, Intaking);
                return;
            }

            // --- 5. IDLE / DEFAULT STATE (Alliance Color) ---
            ApplyLedState(0f, 0.0f, 20f, GetDefaultTexture());
        }

        private void ApplyLedState(float x, float y, float intensity, Texture tex)
        {
            _ledMaterial.SetFloat("_X", x);
            _ledMaterial.SetFloat("_Y", y);
            _ledMaterial.SetFloat("_intensity", intensity);
            if (tex != null) _ledMaterial.SetTexture("_Texture2D", tex);
        }

        private Texture GetDefaultTexture()
        {
            if (activeAlliance == Alliance.Red)
            {
                return RedAllianceDefault != null ? RedAllianceDefault : BlueAllianceDefault;
            }
            return BlueAllianceDefault != null ? BlueAllianceDefault : RedAllianceDefault;
        }

        private void TryAutoDetectAlliance()
        {
            try
            {
                string rootName = transform.root.name.ToLower();
                if (rootName.Contains("red")) { activeAlliance = Alliance.Red; return; }
                if (rootName.Contains("blue")) { activeAlliance = Alliance.Blue; return; }

                if (_robotBase != null)
                {
                    var prop = _robotBase.GetType().GetProperty("Alliance", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                    if (prop != null && prop.PropertyType == typeof(Alliance))
                    {
                        activeAlliance = (Alliance)prop.GetValue(_robotBase);
                    }
                }
            }
            catch
            {
                // Silently fall back to Inspector dropdown if auto-detection fails
            }
        }
    }
}