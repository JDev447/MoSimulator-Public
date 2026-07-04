using UnityEngine;

namespace Prefabs.Reefscape.Robots.Mods.IndianaModpack._1501
{
    [CreateAssetMenu(fileName = "Setpoint", menuName = "Robot/THRUST Setpoint", order = 0)]
    public class THRUSTSetpoint : ScriptableObject
    {
        [Header("Elevator")]
        [Tooltip("Inches")] public float elevatorHeight;

        [Header("End Effector")]
        [Tooltip("Degrees (X-Axis)")] public float endEffectorAngle;

        [Header("Algae Manipulator")]
        [Tooltip("Degrees (X-Axis)")] public float algaeManipulatorAngle;

        [Header("Climber")]
        [Tooltip("Degrees (Z-Axis)")] public float climberAngle;

        [Header("Funnel")]
        [Tooltip("Degrees (X-Axis)")] public float funnelAngle;
    }
}