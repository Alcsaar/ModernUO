namespace Server.Custom.Systems.TravelCodex;

public sealed class TravelCodexPersistence : GenericPersistence
{
    private static TravelCodexPersistence _instance;

    public static void Configure()
    {
        _instance ??= new TravelCodexPersistence();
    }

    private TravelCodexPersistence() : base("TravelCodexPlayerData", 3)
    {
    }

    public override void Serialize(IGenericWriter writer)
    {
        writer.WriteEncodedInt(0); // version
        TravelCodexManager.SerializePlayerData(writer);
    }

    public override void Deserialize(IGenericReader reader)
    {
        var version = reader.ReadEncodedInt();

        switch (version)
        {
            case 0:
                {
                    TravelCodexManager.DeserializePlayerData(reader);
                    break;
                }
        }
    }
}
