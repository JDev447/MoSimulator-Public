using MoSimCore.Enums;
using RobotFramework.Controllers.GamePieceSystem;
using Games.Reefscape.Enums;
using Games.Reefscape.GamePieceSystem;
using MoSimCore.BaseClasses.GameManagement;
using UnityEngine;

namespace Prefabs.Reefscape.Robots.Mods.IndianaModpack._868
{
    public class _868LEDs : MonoBehaviour
    {
        [Header("LED Sections")]
        public GameObject[] centerLeds;
        public GameObject[] l1Leds;
        public GameObject[] l2Leds;
        public GameObject[] l3Leds;
        public GameObject[] l4Leds;

        [Header("Materials & Textures")]
        public Shader shaderGraphShader;
        [Tooltip("Assign solid color textures here")]
        public Texture texWhite;
        public Texture texGreen;
        public Texture texBlue;
        public Texture texOrange;

        [Header("Auto-Align Custom Lighting")]
        [Tooltip("Distance in meters to consider the robot 'aligned' to the reef node.")]
        [SerializeField] private float alignmentDistanceTolerance = 0.15f; 
        [Tooltip("Angle in degrees to consider the robot 'aligned' to the reef node.")]
        [SerializeField] private float alignmentAngleTolerance = 3.0f;

        private Material _centerMat;
        private Material _l1Mat;
        private Material _l2Mat;
        private Material _l3Mat;
        private Material _l4Mat;

        private RobotGamePieceControllerBase _gamePieceManager;
        private Games.Reefscape.Robots.ReefscapeRobotBase _robotBase;
        private RobotGamePieceController<ReefscapeGamePiece, ReefscapeGamePieceData>.GamePieceControllerNode _coralController;
        private RobotGamePieceController<ReefscapeGamePiece, ReefscapeGamePieceData>.GamePieceControllerNode _algaeController;

        private void Start()
        {
            _gamePieceManager = GetComponent<RobotGamePieceControllerBase>();
            _robotBase = GetComponent<Games.Reefscape.Robots.ReefscapeRobotBase>();
            
            var rgpController = GetComponent<RobotGamePieceController<ReefscapeGamePiece, ReefscapeGamePieceData>>();
            if (rgpController != null)
            {
                _coralController = rgpController.GetPieceByName(ReefscapeGamePieceType.Coral.ToString());
                _algaeController = rgpController.GetPieceByName(ReefscapeGamePieceType.Algae.ToString());
            }
            
            if (shaderGraphShader != null)
            {
                _centerMat = CreateMaterialForZone(centerLeds);
                _l1Mat = CreateMaterialForZone(l1Leds);
                _l2Mat = CreateMaterialForZone(l2Leds);
                _l3Mat = CreateMaterialForZone(l3Leds);
                _l4Mat = CreateMaterialForZone(l4Leds);
            }
        }

        private Material CreateMaterialForZone(GameObject[] ledArray)
        {
            Material newMat = new Material(shaderGraphShader);
            foreach (var led in ledArray)
            {
                if (led != null && led.TryGetComponent<Renderer>(out var ren))
                {
                    ren.material = newMat;
                }
            }
            return newMat;
        }
        
        private void Update()
        {
            if (_robotBase == null || _gamePieceManager == null || BaseGameManager.Instance.RobotState == RobotState.Disabled)
            {
                TurnOffAllLeds();
                return;
            }

            bool hasCoral = _coralController != null && _coralController.HasPiece();
            bool hasAlgae = _algaeController != null && _algaeController.HasPiece();
            bool isAutoAligning = _robotBase.AutoAlignLeftAction.IsPressed() || _robotBase.AutoAlignRightAction.IsPressed();
            bool isAligned = IsRobotAlignedToReef();
            bool isFlashingOn = (Time.time % 0.2f) < 0.1f; 
            
            // Only consider it "scoring" if we actually have a piece to shoot
            bool isScoring = _robotBase.OuttakeAction.IsPressed() && (hasCoral || hasAlgae);

            // Default states for this frame (Off / White Texture)
            Texture cTex = texWhite, l1Tex = texWhite, l2Tex = texWhite, l3Tex = texWhite, l4Tex = texWhite;
            float cInt = 0f, l1Int = 0f, l2Int = 0f, l3Int = 0f, l4Int = 0f;

            // --- PRIORITY 1: SCORING OR ALIGNED (ALL GREEN) ---
            if (isScoring || (isAutoAligning && isAligned))
            {
                cTex = l1Tex = l2Tex = l3Tex = l4Tex = texGreen;
                cInt = l1Int = l2Int = l3Int = l4Int = 20f;
            }
            // --- PRIORITY 2: READY FOR CORAL (ALL ORANGE) ---
            else if (!hasCoral && !hasAlgae && _robotBase.CurrentRobotMode == ReefscapeRobotMode.Coral && (_robotBase.CurrentSetpoint == ReefscapeSetpoints.Stow || _robotBase.CurrentSetpoint == ReefscapeSetpoints.Intake))
            {
                cTex = l1Tex = l2Tex = l3Tex = l4Tex = texOrange;
                cInt = l1Int = l2Int = l3Int = l4Int = 20f;
            }
            // --- PRIORITY 3: STANDARD OPERATIONS ---
            else
            {
                bool isAlgaeSetpoint = _robotBase.CurrentSetpoint == ReefscapeSetpoints.LowAlgae || _robotBase.CurrentSetpoint == ReefscapeSetpoints.HighAlgae;

                // Center LED Game Piece Flashing & Algae Mode
                if (hasCoral)
                {
                    cTex = texWhite;
                    cInt = isFlashingOn ? 20f : 0f;
                }
                else if (hasAlgae)
                {
                    cTex = texGreen;
                    cInt = isFlashingOn ? 20f : 0f;
                }
                else if (_robotBase.CurrentRobotMode == ReefscapeRobotMode.Algae || isAlgaeSetpoint)
                {
                    cTex = texGreen;
                    cInt = 20f;
                }

                // Level LED Targeting
                switch (_robotBase.CurrentSetpoint)
                {
                    case ReefscapeSetpoints.L1: l1Tex = texBlue; l1Int = 20f; break;
                    case ReefscapeSetpoints.L2: l2Tex = texBlue; l2Int = 20f; break;
                    case ReefscapeSetpoints.L3: l3Tex = texBlue; l3Int = 20f; break;
                    case ReefscapeSetpoints.L4: l4Tex = texBlue; l4Int = 20f; break;
                }
            }

            // Apply calculated states to the materials
            ApplyLedState(_centerMat, cInt, cTex);
            ApplyLedState(_l1Mat, l1Int, l1Tex);
            ApplyLedState(_l2Mat, l2Int, l2Tex);
            ApplyLedState(_l3Mat, l3Int, l3Tex);
            ApplyLedState(_l4Mat, l4Int, l4Tex);
        }

        private void ApplyLedState(Material mat, float intensity, Texture tex)
        {
            if (mat == null) return;
            
            // If the light is turning off, force the texture to white to prevent color ghosting
            if (intensity <= 0f)
            {
                tex = texWhite;
            }
            
            mat.SetFloat("_intensity", intensity);
            if (tex != null) mat.SetTexture("_Texture2D", tex);
        }

        private void TurnOffAllLeds()
        {
            ApplyLedState(_centerMat, 0f, texWhite);
            ApplyLedState(_l1Mat, 0f, texWhite);
            ApplyLedState(_l2Mat, 0f, texWhite);
            ApplyLedState(_l3Mat, 0f, texWhite);
            ApplyLedState(_l4Mat, 0f, texWhite);
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