using ModernUO.Serialization;
using System;
using System.Collections.Generic;
using Server.Multis.Deeds;
using Server.Network;
using Server.Targeting;

namespace Server.Mobiles
{
    [SerializationGenerator(0, false)]
    public partial class RealEstateBroker : BaseVendor
    {
        private readonly List<SBInfo> m_SBInfos = new();
        private DateTime m_NextCheckPack;

        [Constructible]
        public RealEstateBroker() : base("the real estate broker")
        {
        }

        protected override List<SBInfo> SBInfos => m_SBInfos;

        public override bool HandlesOnSpeech(Mobile from)
        {
            if (from.Alive && from.InRange(this, 3))
            {
                return true;
            }

            return base.HandlesOnSpeech(from);
        }

        public override void OnMovement(Mobile m, Point3D oldLocation)
        {
            if (Core.Now > m_NextCheckPack && InRange(m, 4) && !InRange(oldLocation, 4) && m.Player)
            {
                var pack = m.Backpack;

                if (pack != null)
                {
                    m_NextCheckPack = Core.Now + TimeSpan.FromSeconds(2.0);

                    if (pack.FindItemByType<HouseDeed>(false) != null)
                    {
                        // If you have a deed, I can appraise it or buy it from you...
                        PrivateOverheadMessage(MessageType.Regular, 0x3B2, 500605, m.NetState);

                        // Simply hand me a deed to sell it.
                        PrivateOverheadMessage(MessageType.Regular, 0x3B2, 500606, m.NetState);
                    }
                }
            }

            base.OnMovement(m, oldLocation);
        }

        public override void OnSpeech(SpeechEventArgs e)
        {
            if (!e.Handled && e.Mobile.Alive && e.HasKeyword(0x38)) // *appraise*
            {
                PublicOverheadMessage(MessageType.Regular, 0x3B2, 500608); // Which deed would you like appraised?
                e.Mobile.BeginTarget(12, false, TargetFlags.None, Appraise_OnTarget);
                e.Handled = true;
            }

            base.OnSpeech(e);
        }

        public override bool OnDragDrop(Mobile from, Item dropped)
        {
            if (dropped is HouseDeed deed)
            {
                var price = ComputePriceFor(deed);

                if (price > 0)
                {
                    if (Banker.Deposit(from, price))
                    {
                        // For the deed I have placed gold in your bankbox :
                        PublicOverheadMessage(MessageType.Regular, 0x3B2, 1008000, AffixType.Append, price.ToString());

                        deed.Delete();
                        return true;
                    }

                    PublicOverheadMessage(MessageType.Regular, 0x3B2, 500390); // Your bank box is full.
                    return false;
                }

                PublicOverheadMessage(MessageType.Regular, 0x3B2, 500607); // I'm not interested in that.
                return false;
            }

            return base.OnDragDrop(from, dropped);
        }

        public void Appraise_OnTarget(Mobile from, object obj)
        {
            if (obj is HouseDeed deed)
            {
                var price = ComputePriceFor(deed);

                if (price > 0)
                {
                    // I will pay you gold for this deed :
                    PublicOverheadMessage(MessageType.Regular, 0x3B2, 1008001, AffixType.Append, price.ToString());

                    PublicOverheadMessage(
                        MessageType.Regular,
                        0x3B2,
                        500610
                    ); // Simply hand me the deed if you wish to sell it.
                }
                else
                {
                    PublicOverheadMessage(MessageType.Regular, 0x3B2, 500607); // I'm not interested in that.
                }
            }
            else
            {
                PublicOverheadMessage(MessageType.Regular, 0x3B2, 500609); // I can't appraise things I know nothing about...
            }
        }

        public int ComputePriceFor(HouseDeed deed)
        {
            var price = deed switch
            {
                SmallBrickHouseDeed           => 109500,  // Default price: 43800
                StonePlasterHouseDeed         => 109500,  // Default price: 43800
                FieldStoneHouseDeed           => 109500,  // Default price: 43800
                WoodHouseDeed                 => 109500,  // Default price: 43800
                WoodPlasterHouseDeed          => 109500,  // Default price: 43800
                ThatchedRoofCottageDeed       => 109500,  // Default price: 43800
                BrickHouseDeed                => 361250,  // Default price: 144500
                TwoStoryWoodPlasterHouseDeed  => 481000,  // Default price: 192400
                TwoStoryStonePlasterHouseDeed => 481000,  // Default price: 192400
                TowerDeed                     => 1083000, // Default price: 433200
                KeepDeed                      => 1750000, // Default price: 665200
                CastleDeed                    => 3000000, // Default price: 1022800
                LargePatioDeed                => 382000,  // Default price: 152800
                LargeMarbleDeed               => 480000,  // Default price: 192000
                SmallTowerDeed                => 221250,  // Default price: 88500
                LogCabinDeed                  => 244500,  // Default price: 97800
                SandstonePatioDeed            => 227250,  // Default price: 90900
                VillaDeed                     => 341250,  // Default price: 136500
                StoneWorkshopDeed             => 151500,  // Default price: 60600
                MarbleWorkshopDeed            => 157500,  // Default price: 63000
                _                             => 0
            };

            return AOS.Scale(price, 80); // refunds 80% of the purchase price
        }

        public override void InitSBInfo()
        {
            m_SBInfos.Add(new SBRealEstateBroker());
        }
    }
}
