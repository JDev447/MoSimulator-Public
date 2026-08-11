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

namespace Prefabs.Reefscape.Robots.Mods.IndianaModpack._1501
{
    public class TeamTHRUST : ReefscapeRobotBase
    {
        [Header("Robot Components")]
        [SerializeField] private GenericElevator elevator;
        [SerializeField] private GenericJoint endEffector;
        [SerializeField] private GenericJoint algaeManipulator;
        [SerializeField] private GenericJoint climber;
        [SerializeField] private GenericJoint funnel;
        
        [Header("PID Constants")] 
        [SerializeField] private PidConstants endEffectorPid;
        [SerializeField] private PidConstants algaeManipulatorPid;
        [SerializeField] private PidConstants climberPid;
        [SerializeField] private PidConstants funnelPid;

        [Header("Climber Speeds")]
        [Tooltip("The max PID speed for deploying the climber outward.")]
        [SerializeField] private float climberDeploySpeed = 15f;
        [Tooltip("The max PID speed for winching/retracting the climber under load.")]
        [SerializeField] private float climberRetractSpeed = 3f;

        [Header("Rollers")]
        [SerializeField] private GenericRoller[] coralRollers;
        [SerializeField] private GenericRoller[] algaeRollers;

        [Header("Roller Speeds")]
        [SerializeField] private float coralIntakeSpeed = 800f;
        [SerializeField] private float coralOuttakeSpeed = -1000f;
        [SerializeField] private float algaeIntakeSpeed = 800f;
        [SerializeField] private float algaeOuttakeSpeed = -1000f;

        [Header("Audio")]
        [SerializeField] private AudioSource coralIntakeAudio;
        [SerializeField] private AudioClip coralIntakeClip;
        [SerializeField] private AudioSource algaeIntakeAudio;
        [SerializeField] private AudioClip algaeIntakeClip;

        [Header("Movement Settings")]
        [Tooltip("The angle in the middle of your 'Red Zone' that the arm is not allowed to cross.")]
        [SerializeField] private float forbiddenEndEffectorAngle = 180f; 

        [Header("Setpoints")]
        [SerializeField] private THRUSTSetpoint stow;
        [SerializeField] private THRUSTSetpoint stowCoral; 
        [SerializeField] private THRUSTSetpoint intake;
        [SerializeField] private THRUSTSetpoint groundAlgae; 
        [SerializeField] private THRUSTSetpoint l1;
        [SerializeField] private THRUSTSetpoint l2;
        [SerializeField] private THRUSTSetpoint l3;
        [SerializeField] private THRUSTSetpoint l4;
        [SerializeField] private THRUSTSetpoint lowAlgae;
        [SerializeField] private THRUSTSetpoint highAlgae;
        [SerializeField] private THRUSTSetpoint processor;
        [SerializeField] private THRUSTSetpoint climb;
        [SerializeField] private THRUSTSetpoint climbed;

        [Header("Game Piece Logic")]
        [SerializeField] private ReefscapeGamePieceIntake coralIntakeHolder;
        [SerializeField] private ReefscapeGamePieceIntake algaeIntakeHolder;
        [SerializeField] private GamePieceState coralStowState;
        [SerializeField] private GamePieceState algaeStowState;

        // Internal Target Tracking
        private float _elevatorTarget;
        private float _endEffectorTarget;
        private float _algaeManipulatorTarget;
        private float _climberTarget;
        private float _funnelTarget;

        // SUPERCYCLE LATCH & INPUT BUFFER
        private bool _scoredAlgaeThisPress = false;
        private float _lastIntakePressTime = -1f;

        private RobotGamePieceController<ReefscapeGamePiece, ReefscapeGamePieceData>.GamePieceControllerNode _coralController;
        private RobotGamePieceController<ReefscapeGamePiece, ReefscapeGamePieceData>.GamePieceControllerNode _algaeController; 

        protected override void Start()
        {
            base.Start(); 
            
            // Initialize PIDs
            if (endEffector != null) endEffector.SetPid(endEffectorPid);
            if (algaeManipulator != null) algaeManipulator.SetPid(algaeManipulatorPid);
            if (climber != null) climber.SetPid(climberPid);
            if (funnel != null) funnel.SetPid(funnelPid);

            // Game Piece Controller Setup
            RobotGamePieceController.SetPreload(coralStowState);
            
            _coralController = RobotGamePieceController.GetPieceByName(ReefscapeGamePieceType.Coral.ToString());
            _coralController.gamePieceStates = new[] { coralStowState };
            if (coralIntakeHolder != null) _coralController.intakes.Add(coralIntakeHolder);
            
            _algaeController = RobotGamePieceController.GetPieceByName(ReefscapeGamePieceType.Algae.ToString());
            _algaeController.gamePieceStates = new[] { algaeStowState };
            if (algaeIntakeHolder != null) _algaeController.intakes.Add(algaeIntakeHolder);
            
            SetSetpointTargets(stow);

            // Setup Audio Sources
            SetupAudioSource(coralIntakeAudio, coralIntakeClip);
            SetupAudioSource(algaeIntakeAudio, algaeIntakeClip);
        }

        private void SetupAudioSource(AudioSource source, AudioClip clip)
        {
            if (source != null && clip != null)
            {
                source.clip = clip;
                source.loop = true;
                source.playOnAwake = false;
                source.Stop();
            }
        }

        private void LateUpdate()
        {
            if (endEffector != null) endEffector.UpdatePid(endEffectorPid);
            if (algaeManipulator != null) algaeManipulator.UpdatePid(algaeManipulatorPid);
            if (climber != null) climber.UpdatePid(climberPid);
            if (funnel != null) funnel.UpdatePid(funnelPid);
        }

        protected override void Update()
        {
            base.Update();
            // Reset our latch the instant the player lets go of the outtake button
            if (!OuttakeAction.IsPressed())
            {
                _scoredAlgaeThisPress = false;
            }
        }

        private void FixedUpdate()
        {
            bool hasCoral = _coralController != null && _coralController.HasPiece();
            bool hasAlgae = _algaeController != null && _algaeController.HasPiece();
            
            // 0.1 SECOND INPUT BUFFER
            if (IntakeAction.IsPressed())
            {
                _lastIntakePressTime = Time.time;
            }
            bool intakePressed = (Time.time - _lastIntakePressTime) <= 0.15f;
            
            bool outtakePressed = OuttakeAction.IsPressed();

            if (CurrentRobotMode == ReefscapeRobotMode.Algae && hasAlgae)
            {
                SetRobotMode(ReefscapeRobotMode.Coral);
            }

            // ROLLER & SUPERCYCLE PRIORITY LOGIC
            float currentCoralSpeed = 0f;
            float currentAlgaeSpeed = 0f;

            if (outtakePressed)
            {
                // If holding algae OR if we already scored algae during this press,
                // only spin the algae rollers. 
                if (hasAlgae || _scoredAlgaeThisPress)
                {
                    currentAlgaeSpeed = algaeOuttakeSpeed;
                }
                else
                {
                    currentCoralSpeed = coralOuttakeSpeed;
                }
            }
            else if (intakePressed)
            {
                bool isAlgaeSetpoint = (CurrentSetpoint == ReefscapeSetpoints.LowAlgae || 
                                        CurrentSetpoint == ReefscapeSetpoints.HighAlgae || 
                                        CurrentSetpoint == ReefscapeSetpoints.Processor);

                if (!hasAlgae && (CurrentRobotMode == ReefscapeRobotMode.Algae || isAlgaeSetpoint))
                {
                    currentAlgaeSpeed = algaeIntakeSpeed;
                }
                else if (!hasCoral)
                {
                    currentCoralSpeed = coralIntakeSpeed;
                }
            }

            if (coralRollers != null)
                foreach(var roller in coralRollers) 
                    if(roller != null) roller.ChangeAngularVelocity(currentCoralSpeed); 

            if (algaeRollers != null)
                foreach(var roller in algaeRollers)
                    if (roller != null) roller.ChangeAngularVelocity(currentAlgaeSpeed);

            // If rollers are spinning, play sound.
            UpdateAudio(currentCoralSpeed != 0f, currentAlgaeSpeed != 0f);

            // STATE MACHINE
            if (_algaeController != null) _algaeController.SetTargetState(algaeStowState);
            if (_coralController != null) _coralController.SetTargetState(coralStowState);

            switch (CurrentSetpoint)
            {
                case ReefscapeSetpoints.Stow: 
                    SetSetpointTargets(hasCoral ? stowCoral : stow); 
                    break;
                case ReefscapeSetpoints.Intake:
                    if (CurrentRobotMode == ReefscapeRobotMode.Algae)
                    {
                        SetSetpointTargets(groundAlgae);
                        if (_algaeController != null && algaeIntakeHolder != null) 
                            _algaeController.RequestIntake(algaeIntakeHolder, !hasAlgae && intakePressed);
                            
                        if (_coralController != null && coralIntakeHolder != null)
                            _coralController.RequestIntake(coralIntakeHolder, false);
                    }
                    else 
                    {
                        SetSetpointTargets(intake);
                        
                        bool isElevatorAtTarget = elevator != null && Mathf.Abs(elevator.GetElevatorHeight() - _elevatorTarget) < 2.0f;
                        bool isArmAtTarget = endEffector != null && Mathf.Abs(Mathf.DeltaAngle(endEffector.GetSingleAxisAngle(JointAxis.X), _endEffectorTarget)) < 15.0f;
                        bool isAtSetpoint = isElevatorAtTarget && isArmAtTarget;

                        if (_coralController != null && coralIntakeHolder != null) 
                            _coralController.RequestIntake(coralIntakeHolder, !hasCoral && intakePressed && isAtSetpoint);
                    }
                    break;
                case ReefscapeSetpoints.L1: SetSetpointTargets(l1); break;
                case ReefscapeSetpoints.L2: SetSetpointTargets(l2); break;
                case ReefscapeSetpoints.L3: SetSetpointTargets(l3); break;
                case ReefscapeSetpoints.L4: SetSetpointTargets(l4); break;
                case ReefscapeSetpoints.LowAlgae: 
                    SetSetpointTargets(lowAlgae); 
                    if (_algaeController != null && algaeIntakeHolder != null) 
                        _algaeController.RequestIntake(algaeIntakeHolder, !hasAlgae && intakePressed); 
                    break;
                case ReefscapeSetpoints.HighAlgae: 
                    SetSetpointTargets(highAlgae); 
                    if (_algaeController != null && algaeIntakeHolder != null) 
                        _algaeController.RequestIntake(algaeIntakeHolder, !hasAlgae && intakePressed); 
                    break;
                case ReefscapeSetpoints.Processor: SetSetpointTargets(processor); break;
                case ReefscapeSetpoints.Climb: SetSetpointTargets(climb); break;
                case ReefscapeSetpoints.Climbed: SetSetpointTargets(climbed); break;
                case ReefscapeSetpoints.Place: 
                    if (outtakePressed) PlacePiece();
                    else 
                    {
                        if (LastSetpoint != ReefscapeSetpoints.Place) SetState(LastSetpoint);
                        else SetState(ReefscapeSetpoints.Stow);
                    }
                    break;
                default: SetSetpointTargets(stow); break;
            }

            // CLIMBER SPEED LOGIC
            float targetClimberSpeed = climberDeploySpeed;
            if (CurrentSetpoint == ReefscapeSetpoints.Climbed)
            {
                targetClimberSpeed = climberRetractSpeed;
            }
            if (climberPid != null) climberPid.Max = targetClimberSpeed;

            // Apply targets to physical joints safely
            if (elevator != null) elevator.SetTarget(_elevatorTarget);
            if (endEffector != null) endEffector.SetTargetAngle(_endEffectorTarget).withAxis(JointAxis.X).noWrap(forbiddenEndEffectorAngle);
            if (algaeManipulator != null) algaeManipulator.SetTargetAngle(_algaeManipulatorTarget).withAxis(JointAxis.X);
            if (climber != null) climber.SetTargetAngle(_climberTarget).withAxis(JointAxis.Z);
            if (funnel != null) funnel.SetTargetAngle(_funnelTarget).withAxis(JointAxis.X);
            
            // INTAKE SHUTOFF
            if (!intakePressed)
            {
                if (_coralController != null && coralIntakeHolder != null) _coralController.RequestIntake(coralIntakeHolder, false);
                if (_algaeController != null && algaeIntakeHolder != null) _algaeController.RequestIntake(algaeIntakeHolder, false);
            }

            // CORAL STATION DROP DISTANCE LOGIC
            bool isElevatorReady = elevator != null && Mathf.Abs(elevator.GetElevatorHeight() - _elevatorTarget) < 2.0f;
            bool isArmReady = endEffector != null && Mathf.Abs(Mathf.DeltaAngle(endEffector.GetSingleAxisAngle(JointAxis.X), _endEffectorTarget)) < 15.0f;
            
            bool canIntakeCoral = (CurrentRobotMode == ReefscapeRobotMode.Coral) 
                                  && (CurrentSetpoint == ReefscapeSetpoints.Intake) 
                                  && !hasCoral 
                                  && isElevatorReady 
                                  && isArmReady;

            if (CurrentCoralStationMode != null)
            {
                CurrentCoralStationMode.DropDistance = canIntakeCoral ? 1.35f : 0f;
            }
        }

        private void UpdateAudio(bool coralRollersSpinning, bool algaeRollersSpinning)
        {
            if (BaseGameManager.Instance.RobotState == RobotState.Disabled)
            {
                if (coralIntakeAudio != null) coralIntakeAudio.Stop();
                if (algaeIntakeAudio != null) algaeIntakeAudio.Stop();
                return;
            }

            if (coralIntakeAudio != null)
            {
                if (coralRollersSpinning && !coralIntakeAudio.isPlaying) coralIntakeAudio.Play();
                else if (!coralRollersSpinning && coralIntakeAudio.isPlaying) coralIntakeAudio.Stop();
            }

            if (algaeIntakeAudio != null)
            {
                if (algaeRollersSpinning && !algaeIntakeAudio.isPlaying) algaeIntakeAudio.Play();
                else if (!algaeRollersSpinning && algaeIntakeAudio.isPlaying) algaeIntakeAudio.Stop();
            }
        }

        private void SetSetpointTargets(THRUSTSetpoint setpoint)
        {
            if (setpoint == null) return;
            
            _elevatorTarget = setpoint.elevatorHeight;
            _endEffectorTarget = setpoint.endEffectorAngle;
            _algaeManipulatorTarget = setpoint.algaeManipulatorAngle;
            _climberTarget = setpoint.climberAngle;
            _funnelTarget = setpoint.funnelAngle;
        }

        private void PlacePiece()
        {
            if (_algaeController != null && _algaeController.HasPiece())
            {
                 _algaeController.ReleaseGamePieceWithForce(new Vector3(0, 0, 2f));
                 _scoredAlgaeThisPress = true; // blocks coral release until outtake button is released.
            }
            else if (_coralController != null && _coralController.HasPiece() && !_scoredAlgaeThisPress)
            {
                if (LastSetpoint == ReefscapeSetpoints.L4) 
                    _coralController.ReleaseGamePieceWithForce(new Vector3(0, 0f, 5f));
                else if (LastSetpoint == ReefscapeSetpoints.L1) 
                    _coralController.ReleaseGamePieceWithForce(new Vector3(0, 0, 1f));
                else 
                    _coralController.ReleaseGamePieceWithForce(new Vector3(0, 0f, 7f));
            }
        }
    }
}