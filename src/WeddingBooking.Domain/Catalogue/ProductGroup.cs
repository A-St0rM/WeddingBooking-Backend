namespace WeddingBooking.Domain.Catalogue;

/// <summary>The parts a wedding is sold in — the groups a quote is built from.</summary>
public enum ProductGroup
{
    Reception,
    Ceremony,
    DinnerAndParty,
    LateNight,
    Bar,
    Breakfast,
    FridayEvent,
    Other,
}

/// <summary>How a line is priced.</summary>
public enum LinePricing
{
    /// <summary>Unit price × the booking's guest count. Follows the guest count by itself.</summary>
    PerGuest,

    /// <summary>Unit price × the line's own quantity.</summary>
    Fixed,
}

public enum PackageKind
{
    /// <summary>The base package: menu, wine and beer.</summary>
    Base,

    /// <summary>Something added on top: late-night food, the bar, the Friday event.</summary>
    AddOn,
}
