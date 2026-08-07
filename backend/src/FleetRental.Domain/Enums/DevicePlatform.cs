namespace FleetRental.Domain.Enums;

/// <summary>Push target platform for a registered device.</summary>
public enum DevicePlatform
{
    Ios = 0,
    Android = 1,

    /// <summary>Browser push, used by the admin panel.</summary>
    Web = 2,
}
