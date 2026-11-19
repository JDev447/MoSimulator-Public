using UnityEngine;

namespace Prefabs.Reefscape.Robots.Mods.IndianaModpack._7457
{
    [CreateAssetMenu(fileName = "Setpoint", menuName = "Robot/suPURDUEper Setpoint", order = 0)]
    public class suPURDUEperSetpoint : ScriptableObject
    {
        [Header("Elevator")]
        [Tooltip("Inches")] public float elevatorHeight;

        [Header("Intake")]
        [Tooltip("Degrees (X-Axis)")] public float intakeAngle;

        [Header("End Effector")]
        [Tooltip("Degrees (X-Axis)")] public float endEffectorAngle;

        [Header("Climber")]
        [Tooltip("Degrees (Z-Axis)")] public float climberAngle;
    }
}