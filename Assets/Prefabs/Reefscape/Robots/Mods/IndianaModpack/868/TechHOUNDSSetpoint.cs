using UnityEngine;

namespace Prefabs.Reefscape.Robots.Mods.IndianaModpack._868
{
    [CreateAssetMenu(fileName = "Setpoint", menuName = "Robot/TechHOUNDS Setpoint", order = 0)]
    public class TechHOUNDSSetpoint : ScriptableObject
    {
        [Header("Elevator")]
        [Tooltip("Inches")] public float elevatorHeight;

        [Header("End Effector")]
        [Tooltip("Degrees (X-Axis)")] public float endEffectorAngle;

        [Header("Climber")]
        [Tooltip("Degrees (Z-Axis)")] public float climberAngle;
    }
}