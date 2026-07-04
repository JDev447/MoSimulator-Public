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

        [Header("Rollers")]
        [SerializeField] private GenericRoller[] coralRollers;
        [SerializeField] private GenericRoller[] algaeRollers;

        [Header("Roller Speeds")]
        [SerializeField] private float coralIntakeSpeed = 800f;
        [SerializeField] private float coralOuttakeSpeed = -1000f;
        [SerializeField] private float algaeIntakeSpeed = 800f;
        [SerializeField] private float algaeOuttakeSpeed = -1000f;

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

        private RobotGamePieceController<ReefscapeGamePiece, ReefscapeGamePieceData>.GamePieceControllerNode _coralController;
        private RobotGamePieceController<ReefscapeGamePiece, ReefscapeGamePieceData>.GamePieceControllerNode _algaeController; 

        protected override void Start()
        {
            base.Start(); 
            
            // Initialize PIDs safely
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
        }

        private void LateUpdate()
        {
            if (endEffector != null) endEffector.UpdatePid(endEffectorPid);
            if (algaeManipulator != null) algaeManipulator.UpdatePid(algaeManipulatorPid);
            if (climber != null) climber.UpdatePid(climberPid);
            if (funnel != null) funnel.UpdatePid(funnelPid);
        }

        private void FixedUpdate()
        {
            bool hasCoral = _coralController != null && _coralController.HasPiece();
            bool hasAlgae = _algaeController != null && _algaeController.HasPiece();
            bool intakePressed = IntakeAction.IsPressed(); 
            bool outtakePressed = OuttakeAction.IsPressed();

            if (CurrentRobotMode == ReefscapeRobotMode.Algae && hasAlgae)
            {
                SetRobotMode(ReefscapeRobotMode.Coral);
            }

            // --- ROLLER LOGIC ---
            float currentCoralSpeed = 0f;
            if (intakePressed && !hasCoral) currentCoralSpeed = coralIntakeSpeed;
            else if (outtakePressed) currentCoralSpeed = coralOuttakeSpeed;

            if (coralRollers != null)
                foreach(var roller in coralRollers) 
                    if(roller != null) roller.ChangeAngularVelocity(currentCoralSpeed); 

            float currentAlgaeSpeed = 0f;
            if (intakePressed && !hasAlgae) currentAlgaeSpeed = algaeIntakeSpeed;
            else if (outtakePressed) currentAlgaeSpeed = algaeOuttakeSpeed;

            if (algaeRollers != null)
                foreach(var roller in algaeRollers)
                    if (roller != null) roller.ChangeAngularVelocity(currentAlgaeSpeed);

            // --- STATE MACHINE ---
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
                    }
                    else 
                    {
                        SetSetpointTargets(intake);
                        if (_coralController != null && coralIntakeHolder != null) 
                            _coralController.RequestIntake(coralIntakeHolder, !hasCoral && intakePressed);
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

            // Apply targets to physical joints safely (Now with .noWrap added to the endEffector)
            if (elevator != null) elevator.SetTarget(_elevatorTarget);
            if (endEffector != null) endEffector.SetTargetAngle(_endEffectorTarget).withAxis(JointAxis.X).noWrap(forbiddenEndEffectorAngle);
            if (algaeManipulator != null) algaeManipulator.SetTargetAngle(_algaeManipulatorTarget).withAxis(JointAxis.X);
            if (climber != null) climber.SetTargetAngle(_climberTarget).withAxis(JointAxis.Z);
            if (funnel != null) funnel.SetTargetAngle(_funnelTarget).withAxis(JointAxis.X);
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
            }
            else if (_coralController != null && _coralController.HasPiece())
            {
                if (LastSetpoint == ReefscapeSetpoints.L4) 
                    _coralController.ReleaseGamePieceWithContinuedForce(new Vector3(0, 0f, 6f), 1f, 0.5f);
                else if (LastSetpoint == ReefscapeSetpoints.L1) 
                    _coralController.ReleaseGamePieceWithForce(new Vector3(0, 0, 3f));
                else 
                    _coralController.ReleaseGamePieceWithForce(new Vector3(0, 0f, 7f));
            }
        }
    }
}