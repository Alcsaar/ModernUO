namespace Server.Custom.Systems.VirtueAlignment;

public sealed class VirtueAlignmentPersistence : GenericPersistence
{
    private static VirtueAlignmentPersistence _instance;

    public static void Configure()
    {
        _instance ??= new VirtueAlignmentPersistence();
    }

    private VirtueAlignmentPersistence() : base("VirtueAlignment", 1)
    {
    }

    public override void Serialize(IGenericWriter writer)
    {
        writer.WriteEncodedInt(1);
        VirtueAlignmentService.Serialize(writer);
    }

    public override void Deserialize(IGenericReader reader)
    {
        var version = reader.ReadEncodedInt();

        if (version is 0 or 1)
        {
            VirtueAlignmentService.Deserialize(reader, version);
        }
    }
}
