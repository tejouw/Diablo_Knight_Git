// Path: Assets/Game/Scripts/PlayerNetworkInput.cs

using Fusion;
using UnityEngine;

/// <summary>
/// Network input structure for player movement and actions
/// </summary>
public struct PlayerNetworkInput : INetworkInput
{
    public Vector2 MovementInput;
    public bool IsMoving;
    public Vector2 LookDirection;
    
    // Future expansion for actions
    public bool Attack;
    public bool UseSkill;
    public bool Interact;
}