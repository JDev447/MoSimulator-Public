using System;
using Games.Reefscape.Enums;
using Games.Reefscape.FieldScripts;
using Games.Reefscape.GamePieceSystem;
using Games.Reefscape.Robots;
using MoSimCore.BaseClasses.GameManagement;
using MoSimCore.Enums;
using MoSimLib;
using RobotFramework.Components;
using RobotFramework.Controllers.GamePieceSystem;
using RobotFramework.Controllers.PidSystems;
using RobotFramework.Enums;
using RobotFramework.GamePieceSystem;
using UnityEngine;

namespace Prefabs.Reefscape.Robots.Mods.IndianaModpack._868
{
    public class TechHOUNDS : ReefscapeRobotBase
    {
        [Header("Robot Components")]
        [SerializeField] private GenericElevator elevator;
        [SerializeField] private GenericJoint endEffector;
        [SerializeField] private GenericJoint climber;
        
        [Header("PID Constants")] 
        [SerializeField] private PidConstants endEffectorPid;
        [SerializeField] private PidConstants climberPid;
        
        [Header("Robot Setpoints")]
        [SerializeField] private TechHOUNDSSetpoint barge;
        [SerializeField] private TechHOUNDSSetpoint climbed;
        [SerializeField] private TechHOUNDSSetpoint climbPrep;
        [SerializeField] private TechHOUNDSSetpoint groundAlgae;
        [SerializeField] private TechHOUNDSSetpoint highAlgae;
        [SerializeField] private TechHOUNDSSetpoint intake;
        [SerializeField] private TechHOUNDSSetpoint l1;
        [SerializeField] private TechHOUNDSSetpoint l2;
        [SerializeField] private TechHOUNDSSetpoint l3;
        [SerializeField] private TechHOUNDSSetpoint l4;
        [SerializeField] private TechHOUNDSSetpoint l4Score;
        [SerializeField] private TechHOUNDSSetpoint lowAlgae;
        [SerializeField] private TechHOUNDSSetpoint processor;
        [SerializeField] private TechHOUNDSSetpoint stow;
        [SerializeField] private TechHOUNDSSetpoint algaeStow;

        [Header("Game Piece Logic")]
        [SerializeField] private ReefscapeGamePieceIntake coralIntake;
        [SerializeField] private GamePieceState coralStowState;
        
        [SerializeField] private ReefscapeGamePieceIntake algaeIntake;
        [SerializeField] private GamePieceState algaeStowState;

        [Header("Rollers")]
        [SerializeField] private GenericRoller[] endEffectorRollers;
        
        [Header("Roller Speeds")]
        [SerializeField] private float rollerIntakeSpeed = 800f;
        [SerializeField] private float rollerOuttakeSpeed = -1000f;
        [SerializeField] private float rollerL1OuttakeSpeed = -400f; 

        // Scoring Forces
        [Header("Scoring Forces")]
        [Tooltip("The Z-axis force applied when scoring Coral at standard setpoints (L2, L3, L4).")]
        [SerializeField] private float coralStandardScoreForce = 15f;
        [Tooltip("The Z-axis force applied when scoring Coral at L1.")]
        [SerializeField] private float coralL1ScoreForce = 3f;
        [Tooltip("The Z-axis force applied when scoring Algae.")]
        [SerializeField] private float algaeScoreForce = 1f;

        [Header("Audio")]
        [SerializeField] private AudioSource rollerAudioSource;
        [SerializeField] private AudioClip rollerAudioClip;

        [Header("Movement Settings")]
        [SerializeField] private float elevatorTolerance = 1.5f;
        [SerializeField] private float angleTolerance = 15f;
        [SerializeField] private float safeClearanceHeight = 10f;
        [Tooltip("The angle pointing into the 'Red Zone' that the arm is never allowed to cross.")]
        [SerializeField] private float forbiddenAngle = -90f; 

        [Header("Auto Align Settings")]
        [Tooltip("The Z-distance the robot holds at before moving in.")]
        [SerializeField] private float alignHoldDistance = -5f;
        [Tooltip("The Z-distance the robot pushes forward to when scoring.")]
        [SerializeField] private float alignScoreDistance = 1f; 
        [Tooltip("Minimum elevator height required before pushing forward for L2.")]
        [SerializeField] private float l2MinAlignHeight = 10f;
        [Tooltip("Minimum elevator height required before pushing forward for L3.")]
        [SerializeField] private float l3MinAlignHeight = 25f;
        [Tooltip("Minimum elevator height required before pushing forward for L4.")]
        [SerializeField] private float l4MinAlignHeight = 45f;

        private RobotGamePieceController<ReefscapeGamePiece, ReefscapeGamePieceData>.GamePieceControllerNode _coralController;
        private RobotGamePieceController<ReefscapeGamePiece, ReefscapeGamePieceData>.GamePieceControllerNode _algaeController;
        
        private ReefscapeAutoAlign _align;
        private bool _isPreAligned = false;

        private float _elevatorTargetHeight;
        private float _endEffectorTargetAngle;
        private float _climberTargetAngle;
        
        private bool _isL4Swing = false;

        protected override void Start()
        {
            base.Start(); 
            
            endEffector.SetPid(endEffectorPid);
            climber.SetPid(climberPid);

            _elevatorTargetHeight = 0;
            _endEffectorTargetAngle = 0;
            _climberTargetAngle = 0;

            RobotGamePieceController.SetPreload(coralStowState);
            _coralController = RobotGamePieceController.GetPieceByName(ReefscapeGamePieceType.Coral.ToString());
            _coralController.gamePieceStates = new[] { coralStowState };
            _coralController.intakes.Add(coralIntake);
            
            _algaeController = RobotGamePieceController.GetPieceByName(ReefscapeGamePieceType.Algae.ToString());
            _algaeController.gamePieceStates = new[] { algaeStowState };
            _algaeController.intakes.Add(algaeIntake);

            _align = gameObject.GetComponent<ReefscapeAutoAlign>();

            if (rollerAudioSource != null && rollerAudioClip != null)
            {
                rollerAudioSource.clip = rollerAudioClip;
                rollerAudioSource.loop = true;
                rollerAudioSource.playOnAwake = false;
                rollerAudioSource.Stop();
            }
        }

        private void LateUpdate()
        {
            endEffector.UpdatePid(endEffectorPid);
            climber.UpdatePid(climberPid);
        }

        private void FixedUpdate()
        {
            bool hasCoral = _coralController.HasPiece();
            bool hasAlgae = _algaeController.HasPiece();
            
            bool intakePressed = IntakeAction.IsPressed();
            bool outtakePressed = OuttakeAction.IsPressed();

            TechHOUNDSSetpoint currentStow = (hasAlgae && algaeStow != null) ? algaeStow : stow;

            if (CurrentRobotMode == ReefscapeRobotMode.Algae && hasAlgae)
            {
                SetRobotMode(ReefscapeRobotMode.Coral);
            }
            
            _coralController.SetTargetState(coralStowState);
            _algaeController.SetTargetState(algaeStowState);

            float currentRollerSpeed = 0f;
            
            if (intakePressed && !hasCoral && !hasAlgae) 
            {
                currentRollerSpeed = rollerIntakeSpeed;
            }
            else if (outtakePressed) 
            {
                if (LastSetpoint == ReefscapeSetpoints.L1)
                {
                    currentRollerSpeed = rollerL1OuttakeSpeed;
                }
                else
                {
                    currentRollerSpeed = rollerOuttakeSpeed;
                }
            }

            if (endEffectorRollers != null)
            {
                foreach (var roller in endEffectorRollers)
                {
                    if (roller != null) roller.ChangeAngularVelocity(currentRollerSpeed);
                }
            }

            UpdateAudio(currentRollerSpeed);

            float currentElevatorHeight = elevator.GetElevatorHeight();
            float currentEEAngle = endEffector.GetSingleAxisAngle(JointAxis.X); 
            
            bool isArmOut = Mathf.Abs(Mathf.DeltaAngle(currentEEAngle, currentStow.endEffectorAngle)) > angleTolerance;

            bool isL4Action = (CurrentSetpoint == ReefscapeSetpoints.L4);

            if (isL4Action) _isL4Swing = true;
            
            if (!isArmOut) 
            {
                if (!isL4Action) _isL4Swing = false;
            }

            UpdateAutoAlignOffsets(currentElevatorHeight);

            switch (CurrentSetpoint)
            {
                case ReefscapeSetpoints.Stow: 
                    SetSetpointTargets(currentStow); 
                    break;
                case ReefscapeSetpoints.Intake:
                    if (hasAlgae)
                    {
                        SetSetpointTargets(currentStow);
                        _coralController.RequestIntake(coralIntake, false);
                    }
                    else if (CurrentRobotMode == ReefscapeRobotMode.Algae)
                    {
                        SetSetpointTargets(groundAlgae);
                        _algaeController.RequestIntake(algaeIntake, !hasAlgae && !hasCoral && intakePressed);
                        
                        // Explicitly disable coral intake in Algae mode
                        _coralController.RequestIntake(coralIntake, false);
                    }
                    else 
                    {
                        SetSetpointTargets(intake);
                        
                        // Check if mechanisms are physically at the intake position
                        bool isElevatorAtTarget = elevator != null && Mathf.Abs(currentElevatorHeight - _elevatorTargetHeight) < 2.0f;
                        bool isArmAtTarget = endEffector != null && Mathf.Abs(Mathf.DeltaAngle(currentEEAngle, _endEffectorTargetAngle)) < 15.0f;
                        bool isAtSetpoint = isElevatorAtTarget && isArmAtTarget;

                        _coralController.RequestIntake(coralIntake, !hasCoral && !hasAlgae && intakePressed && isAtSetpoint);
                    }
                    break;
                case ReefscapeSetpoints.L1:
                    SetSetpointTargets(l1);
                    break;
                case ReefscapeSetpoints.L2:
                    SetSetpointTargets(l2);
                    break;
                case ReefscapeSetpoints.L3:
                    SetSetpointTargets(l3);
                    break;
                case ReefscapeSetpoints.L4:
                    SetSetpointTargets(l4);
                    break;
                case ReefscapeSetpoints.LowAlgae:
                    SetSetpointTargets(lowAlgae);
                    _algaeController.RequestIntake(algaeIntake, !hasAlgae && !hasCoral && intakePressed);
                    break;
                case ReefscapeSetpoints.HighAlgae:
                    SetSetpointTargets(highAlgae);
                    _algaeController.RequestIntake(algaeIntake, !hasAlgae && !hasCoral && intakePressed);
                    break;
                case ReefscapeSetpoints.Processor:
                    SetSetpointTargets(processor);
                    break;
                case ReefscapeSetpoints.Barge:
                    SetSetpointTargets(barge);
                    break;
                case ReefscapeSetpoints.Climb:
                    SetSetpointTargets(climbPrep);
                    break;
                case ReefscapeSetpoints.Climbed:
                    SetSetpointTargets(climbed);
                    break;
                case ReefscapeSetpoints.Place:
                    if (LastSetpoint == ReefscapeSetpoints.L4)
                    {
                        SetSetpointTargets(l4);
                    }
                    
                    if (outtakePressed) 
                    {
                        PlacePiece();
                    }
                    else 
                    {
                        SetState(ReefscapeSetpoints.Stow);
                    }
                    break;
            }

            bool isGoingUp = _elevatorTargetHeight > currentElevatorHeight + elevatorTolerance;
            bool isArmAtTargetFinal = Mathf.Abs(Mathf.DeltaAngle(currentEEAngle, _endEffectorTargetAngle)) <= angleTolerance;

            if (isGoingUp && !isArmAtTargetFinal)
            {
                _elevatorTargetHeight = currentStow.elevatorHeight; 
            }

            bool isArmTargetStow = Mathf.Abs(Mathf.DeltaAngle(_endEffectorTargetAngle, currentStow.endEffectorAngle)) <= 5f;
            
            if (isArmTargetStow && currentElevatorHeight > safeClearanceHeight)
            {
                _endEffectorTargetAngle = _isL4Swing ? l4Score.endEffectorAngle : currentEEAngle;
            }

            UpdateJoints();
            
            if (!intakePressed)
            {
                _coralController.RequestIntake(coralIntake, false);
                _algaeController.RequestIntake(algaeIntake, false);
            }

            // CORAL STATION DROP DISTANCE LOGIC
            bool isElevatorReady = elevator != null && Mathf.Abs(currentElevatorHeight - _elevatorTargetHeight) < 2.0f;
            bool isArmReady = endEffector != null && Mathf.Abs(Mathf.DeltaAngle(currentEEAngle, _endEffectorTargetAngle)) < 15.0f;
            
            bool canIntakeCoral = (CurrentRobotMode == ReefscapeRobotMode.Coral) 
                                  && (CurrentSetpoint == ReefscapeSetpoints.Intake) 
                                  && !hasCoral 
                                  && !hasAlgae
                                  && isElevatorReady 
                                  && isArmReady;

            if (CurrentCoralStationMode != null)
            {
                CurrentCoralStationMode.DropDistance = canIntakeCoral ? 1.55f : 0f;
            }
        }

        private void UpdateAutoAlignOffsets(float currentElevatorHeight)
        {
            if (_align == null) return;

            if (!AutoAlignLeftAction.IsPressed() && !AutoAlignRightAction.IsPressed())
            {
                _isPreAligned = false;
                return;
            }

            float requiredHeight = 0f;
            if (CurrentSetpoint == ReefscapeSetpoints.L2) requiredHeight = l2MinAlignHeight;
            else if (CurrentSetpoint == ReefscapeSetpoints.L3) requiredHeight = l3MinAlignHeight;
            else if (CurrentSetpoint == ReefscapeSetpoints.L4) requiredHeight = l4MinAlignHeight;

            if (!_isPreAligned)
            {
                if (currentElevatorHeight >= requiredHeight)
                {
                    _isPreAligned = true;
                }
            }

            float zOffset = _isPreAligned ? alignScoreDistance : alignHoldDistance;
            _align.offset = new Vector3(0, 0, zOffset);
        }

        private void SetSetpointTargets(TechHOUNDSSetpoint setpoint)
        {
            if (setpoint == null) return; 
            
            _elevatorTargetHeight = setpoint.elevatorHeight;
            _endEffectorTargetAngle = setpoint.endEffectorAngle;
            _climberTargetAngle = setpoint.climberAngle;
        }

        private void UpdateJoints()
        {
            elevator.SetTarget(_elevatorTargetHeight);
            endEffector.SetTargetAngle(_endEffectorTargetAngle).withAxis(JointAxis.X).noWrap(forbiddenAngle);
            climber.SetTargetAngle(_climberTargetAngle).withAxis(JointAxis.Z);
        }

        private void PlacePiece()
        {
            // Adjusted Scoring Force Logic
            if (_algaeController.HasPiece())
            {
                _algaeController.ReleaseGamePieceWithForce(new Vector3(0, 0, algaeScoreForce));
            }
            else if (_coralController.HasPiece())
            {
                if (LastSetpoint == ReefscapeSetpoints.L1)
                {
                    _coralController.ReleaseGamePieceWithForce(new Vector3(0, 0, coralL1ScoreForce));
                }
                else
                {
                    _coralController.ReleaseGamePieceWithForce(new Vector3(0, 0, coralStandardScoreForce));
                }
            }
        }

        private void UpdateAudio(float currentRollerSpeed)
        {
            if (BaseGameManager.Instance.RobotState == RobotState.Disabled)
            {
                if (rollerAudioSource != null && rollerAudioSource.isPlaying)
                {
                    rollerAudioSource.Stop();
                }
                return;
            }

            if (rollerAudioSource != null)
            {
                bool isSpinning = Mathf.Abs(currentRollerSpeed) > 0.1f;

                if (isSpinning && !rollerAudioSource.isPlaying)
                {
                    rollerAudioSource.Play();
                }
                else if (!isSpinning && rollerAudioSource.isPlaying)
                {
                    rollerAudioSource.Stop();
                }
            }
        }
    }
}