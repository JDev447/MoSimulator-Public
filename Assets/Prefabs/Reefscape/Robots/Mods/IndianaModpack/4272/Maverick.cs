using System;
using Games.Reefscape.Enums;
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

namespace Prefabs.Reefscape.Robots.Mods.IndianaModpack._4272
{
    public class Maverick: ReefscapeRobotBase
    {
        [Header("Components")]
        [SerializeField] private GenericElevator elevator;
        [SerializeField] private GenericJoint endEffector;
        [SerializeField] private GenericJoint climber;
        
        [Header("PIDS")]
        [SerializeField] private PidConstants endEffectorPid;
        [SerializeField] private PidConstants climberPid;

        [Header("Arm Speeds")]
        [SerializeField] private float regularArmSpeed = 5f;
        [SerializeField] private float l4ArmSpeed = 7f;
        [SerializeField] private float bargeScoreArmSpeed = 3f;

        [Header("Rollers")]
        [SerializeField] private GenericRoller[] intakeRollers;
        [SerializeField] private GenericRoller[] endEffectorRollers;
        [SerializeField] private GenericRoller[] algaeRollers;

        [Header("Roller Speeds")]
        [SerializeField] private float floorIntakeSpeed = 800f;
        [SerializeField] private float eeCoralIntakeSpeed = -800f;
        [SerializeField] private float coralStandardScoreSpeed = -1000f;
        [SerializeField] private float coralL4ScoreSpeed = 1000f;
        [SerializeField] private float algaeIntakeSpeed = 800f;

        [Header("Audio")]
        [SerializeField] private AudioSource intakeAudioSource;
        [SerializeField] private AudioClip intakeClip;
        
        [SerializeField] private AudioSource eeRollerAudioSource;
        [SerializeField] private AudioClip eeRollerClip;
        
        [SerializeField] private AudioSource eePivotAudioSource;
        [SerializeField] private AudioClip eePivotClip;
        
        [SerializeField] private AudioSource climberAudioSource;
        [SerializeField] private AudioClip climberClip;

        [Header("Setpoints")]
        [SerializeField] private MaverickSetpoint stow;
        [SerializeField] private MaverickSetpoint intake;
        [SerializeField] private MaverickSetpoint l1;
        [SerializeField] private MaverickSetpoint l2;
        [SerializeField] private MaverickSetpoint l3;
        [SerializeField] private MaverickSetpoint l4;
        [SerializeField] private MaverickSetpoint lowAlgae;
        [SerializeField] private MaverickSetpoint highAlgae;
        [SerializeField] private MaverickSetpoint barge;
        [SerializeField] private MaverickSetpoint bargeScore;
        [SerializeField] private MaverickSetpoint climb;
        [SerializeField] private MaverickSetpoint climbed;
        
        [Header("Intake Components")]
        [SerializeField] private ReefscapeGamePieceIntake coralIntake;
        [SerializeField] private ReefscapeGamePieceIntake algaeIntake;
        
        [Header("Game Piece States")]
        [SerializeField] private GamePieceState coralStowState;
        [SerializeField] private GamePieceState algaeStowState;

        [Header("Shooting Forces")]
        [SerializeField] private Vector3 standardScoreForce = new Vector3(0, -3f, 6f);
        [SerializeField] private Vector3 l1ScoreForce = new Vector3(0, -1.5f, 3f); 
        [SerializeField] private Vector3 l4ScoreForce = new Vector3(0, 0, 6f);

        // Scoring Timer / Buffer Settings
        [Header("Scoring Settings")]
        [Tooltip("How long (in seconds) the robot holds its scoring position and spins rollers after releasing Outtake.")]
        [SerializeField] private float scoreDelay = 0.1f;

        [Header("Movement Settings")]
        [SerializeField] private float elevatorTolerance = 1.5f;
        [SerializeField] private float angleTolerance = 15f;
        [SerializeField] private float bargeSafeAngleTolerance = 45f;
        [SerializeField] private float safeClearanceHeight = 10f;
        [SerializeField] private float forbiddenBottomAngle = -90f; 
        [SerializeField] private float forbiddenTopAngle = 90f; 
        
        private RobotGamePieceController<ReefscapeGamePiece, ReefscapeGamePieceData>.GamePieceControllerNode _coralController;
        private RobotGamePieceController<ReefscapeGamePiece, ReefscapeGamePieceData>.GamePieceControllerNode _algaeController;

        private float _elevatorTargetHeight;
        private float _endEffectorTargetAngle;
        private float _climberTargetAngle;
        
        // State tracking variables
        private bool _wasArmOut = false;
        private float _lockedSafeHeight = 0f;
        private bool _isBargeSwing = false;
        private bool _isL4Swing = false;
        private float _lastOuttakeTime = -10f;
        
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
            _algaeController = RobotGamePieceController.GetPieceByName(ReefscapeGamePieceType.Algae.ToString());

            _coralController.gamePieceStates = new[] { coralStowState };
            _coralController.intakes.Add(coralIntake);

            _algaeController.gamePieceStates = new[] { algaeStowState };
            _algaeController.intakes.Add(algaeIntake);

            if (intakeAudioSource != null && intakeClip != null) { intakeAudioSource.clip = intakeClip; intakeAudioSource.loop = true; }
            if (eeRollerAudioSource != null && eeRollerClip != null) { eeRollerAudioSource.clip = eeRollerClip; eeRollerAudioSource.loop = true; }
            if (eePivotAudioSource != null && eePivotClip != null) { eePivotAudioSource.clip = eePivotClip; eePivotAudioSource.loop = true; }
            if (climberAudioSource != null && climberClip != null) { climberAudioSource.clip = climberClip; climberAudioSource.loop = true; }
        }

        private void LateUpdate()
        {
            endEffector.UpdatePid(endEffectorPid);
            climber.UpdatePid(climberPid);
        }

        private void FixedUpdate()
        {
            bool hasAlgae = _algaeController.HasPiece();
            bool hasCoral = _coralController.HasPiece();
            
            _algaeController.SetTargetState(algaeStowState);
            _coralController.SetTargetState(coralStowState);

            bool intakePressed = IntakeAction.IsPressed();
            bool outtakePressed = OuttakeAction.IsPressed();
            
            // Track Scoring Buffer Window
            if (outtakePressed)
            {
                _lastOuttakeTime = Time.time;
            }
            bool isScoreBuffering = (Time.time - _lastOuttakeTime) <= scoreDelay;
            
            // Update roller speeds (Keep spinning during score buffering)
            float eeCurrentSpeed = 0f;
            if (intakePressed && !hasCoral) eeCurrentSpeed = eeCoralIntakeSpeed;
            else if (outtakePressed || (CurrentSetpoint == ReefscapeSetpoints.Place && isScoreBuffering))
            {
                if (LastSetpoint == ReefscapeSetpoints.L4 || LastSetpoint == ReefscapeSetpoints.Barge) eeCurrentSpeed = coralL4ScoreSpeed;
                else eeCurrentSpeed = coralStandardScoreSpeed;
            }

            if (endEffectorRollers != null)
                foreach (var roller in endEffectorRollers)
                    if (roller != null) roller.ChangeAngularVelocity(eeCurrentSpeed);

            // Block intake rollers from spinning during Low/High Algae setpoints
            bool isAlgaeSetpoint = CurrentSetpoint == ReefscapeSetpoints.LowAlgae || CurrentSetpoint == ReefscapeSetpoints.HighAlgae;
            float floorIntakeCurrentSpeed = (intakePressed && !hasCoral && !isAlgaeSetpoint) ? floorIntakeSpeed : 0f;
            
            if (intakeRollers != null)
                foreach (var roller in intakeRollers)
                    if (roller != null) roller.ChangeAngularVelocity(floorIntakeCurrentSpeed);

            float algaeCurrentSpeed = 0f;
            if (intakePressed && !hasAlgae) algaeCurrentSpeed = algaeIntakeSpeed;
            else if (outtakePressed || (CurrentSetpoint == ReefscapeSetpoints.Place && isScoreBuffering)) algaeCurrentSpeed = -algaeIntakeSpeed; 

            if (algaeRollers != null)
                foreach (var roller in algaeRollers)
                    if (roller != null) roller.ChangeAngularVelocity(algaeCurrentSpeed);

            // Read physical joint states
            float currentElevatorHeight = elevator.GetElevatorHeight();
            float currentEEAngle = endEffector.GetSingleAxisAngle(JointAxis.Z);
            float distanceToStow = Mathf.Abs(Mathf.DeltaAngle(currentEEAngle, stow.endEffectorAngle));
            bool isArmOut = distanceToStow > angleTolerance;

            // Record peak height for clearance
            if (isArmOut)
            {
                if (!_wasArmOut) _lockedSafeHeight = currentElevatorHeight;
                else _lockedSafeHeight = Mathf.Max(_lockedSafeHeight, currentElevatorHeight);
            }
            _wasArmOut = isArmOut;

            // Arm path tracking
            bool isL4Action = (CurrentSetpoint == ReefscapeSetpoints.L4 && hasCoral);
            bool isBargeAction = (CurrentSetpoint == ReefscapeSetpoints.Barge) || (CurrentSetpoint == ReefscapeSetpoints.L4 && !hasCoral) || (CurrentSetpoint == ReefscapeSetpoints.Place && LastSetpoint == ReefscapeSetpoints.Barge);

            if (isL4Action) _isL4Swing = true;
            if (isBargeAction) _isBargeSwing = true;
            
            if (!isArmOut) 
            {
                if (!isL4Action) _isL4Swing = false;
                if (!isBargeAction) _isBargeSwing = false;
            }

            float activeForbiddenAngle = _isBargeSwing ? forbiddenTopAngle : forbiddenBottomAngle;

            float targetArmSpeed = regularArmSpeed;
            if (_isL4Swing) targetArmSpeed = l4ArmSpeed;
            else if (CurrentSetpoint == ReefscapeSetpoints.Place && _isBargeSwing) targetArmSpeed = bargeScoreArmSpeed;
            endEffectorPid.Max = targetArmSpeed;

            // Setpoint state machine
            switch (CurrentSetpoint)
            {
                case ReefscapeSetpoints.Stow:
                    SetSetpointTargets(stow);
                    // Explicitly ensure intakes shut off the moment we enter stow
                    _coralController.RequestIntake(coralIntake, false);
                    _algaeController.RequestIntake(algaeIntake, false);
                    break;
                case ReefscapeSetpoints.Intake:
                    SetSetpointTargets(intake);
                    _algaeController.RequestIntake(algaeIntake, intakePressed && CurrentRobotMode == ReefscapeRobotMode.Algae && !hasAlgae && !hasCoral);
                    _coralController.RequestIntake(coralIntake, intakePressed && !hasCoral && !hasAlgae);
                    break;
                case ReefscapeSetpoints.L1:
                    SetSetpointTargets(l1);
                    break;
                case ReefscapeSetpoints.L2:
                case ReefscapeSetpoints.LowAlgae:
                    if (hasCoral) SetSetpointTargets(l2);
                    else
                    {
                        SetSetpointTargets(lowAlgae);
                        _algaeController.RequestIntake(algaeIntake, intakePressed && !hasAlgae && !hasCoral);
                    }
                    break;
                case ReefscapeSetpoints.L3:
                case ReefscapeSetpoints.HighAlgae:
                    if (hasCoral) SetSetpointTargets(l3);
                    else
                    {
                        SetSetpointTargets(highAlgae);
                        _algaeController.RequestIntake(algaeIntake, intakePressed && !hasAlgae && !hasCoral);
                    }
                    break;
                case ReefscapeSetpoints.L4:
                case ReefscapeSetpoints.Barge:
                    if (isL4Action)
                    {
                        SetSetpointTargets(l4);
                        if (Mathf.Abs(currentElevatorHeight - l4.elevatorHeight) > elevatorTolerance)
                        {
                            _endEffectorTargetAngle = stow.endEffectorAngle;
                        }
                    }
                    else
                    {
                        SetSetpointTargets(barge);
                    }
                    break;
                case ReefscapeSetpoints.Climb:
                    SetSetpointTargets(climb);
                    break;
                case ReefscapeSetpoints.Climbed:
                    SetSetpointTargets(climbed);
                    break;
                case ReefscapeSetpoints.Place:
                    // Hold position and rollers during scoreBuffering window
                    if (outtakePressed || isScoreBuffering)
                    {
                        PlacePiece();
                        ReturnToLastSetpointTargets(); 
                    }
                    else
                    {
                        SetState(ReefscapeSetpoints.Stow);
                    }
                    break;
            }

            // Downward collision prevention
            float requiredTolerance = _isBargeSwing ? bargeSafeAngleTolerance : angleTolerance;
            bool isMajorDrop = (currentElevatorHeight > safeClearanceHeight && _elevatorTargetHeight < currentElevatorHeight - elevatorTolerance);

            if (isMajorDrop)
            {
                _endEffectorTargetAngle = stow.endEffectorAngle;

                if (distanceToStow > requiredTolerance)
                {
                    _elevatorTargetHeight = Mathf.Max(_elevatorTargetHeight, _lockedSafeHeight);
                }
            }
            
            UpdateJoints(activeForbiddenAngle);
            UpdateAudio(floorIntakeCurrentSpeed, eeCurrentSpeed, currentEEAngle);

            if (!intakePressed)
            {
                _coralController.RequestIntake(coralIntake, false);
                _algaeController.RequestIntake(algaeIntake, false);
            }

            // CORAL STATION DROP DISTANCE LOGIC
            bool isElevatorDown = elevator == null || currentElevatorHeight < safeClearanceHeight;
            bool isArmReady = endEffector == null || Mathf.Abs(Mathf.DeltaAngle(currentEEAngle, _endEffectorTargetAngle)) < 30.0f;
            
            bool canIntakeCoral = (CurrentSetpoint == ReefscapeSetpoints.Stow || CurrentSetpoint == ReefscapeSetpoints.Intake) 
                                  && !hasCoral 
                                  && isElevatorDown 
                                  && isArmReady;

            if (CurrentCoralStationMode != null)
            {
                CurrentCoralStationMode.DropDistance = canIntakeCoral ? 1.55f : 0f;
            }
        }

        private void PlacePiece()
        {
            if (_algaeController.HasPiece())
            {
                if (LastSetpoint == ReefscapeSetpoints.Barge) _algaeController.ReleaseGamePieceWithForce(new Vector3(0, 2, 1.5f));
                else _algaeController.ReleaseGamePieceWithForce(new Vector3(0, 0, 1.5f));
            }
            else if (_coralController.HasPiece())
            {
                if (LastSetpoint == ReefscapeSetpoints.L4) _coralController.ReleaseGamePieceWithForce(l4ScoreForce);
                else if (LastSetpoint == ReefscapeSetpoints.L1) _coralController.ReleaseGamePieceWithForce(l1ScoreForce);
                else _coralController.ReleaseGamePieceWithForce(standardScoreForce);
            }
        }

        private void ReturnToLastSetpointTargets()
        {
            switch (LastSetpoint)
            {
                case ReefscapeSetpoints.L1: SetSetpointTargets(l1); break;
                case ReefscapeSetpoints.L2: SetSetpointTargets(l2); break;
                case ReefscapeSetpoints.L3: SetSetpointTargets(l3); break;
                case ReefscapeSetpoints.L4: SetSetpointTargets(l4); break;
                case ReefscapeSetpoints.Barge: SetSetpointTargets(bargeScore); break; 
                case ReefscapeSetpoints.LowAlgae: SetSetpointTargets(lowAlgae); break;
                case ReefscapeSetpoints.HighAlgae: SetSetpointTargets(highAlgae); break;
                default: SetSetpointTargets(stow); break;
            }
        }

        private void SetSetpointTargets(MaverickSetpoint setpoint)
        {
            if (setpoint == null) return;
            
            _elevatorTargetHeight = setpoint.elevatorHeight;
            _endEffectorTargetAngle = setpoint.endEffectorAngle;
            _climberTargetAngle = setpoint.climberAngle;
        }

        private void UpdateJoints(float forbiddenAngle)
        {
            elevator.SetTarget(_elevatorTargetHeight);
            endEffector.SetTargetAngle(_endEffectorTargetAngle).withAxis(JointAxis.Z).noWrap(forbiddenAngle);
            climber.SetTargetAngle(_climberTargetAngle).withAxis(JointAxis.X);
        }

        private void UpdateAudio(float floorIntakeSpeed, float eeSpeed, float currentEEAngle)
        {
            if (BaseGameManager.Instance.RobotState == RobotState.Disabled)
            {
                ManageAudio(intakeAudioSource, false);
                ManageAudio(eeRollerAudioSource, false);
                ManageAudio(eePivotAudioSource, false);
                ManageAudio(climberAudioSource, false);
                return;
            }

            ManageAudio(intakeAudioSource, Mathf.Abs(floorIntakeSpeed) > 0.1f);
            ManageAudio(eeRollerAudioSource, Mathf.Abs(eeSpeed) > 0.1f);

            bool isPivotMoving = Mathf.Abs(currentEEAngle - _endEffectorTargetAngle) > 2f;
            ManageAudio(eePivotAudioSource, isPivotMoving);

            bool isClimberMoving = Mathf.Abs(climber.GetSingleAxisAngle(JointAxis.X) - _climberTargetAngle) > 2f;
            ManageAudio(climberAudioSource, isClimberMoving);
        }

        private void ManageAudio(AudioSource source, bool shouldPlay)
        {
            if (source == null || source.clip == null) return;

            if (shouldPlay && !source.isPlaying) source.Play();
            else if (!shouldPlay && source.isPlaying) source.Stop();
        }
    }
}