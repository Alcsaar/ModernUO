using System.Collections.Generic;
using Server.Multis.Deeds;

namespace Server.Mobiles
{
    public class SBHouseDeed : SBInfo
    {
        public override IShopSellInfo SellInfo { get; } = new InternalSellInfo();

        public override List<GenericBuyInfo> BuyInfo { get; } = new InternalBuyInfo();

        public class InternalBuyInfo : List<GenericBuyInfo>
        {
            public InternalBuyInfo()
            {
                Add(
                    new GenericBuyInfo(
                        "deed to a stone-and-plaster house",
                        typeof(StonePlasterHouseDeed),
                        109500, // Default price: 43800
                        20,
                        0x14F0,
                        0
                    )
                );
                Add(new GenericBuyInfo("deed to a field stone house", typeof(FieldStoneHouseDeed), 109500, 20, 0x14F0, 0)); // Default price: 43800
                Add(new GenericBuyInfo("deed to a small brick house", typeof(SmallBrickHouseDeed), 109500, 20, 0x14F0, 0)); // Default price: 43800
                Add(new GenericBuyInfo("deed to a wooden house", typeof(WoodHouseDeed), 109500, 20, 0x14F0, 0)); // Default price: 43800
                Add(
                    new GenericBuyInfo(
                        "deed to a wood-and-plaster house",
                        typeof(WoodPlasterHouseDeed),
                        109500, // Default price: 43800
                        20,
                        0x14F0,
                        0
                    )
                );
                Add(
                    new GenericBuyInfo(
                        "deed to a thatched-roof cottage",
                        typeof(ThatchedRoofCottageDeed),
                        109500, // Default price: 43800
                        20,
                        0x14F0,
                        0
                    )
                );
                Add(new GenericBuyInfo("deed to a brick house", typeof(BrickHouseDeed), 361250, 20, 0x14F0, 0)); // Default price: 144500
                Add(
                    new GenericBuyInfo(
                        "deed to a two-story wood-and-plaster house",
                        typeof(TwoStoryWoodPlasterHouseDeed),
                        481000, // Default price: 192400
                        20,
                        0x14F0,
                        0
                    )
                );
                Add(
                    new GenericBuyInfo(
                        "deed to a two-story stone-and-plaster house",
                        typeof(TwoStoryStonePlasterHouseDeed),
                        481000, // Default price: 192400
                        20,
                        0x14F0,
                        0
                    )
                );
                Add(new GenericBuyInfo("deed to a tower", typeof(TowerDeed), 1083000, 20, 0x14F0, 0)); // Default price: 433200
                Add(new GenericBuyInfo("deed to a small stone keep", typeof(KeepDeed), 1750000, 20, 0x14F0, 0)); // Default price: 665200
                Add(new GenericBuyInfo("deed to a castle", typeof(CastleDeed), 3000000, 20, 0x14F0, 0)); // Default price: 1022800
                Add(new GenericBuyInfo("deed to a large house with patio", typeof(LargePatioDeed), 382000, 20, 0x14F0, 0)); // Default price: 152800
                Add(new GenericBuyInfo("deed to a marble house with patio", typeof(LargeMarbleDeed), 480000, 20, 0x14F0, 0)); // Default price: 192000
                Add(new GenericBuyInfo("deed to a small stone tower", typeof(SmallTowerDeed), 221250, 20, 0x14F0, 0)); // Default price: 88500
                Add(new GenericBuyInfo("deed to a two story log cabin", typeof(LogCabinDeed), 244500, 20, 0x14F0, 0)); // Default price: 97800
                Add(
                    new GenericBuyInfo(
                        "deed to a sandstone house with patio",
                        typeof(SandstonePatioDeed),
                        227250, // Default price: 90900
                        20,
                        0x14F0,
                        0
                    )
                );
                Add(new GenericBuyInfo("deed to a two story villa", typeof(VillaDeed), 341250, 20, 0x14F0, 0)); // Default price: 136500
                Add(new GenericBuyInfo("deed to a small stone workshop", typeof(StoneWorkshopDeed), 151500, 20, 0x14F0, 0)); // Default price: 60600
                Add(new GenericBuyInfo("deed to a small marble workshop", typeof(MarbleWorkshopDeed), 157500, 20, 0x14F0, 0)); // Default price: 63000
            }
        }

        public class InternalSellInfo : GenericSellInfo
        {
        }
    }
}
