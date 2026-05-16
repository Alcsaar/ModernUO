namespace Server.Custom.Systems.AchievementSystem;

public sealed class AchievementPersistence : GenericPersistence
{
    private static AchievementPersistence _instance;

    public static void Configure()
    {
        _instance ??= new AchievementPersistence();
    }

    private AchievementPersistence() : base("AchievementSystem", 3)
    {
    }

    public override void Serialize(IGenericWriter writer)
    {
        writer.WriteEncodedInt(1); // version
        AchievementService.SerializePersistence(writer);
    }

    public override void Deserialize(IGenericReader reader)
    {
        var version = reader.ReadEncodedInt();

        switch (version)
        {
            case 0:
                {
                    AchievementService.DeserializePersistence(reader, version);
                    break;
                }
            case 1:
                {
                    AchievementService.DeserializePersistence(reader, version);
                    break;
                }
        }
    }
}
