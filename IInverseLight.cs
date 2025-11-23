using UnityEngine.Rendering.Universal;

public interface IInverseLight
{
    Light2D GetLight();
    void RegisterToController();
    void UnregisterFromController();
}