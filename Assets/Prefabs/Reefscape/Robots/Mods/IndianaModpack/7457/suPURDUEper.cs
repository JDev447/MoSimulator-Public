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
        [SerializeField] private GenericRoller[] climberRollers;
        [SerializeField] private GenericRoller[] endEffectorRollers;
        [SerializeField] private GenericRoller[] algaeRollers;

        [Header("Roller Speeds")]
        [SerializeField] private float floorIntakeSpeed = 800f;
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
        [SerializeField] private AudioClip algaeStallClip;
        [SerializeField] private AudioSource climberAudio; 
        [SerializeField] private AudioClip climberClip;

        [Header("Sensors")]
        [Tooltip("The Trigger Script on the front of the robot (Set to 'Algae' mode)")]
        [SerializeField] private suPURDUEperClimberTrigger algaeSensor;

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

            UpdateAudio(intakePressed, hasCoral, hasAlgae);

            // Floor Intake Generic Rollers
            float currentIntakeSpeed = (intakePressed && !hasCoral) ? floorIntakeSpeed : 0f;
            if (intakeRollers != null)
            {
                foreach(var roller in intakeRollers) 
                { 
                    if(roller != null) roller.ChangeAngularVelocity(currentIntakeSpeed); 
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

            // Algae Generic Rollers (Animation / Visual Logic)
            float currentAlgaeSpeed = algaeIdleSpeed;

            if (outtakePressed)
            {
                currentAlgaeSpeed = algaeScoreSpeed;
            }
            else if (CurrentSetpoint == ReefscapeSetpoints.L2 || CurrentSetpoint == ReefscapeSetpoints.L3 || (intakePressed && !hasAlgae))
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

            if (CurrentIntakeMode == ReefscapeIntakeMode.L1 && hasCoral)
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
                        _algaeController.RequestIntake(algaeIntake, !hasAlgae);
                    }
                    else 
                    {
                        if (CurrentIntakeMode == ReefscapeIntakeMode.L1)
                        {
                            SetRobotTargets(l1Intake);
                            var targetIntake = l1GamePieceIntake != null ? l1GamePieceIntake : endEffectorHolder;
                            _coralController.RequestIntake(targetIntake, !hasCoral && intakePressed);
                        }
                        else
                        {
                            SetRobotTargets(intakePos);
                            _coralController.RequestIntake(endEffectorHolder, !hasCoral && intakePressed);
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
                        
                        if (!outtakePressed) 
                            _algaeController.RequestIntake(algaeIntake, !hasAlgae);
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
                        if (!outtakePressed) 
                            _algaeController.RequestIntake(algaeIntake, !hasAlgae);
                    }
                    else 
                    {
                        SetRobotTargets(l3Optimized);
                    }
                    break;

                case ReefscapeSetpoints.LowAlgae: SetRobotTargets(lowAlgae); _algaeController.RequestIntake(algaeIntake, !hasAlgae); break;
                case ReefscapeSetpoints.HighAlgae: SetRobotTargets(highAlgae); _algaeController.RequestIntake(algaeIntake, !hasAlgae); break;
                
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
        }

        private void UpdateAudio(bool intakePressed, bool hasCoral, bool hasAlgae)
        {
            if (BaseGameManager.Instance.RobotState == RobotState.Disabled)
            {
                if (intakeAudio != null) intakeAudio.Stop();
                if (algaeStallAudio != null) algaeStallAudio.Stop();
                if (climberAudio != null) climberAudio.Stop();
                return;
            }

            bool runIntake = (intakePressed && !hasCoral);
            if (intakeAudio != null)
            {
                if (runIntake && !intakeAudio.isPlaying) intakeAudio.Play();
                else if (!runIntake && intakeAudio.isPlaying) intakeAudio.Stop();
            }

            if (algaeStallAudio != null)
            {
                if (hasAlgae && !algaeStallAudio.isPlaying) algaeStallAudio.Play();
                else if (!hasAlgae && algaeStallAudio.isPlaying) algaeStallAudio.Stop();
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
            elevator.SetTarget(setpoint.elevatorHeight);
            intake.SetTargetAngle(setpoint.intakeAngle).withAxis(JointAxis.X).flipDirection();
            endEffector.SetTargetAngle(setpoint.endEffectorAngle).withAxis(JointAxis.X).flipDirection();
            climber.SetTargetAngle(setpoint.climberAngle).withAxis(JointAxis.Z).flipDirection();
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