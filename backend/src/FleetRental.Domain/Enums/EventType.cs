namespace FleetRental.Domain.Enums;

/// <summary>
/// What the client is renting for. Phase 3 analytics group revenue and demand by
/// this, which is why it is a typed field rather than free text in the notes.
/// </summary>
public enum EventType
{
    ProductLaunch = 0,
    TradeShow = 1,
    Wedding = 2,
    CorporateEvent = 3,
    Photoshoot = 4,
    RoadShow = 5,
    Conference = 6,
    Other = 99,
}
