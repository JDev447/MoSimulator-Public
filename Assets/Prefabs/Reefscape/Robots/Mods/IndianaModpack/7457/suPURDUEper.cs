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

namespace Prefabs.Reefscape.Robots.Mods.IndianaModpack._7457
{
    public class suPURDUEper : ReefscapeRobotBase
    {
        [Header("Robot Components")]
        [SerializeField] private GenericElevator elevator;
        [SerializeField] private GenericJoint intake;
        [SerializeField] private GenericJoint endEffector;
        [SerializeField] private GenericJoint climber;
        
        [SerializeField] private suPURDUEperClimber climberController; 
        
        [Header("PID Constants")] 
        [SerializeField] private PidConstants intakePid;
        [SerializeField] private PidConstants endEffectorPid;
        [SerializeField] private PidConstants climberPid;
        
        [Header("Active Rollers")]
        [SerializeField] private GenericRoller[] intakeRollers;
        [SerializeField] private GenericRoller[] l1IntakeRollers;
        [SerializeField] private GenericRoller[] climberRollers;
        [SerializeField] private GenericRoller[] endEffectorRollers;
        [SerializeField] private GenericRoller[] algaeRollers;

        [Header("Roller Speeds")]
        [SerializeField] private float floorIntakeSpeed = 800f;
        [SerializeField] private float l1IntakeSpeed = 800f;
        [SerializeField] private float l1OuttakeSpeed = -1000f;
        [SerializeField] private float eeOuttakeSpeed = 1000f;
        [SerializeField] private float climberSpeed = 3000f;
        
        [Tooltip("Test speeds for algae roller animation")]
        [SerializeField] private float algaeIdleSpeed = 150f;
        [SerializeField] private float algaeActiveSpeed = 350f;
        [SerializeField] private float algaeScoreSpeed = -350f;

        [Header("Audio")]
        [SerializeField] private AudioSource intakeAudio;
        [SerializeField] private AudioClip intakeClip;
        [SerializeField] private AudioSource algaeStallAudio; 
        [SerializeField] private AudioClip algaeStallClip;    // 1986Stall
        [SerializeField] private AudioSource climberAudio; 
        [SerializeField] private AudioClip climberClip;

        [Header("Sensors")]
        [Tooltip("The Trigger Script on the front of the robot (Set to 'Algae' mode)")]
        [SerializeField] private suPURDUEperClimberTrigger algaeSensor;

        [Header("Movement Settings")]
        [Tooltip("The elevator height (in inches) required before the end effector is allowed to rotate out of stow when going to L4 or Barge.")]
        [SerializeField] private float safeClearanceHeight = 10f;

        [Header("Robot Setpoints")]
        [SerializeField] private suPURDUEperSetpoint stow;
        [SerializeField] private suPURDUEperSetpoint intakePos;
        [SerializeField] private suPURDUEperSetpoint l1Intake; 
        [SerializeField] private suPURDUEperSetpoint l1Score;  
        [SerializeField] private suPURDUEperSetpoint l1; 
        
        [Header("Smart Setpoints")]
        [SerializeField] private suPURDUEperSetpoint l2; // Standard (Trade)
        [SerializeField] private suPURDUEperSetpoint l2Optimized; // Fast Coral Score
        [SerializeField] private suPURDUEperSetpoint l3; // Standard (Trade)
        [SerializeField] private suPURDUEperSetpoint l3Optimized; // Fast Coral Score

        [SerializeField] private suPURDUEperSetpoint l4;
        [SerializeField] private suPURDUEperSetpoint l4Score;
        [SerializeField] private suPURDUEperSetpoint lowAlgae; 
        [SerializeField] private suPURDUEperSetpoint highAlgae;
        [SerializeField] private suPURDUEperSetpoint barge;
        [SerializeField] private suPURDUEperSetpoint climbDeployed;
        [SerializeField] private suPURDUEperSetpoint climbed;
        [SerializeField] private suPURDUEperSetpoint lollipop; 

        [Header("Game Piece Logic")]
        [SerializeField] private ReefscapeGamePieceIntake endEffectorHolder;
        [SerializeField] private ReefscapeGamePieceIntake algaeIntake;
        [SerializeField] private ReefscapeGamePieceIntake l1GamePieceIntake; 

        [SerializeField] private GamePieceState coralStowState;
        [SerializeField] private GamePieceState algaeStowState; 
        [SerializeField] private GamePieceState l1CoralStow; 

        // --- FLAGS ---
        public bool _releaseDetected = false; 
        private bool _wasPressedLastFrame = false;
        private bool _isFreshPress = false;
        private bool _algaeMode = false;
        private ReefscapeSetpoints? bufferedSetpoint;

        // Sticky Flag for Algae Logic
        private bool _stickyAlgaeMode = false;

        private RobotGamePieceController<ReefscapeGamePiece, ReefscapeGamePieceData>.GamePieceControllerNode _coralController;
        private RobotGamePieceController<ReefscapeGamePiece, ReefscapeGamePieceData>.GamePieceControllerNode _algaeController; 

        // --- TARGET TRACKING ---
        private float _elevatorTargetHeight;
        private float _endEffectorTargetAngle;

        protected override void Start()
        {
            base.Start(); 
            intake.SetPid(intakePid);
            endEffector.SetPid(endEffectorPid);
            climber.SetPid(climberPid);

            RobotGamePieceController.SetPreload(coralStowState);
            _coralController = RobotGamePieceController.GetPieceByName(ReefscapeGamePieceType.Coral.ToString());
            _coralController.gamePieceStates = new[] { coralStowState, l1CoralStow };
            _coralController.intakes.Add(endEffectorHolder);
            if (l1GamePieceIntake != null && l1GamePieceIntake != endEffectorHolder) _coralController.intakes.Add(l1GamePieceIntake);
            
            _algaeController = RobotGamePieceController.GetPieceByName(ReefscapeGamePieceType.Algae.ToString());
            _algaeController.gamePieceStates = new[] { algaeStowState };
            _algaeController.intakes.Add(algaeIntake);
            
            SetRobotTargets(stow);
            SetupAudioSource(intakeAudio, intakeClip);
            SetupAudioSource(algaeStallAudio, algaeStallClip);
            SetupAudioSource(climberAudio, climberClip);
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

        private bool IsAlgaeInFront()
        {
            return algaeSensor != null && algaeSensor.HasDetectedObject;
        }

        private void LateUpdate()
        {
            intake.UpdatePid(intakePid);
            endEffector.UpdatePid(endEffectorPid);
            climber.UpdatePid(climberPid);
        }

        protected override void Update()
        {
            base.Update(); 
            bool isPressed = OuttakeAction.IsPressed();
            if (_wasPressedLastFrame && !isPressed) _releaseDetected = true; 
            if (isPressed && !_wasPressedLastFrame) { _isFreshPress = true; _releaseDetected = false; } else { _isFreshPress = false; }
            _wasPressedLastFrame = isPressed;
        }

        private void FixedUpdate()
        {
            bool hasCoral = _coralController.HasPiece();
            bool hasAlgae = _algaeController.HasPiece();
            bool intakePressed = IntakeAction.IsPressed(); 
            bool outtakePressed = OuttakeAction.IsPressed();

            // --- STICKY FLAG RESET ---
            if (CurrentSetpoint != ReefscapeSetpoints.L2 && CurrentSetpoint != ReefscapeSetpoints.L3)
            {
                _stickyAlgaeMode = false;
            }

            if (CurrentRobotMode == ReefscapeRobotMode.Algae && hasAlgae)
            {
                SetRobotMode(ReefscapeRobotMode.Coral);
            }

            // --- STATE HELPERS FOR ROLLER & AUDIO LOGIC ---
            bool isL1Mode = (CurrentIntakeMode == ReefscapeIntakeMode.L1);
            bool isScoringAlgae = outtakePressed && (_algaeMode || CurrentSetpoint == ReefscapeSetpoints.Barge || LastSetpoint == ReefscapeSetpoints.Barge || CurrentRobotMode == ReefscapeRobotMode.Algae);
            
            bool isAlgaeReefSetpoint = (CurrentSetpoint == ReefscapeSetpoints.L2 || 
                                        CurrentSetpoint == ReefscapeSetpoints.L3 || 
                                        CurrentSetpoint == ReefscapeSetpoints.LowAlgae || 
                                        CurrentSetpoint == ReefscapeSetpoints.HighAlgae);

            // --- FLOOR & L1 ROLLER LOGIC ---
            float currentIntakeSpeed = 0f;
            float currentL1Speed = 0f;

            if (isL1Mode)
            {
                if (intakePressed && !hasCoral)
                {
                    currentL1Speed = l1IntakeSpeed;
                    currentIntakeSpeed = -floorIntakeSpeed; // Reverse regular floor intake
                }
                else if (outtakePressed)
                {
                    currentL1Speed = l1OuttakeSpeed;
                    currentIntakeSpeed = floorIntakeSpeed; // Spin regular floor intake normally
                }
            }
            else
            {
                // Lock regular floor intake: only runs when intaking coral from the floor!
                bool runFloorIntake = intakePressed && !hasCoral && !isAlgaeReefSetpoint && CurrentRobotMode != ReefscapeRobotMode.Algae;
                if (runFloorIntake) 
                {
                    currentIntakeSpeed = floorIntakeSpeed;
                    currentL1Speed = -l1IntakeSpeed; // Reverse L1 rollers when intaking normally
                }
            }

            // Apply to Floor Intake Generic Rollers
            if (intakeRollers != null)
            {
                foreach(var roller in intakeRollers) 
                { 
                    if(roller != null) roller.ChangeAngularVelocity(currentIntakeSpeed); 
                }
            }

            // Apply to L1 Generic Rollers
            if (l1IntakeRollers != null)
            {
                foreach (var roller in l1IntakeRollers)
                {
                    if (roller != null) roller.ChangeAngularVelocity(currentL1Speed);
                }
            }

            // End Effector Generic Rollers
            float currentEESpeed = outtakePressed ? eeOuttakeSpeed : 0f;
            if (endEffectorRollers != null)
            {
                foreach(var roller in endEffectorRollers)
                {
                    if (roller != null) roller.ChangeAngularVelocity(currentEESpeed);
                }
            }

            // --- ALGAE ROLLER LOGIC ---
            float currentAlgaeSpeed = algaeIdleSpeed;

            if (isScoringAlgae)
            {
                currentAlgaeSpeed = algaeScoreSpeed;
            }
            else if (hasAlgae || CurrentSetpoint == ReefscapeSetpoints.Climb || CurrentSetpoint == ReefscapeSetpoints.Climbed)
            {
                currentAlgaeSpeed = 0f;
            }
            else if (isAlgaeReefSetpoint || (intakePressed && CurrentRobotMode == ReefscapeRobotMode.Algae))
            {
                currentAlgaeSpeed = algaeActiveSpeed;
            }

            if (algaeRollers != null)
            {
                foreach(var roller in algaeRollers)
                {
                    if (roller != null) roller.ChangeAngularVelocity(currentAlgaeSpeed);
                }
            }

            // Update audio: check if either floor or L1 rollers are spinning
            bool playIntakeAudio = (currentIntakeSpeed != 0f) || (currentL1Speed != 0f);
            UpdateAudio(playIntakeAudio, hasAlgae, isScoringAlgae);

            // Climber Generic Rollers
            float currentClimberSpeed = (CurrentSetpoint == ReefscapeSetpoints.Climb) ? climberSpeed : 0f;
            if (climberRollers != null)
            {
                foreach(var roller in climberRollers) 
                { 
                    if(roller != null) roller.ChangeAngularVelocity(currentClimberSpeed); 
                }
            }

            if (CurrentIntakeMode == ReefscapeIntakeMode.L1)
            {
                CurrentCoralStationMode.DropType = Games.Reefscape.FieldScripts.DropType.Station; 
                CurrentCoralStationMode.DropOrientation = Games.Reefscape.FieldScripts.DropOrientation.Horizontal; 
                CurrentCoralStationMode.DropStrength = 1; 
                _coralController.SetTargetState(l1CoralStow);
            }
            else
            {
                CurrentCoralStationMode.DropType = Games.Reefscape.FieldScripts.DropType.Station; 
                CurrentCoralStationMode.DropOrientation = Games.Reefscape.FieldScripts.DropOrientation.Vertical; 
                CurrentCoralStationMode.DropStrength = 8; 
                _coralController.SetTargetState(coralStowState);
            }
            
            _algaeController.SetTargetState(algaeStowState);

            // L1 Intake Transition Delay: Only buffer when the user has released the intake button
            if (CurrentIntakeMode == ReefscapeIntakeMode.L1 && hasCoral && !intakePressed)
            {
                if (CurrentSetpoint != ReefscapeSetpoints.Place && CurrentSetpoint != ReefscapeSetpoints.L1)
                    bufferedSetpoint = ReefscapeSetpoints.L1;
            }

            if (bufferedSetpoint.HasValue)
            {
                SetState(bufferedSetpoint.Value);
                bufferedSetpoint = null;
            }

            if (CurrentSetpoint != ReefscapeSetpoints.L4 && CurrentSetpoint != ReefscapeSetpoints.Place && CurrentSetpoint != ReefscapeSetpoints.Barge)
            {
                _releaseDetected = false;
                _algaeMode = false; 
            }

            switch (CurrentSetpoint)
            {
                case ReefscapeSetpoints.Stow: 
                    SetRobotTargets(stow); 
                    break;
                case ReefscapeSetpoints.Intake:
                    if (CurrentRobotMode == ReefscapeRobotMode.Algae)
                    {
                        SetRobotTargets(lollipop);
                        _algaeController.RequestIntake(algaeIntake, !hasAlgae && intakePressed);
                        
                        _coralController.RequestIntake(endEffectorHolder, false);
                        if (l1GamePieceIntake != null) _coralController.RequestIntake(l1GamePieceIntake, false);
                    }
                    else 
                    {
                        if (CurrentIntakeMode == ReefscapeIntakeMode.L1)
                        {
                            SetRobotTargets(l1Intake);
                            var targetIntake = l1GamePieceIntake != null ? l1GamePieceIntake : endEffectorHolder;
                            
                            // Check if mechanisms are physically at the intake position
                            bool isElevatorAtTarget = elevator != null && Mathf.Abs(elevator.GetElevatorHeight() - _elevatorTargetHeight) < 2.0f;
                            bool isArmAtTarget = endEffector != null && Mathf.Abs(Mathf.DeltaAngle(endEffector.GetSingleAxisAngle(JointAxis.X), -_endEffectorTargetAngle)) < 15.0f;
                            bool isAtSetpoint = isElevatorAtTarget && isArmAtTarget;

                            _coralController.RequestIntake(targetIntake, !hasCoral && intakePressed && isAtSetpoint);
                        }
                        else
                        {
                            SetRobotTargets(intakePos);
                            
                            // Check if mechanisms are physically at the intake position
                            bool isElevatorAtTarget = elevator != null && Mathf.Abs(elevator.GetElevatorHeight() - _elevatorTargetHeight) < 2.0f;
                            bool isArmAtTarget = endEffector != null && Mathf.Abs(Mathf.DeltaAngle(endEffector.GetSingleAxisAngle(JointAxis.X), -_endEffectorTargetAngle)) < 15.0f;
                            bool isAtSetpoint = isElevatorAtTarget && isArmAtTarget;

                            _coralController.RequestIntake(endEffectorHolder, !hasCoral && intakePressed && isAtSetpoint);
                        }
                    }
                    break;
                case ReefscapeSetpoints.L1:
                    if (CurrentIntakeMode == ReefscapeIntakeMode.L1) SetRobotTargets(l1Score);
                    else SetRobotTargets(l1);
                    break;

                case ReefscapeSetpoints.L2: 
                    if (IsAlgaeInFront()) _stickyAlgaeMode = true;

                    if (_stickyAlgaeMode) 
                    {
                        SetRobotTargets(l2); 
                        _algaeController.RequestIntake(algaeIntake, !hasAlgae && !outtakePressed);
                    }
                    else 
                    {
                        SetRobotTargets(l2Optimized); 
                    }
                    break;

                case ReefscapeSetpoints.L3: 
                    if (IsAlgaeInFront()) _stickyAlgaeMode = true;

                    if (_stickyAlgaeMode) 
                    {
                        SetRobotTargets(l3); 
                        _algaeController.RequestIntake(algaeIntake, !hasAlgae && !outtakePressed);
                    }
                    else 
                    {
                        SetRobotTargets(l3Optimized);
                    }
                    break;

                case ReefscapeSetpoints.LowAlgae: 
                    SetRobotTargets(lowAlgae); 
                    _algaeController.RequestIntake(algaeIntake, !hasAlgae && !outtakePressed); 
                    break;
                case ReefscapeSetpoints.HighAlgae: 
                    SetRobotTargets(highAlgae); 
                    _algaeController.RequestIntake(algaeIntake, !hasAlgae && !outtakePressed); 
                    break;
                
                case ReefscapeSetpoints.L4:
                    if (hasAlgae) _algaeMode = true;
                    else if (_isFreshPress && !hasAlgae) _algaeMode = false;
                    SetRobotTargets(_algaeMode ? barge : l4);
                    break;
                case ReefscapeSetpoints.Barge: SetRobotTargets(barge); break;
                case ReefscapeSetpoints.Climb:
                    SetRobotTargets(climbDeployed);
                    if (climberController != null && climberController.IsReadyToClimb) SetState(ReefscapeSetpoints.Climbed); 
                    break;
                case ReefscapeSetpoints.Climbed: SetRobotTargets(climbed); break;
                
                case ReefscapeSetpoints.Place: 
                    if (_algaeMode || LastSetpoint == ReefscapeSetpoints.Barge)
                    {
                        if (OuttakeAction.IsPressed()) PlacePiece();
                        else { _algaeMode = false; SetState(ReefscapeSetpoints.Stow); }
                    }
                    else if (LastSetpoint == ReefscapeSetpoints.L4) PlacePiece();
                    else if (!OuttakeAction.IsPressed())
                    {
                        if (LastSetpoint == ReefscapeSetpoints.L2 || LastSetpoint == ReefscapeSetpoints.L3) SetState(LastSetpoint);
                        else SetState(ReefscapeSetpoints.Stow);
                    }
                    else PlacePiece(); 
                    break;
                    
                default: SetRobotTargets(stow); break;
            }

            // --- MASTER INTAKE SHUTOFF ---
            if (!intakePressed)
            {
                _coralController.RequestIntake(endEffectorHolder, false);
                if (l1GamePieceIntake != null) _coralController.RequestIntake(l1GamePieceIntake, false);
                
                // Allow automatic algae intaking to bypass the master shutoff
                bool isAutoAlgae = _stickyAlgaeMode || CurrentSetpoint == ReefscapeSetpoints.LowAlgae || CurrentSetpoint == ReefscapeSetpoints.HighAlgae;
                if (!isAutoAlgae)
                {
                    _algaeController.RequestIntake(algaeIntake, false);
                }
            }

            // --- CORAL STATION DROP DISTANCE LOGIC ---
            bool isElevatorReadyForCoral = elevator != null && Mathf.Abs(elevator.GetElevatorHeight() - _elevatorTargetHeight) < 2.0f;
            bool isArmReadyForCoral = endEffector != null && Mathf.Abs(Mathf.DeltaAngle(endEffector.GetSingleAxisAngle(JointAxis.X), -_endEffectorTargetAngle)) < 15.0f;
            
            bool canIntakeCoral = (CurrentRobotMode == ReefscapeRobotMode.Coral) 
                                  && (CurrentSetpoint == ReefscapeSetpoints.Intake) 
                                  && !hasCoral 
                                  && isElevatorReadyForCoral 
                                  && isArmReadyForCoral;

            if (CurrentCoralStationMode != null)
            {
                CurrentCoralStationMode.DropDistance = canIntakeCoral ? 1.55f : 0f;
            }
        }

        private void UpdateAudio(bool playIntakeAudio, bool hasAlgae, bool isScoringAlgae)
        {
            if (BaseGameManager.Instance.RobotState == RobotState.Disabled)
            {
                if (intakeAudio != null) intakeAudio.Stop();
                if (algaeStallAudio != null) algaeStallAudio.Stop();
                if (climberAudio != null) climberAudio.Stop();
                return;
            }

            if (intakeAudio != null)
            {
                if (playIntakeAudio && !intakeAudio.isPlaying) intakeAudio.Play();
                else if (!playIntakeAudio && intakeAudio.isPlaying) intakeAudio.Stop();
            }

            // --- ALGAE STALL AUDIO (1986Stall ONLY) ---
            if (algaeStallAudio != null)
            {
                if (hasAlgae && !isScoringAlgae)
                {
                    // If holding an algae (and not actively outtaking it), play 1986Stall
                    if (algaeStallAudio.clip != algaeStallClip)
                    {
                        algaeStallAudio.Stop();
                        algaeStallAudio.clip = algaeStallClip;
                    }
                    if (!algaeStallAudio.isPlaying && algaeStallClip != null) algaeStallAudio.Play();
                }
                else
                {
                    // Turn off when idle, intaking, or scoring
                    if (algaeStallAudio.isPlaying) algaeStallAudio.Stop();
                }
            }

            bool isClimbing = (CurrentSetpoint == ReefscapeSetpoints.Climb);
            if (climberAudio != null)
            {
                if (isClimbing && !climberAudio.isPlaying) climberAudio.Play();
                else if (!isClimbing && climberAudio.isPlaying) climberAudio.Stop();
            }
        }

        private void SetRobotTargets(suPURDUEperSetpoint setpoint)
        {
            if (setpoint == null) { elevator.SetTarget(0); return; }
            
            _elevatorTargetHeight = setpoint.elevatorHeight;

            elevator.SetTarget(setpoint.elevatorHeight);
            intake.SetTargetAngle(setpoint.intakeAngle).withAxis(JointAxis.X).flipDirection();
            climber.SetTargetAngle(setpoint.climberAngle).withAxis(JointAxis.Z).flipDirection();

            // --- END EFFECTOR HEIGHT WAIT LOGIC ---
            float currentElevatorHeight = elevator != null ? elevator.GetElevatorHeight() : 0f;
            float targetEEAngle = setpoint.endEffectorAngle;

            // Only apply the clearance wait when lifting for high-scoring setpoints (L4 or Barge)
            bool isHighScoring = (CurrentSetpoint == ReefscapeSetpoints.L4 || 
                                  CurrentSetpoint == ReefscapeSetpoints.Barge || 
                                  setpoint == l4 || 
                                  setpoint == barge || 
                                  setpoint == l4Score);

            if (isHighScoring && setpoint.elevatorHeight >= safeClearanceHeight && currentElevatorHeight < safeClearanceHeight)
            {
                targetEEAngle = stow != null ? stow.endEffectorAngle : 0f;
            }

            _endEffectorTargetAngle = targetEEAngle;
            endEffector.SetTargetAngle(targetEEAngle).withAxis(JointAxis.X).flipDirection();
        }

        private void PlacePiece()
        {
            if (_algaeMode || (LastSetpoint == ReefscapeSetpoints.Barge && _algaeController.HasPiece()))
            {
                 if (_algaeController.HasPiece()) _algaeController.ReleaseGamePieceWithForce(new Vector3(0, 0, 6f));
                 SetRobotTargets(barge);
                 return;
            }

            if (LastSetpoint == ReefscapeSetpoints.L4)
            {
                SetRobotTargets(l4);
                if (_coralController.HasPiece()) _coralController.ReleaseGamePieceWithContinuedForce(new Vector3(0, 0, 6f), 1f, 0.5f);
                else if (_releaseDetected) SetRobotTargets(l4Score);
                return;
            }

            RestoreLastSetpoint();

            if (LastSetpoint == ReefscapeSetpoints.L1 || LastSetpoint == ReefscapeSetpoints.L2 || LastSetpoint == ReefscapeSetpoints.L3)
            {
                if (_coralController.HasPiece())
                {
                    if (CurrentIntakeMode == ReefscapeIntakeMode.L1)
                        _coralController.ReleaseGamePieceWithForce(new Vector3(0, 1.5f, 0));
                    else if (LastSetpoint == ReefscapeSetpoints.L1)
                        _coralController.ReleaseGamePieceWithForce(new Vector3(0, 0, 5f));
                    else
                        _coralController.ReleaseGamePieceWithForce(new Vector3(0, 0, 10f));
                }
            }
            else
            {
                if (_algaeController.HasPiece())
                    _algaeController.ReleaseGamePieceWithForce(new Vector3(0, 0, 6f));
            }
        }

        private void RestoreLastSetpoint()
        {
            if (LastSetpoint == ReefscapeSetpoints.L1 && CurrentIntakeMode == ReefscapeIntakeMode.L1)
            {
                SetRobotTargets(l1Score);
                return;
            }

            switch (LastSetpoint)
            {
                case ReefscapeSetpoints.L1: SetRobotTargets(l1); break;
                case ReefscapeSetpoints.L2: 
                    SetRobotTargets(_stickyAlgaeMode ? l2 : l2Optimized);
                    break;
                case ReefscapeSetpoints.L3: 
                    SetRobotTargets(_stickyAlgaeMode ? l3 : l3Optimized);
                    break;
                case ReefscapeSetpoints.LowAlgae: SetRobotTargets(lowAlgae); break;
                case ReefscapeSetpoints.HighAlgae: SetRobotTargets(highAlgae); break;
                case ReefscapeSetpoints.Barge: SetRobotTargets(barge); break;
                default: SetRobotTargets(stow); break;
            }
        }
    }
}