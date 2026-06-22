namespace Server.Custom.Systems.Townships;

public sealed class TownshipPersistence : GenericPersistence
{
    private static TownshipPersistence _instance;

    public static void Configure()
    {
        _instance ??= new TownshipPersistence();
    }

    private TownshipPersistence() : base("Townships", 3)
    {
    }

    public override void Serialize(IGenericWriter writer)
    {
        writer.WriteEncodedInt(10);
        TownshipService.Serialize(writer);
    }

    public override void Deserialize(IGenericReader reader)
    {
        var version = reader.ReadEncodedInt();

        if (version is >= 0 and <= 10)
        {
            TownshipService.Deserialize(reader, version);
        }
    }
}
