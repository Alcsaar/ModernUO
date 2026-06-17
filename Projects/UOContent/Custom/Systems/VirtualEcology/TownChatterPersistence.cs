namespace Server.Custom.Systems.VirtualEcology;

public sealed class TownChatterPersistence : GenericPersistence
{
    private static TownChatterPersistence _instance;

    public static void Configure()
    {
        _instance ??= new TownChatterPersistence();
    }

    private TownChatterPersistence() : base("TownChatter", 3)
    {
    }

    public override void Serialize(IGenericWriter writer)
    {
        writer.WriteEncodedInt(1); // version
        TownChatterService.SerializePersistence(writer);
    }

    public override void Deserialize(IGenericReader reader)
    {
        var version = reader.ReadEncodedInt();

        switch (version)
        {
            case 0:
            case 1:
                {
                    TownChatterService.DeserializePersistence(reader, version);
                    break;
                }
        }
    }
}
