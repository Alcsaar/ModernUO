using System.Collections.Generic;

namespace Server.Custom.Systems.TravelCodex;

public sealed class TravelCodexPlayerRecord
{
    public string PlayerSerial { get; set; }

    public List<string> DiscoveredDestinationKeys { get; set; } = new();
}

public sealed class TravelCodexPlayerDataFile
{
    public List<TravelCodexPlayerRecord> Players { get; set; } = new();
}
