using UnityEngine;

namespace Prefabs.Reefscape.Robots.Mods.IndianaModpack._4272
{
    [CreateAssetMenu(fileName = "Setpoint", menuName = "Robot/Maverick Setpoint", order = 0)]
    public class MaverickSetpoint : ScriptableObject
    {
        [Header("Elevator")]
        [Tooltip("Inches")] public float elevatorHeight;

        [Header("End Effector")]
        [Tooltip("Degrees (Z-Axis)")] public float endEffectorAngle;

        [Header("Climber")]
        [Tooltip("Degrees (X-Axis)")] public float climberAngle;
    }
}