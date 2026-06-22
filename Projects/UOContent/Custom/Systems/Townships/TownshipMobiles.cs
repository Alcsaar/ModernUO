using System;
using ModernUO.Serialization;
using Server.Collections;
using Server.ContextMenus;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Multis;
using Server.Network;
using CalcMoves = Server.Movement.Movement;

namespace Server.Custom.Systems.Townships;

public interface ITownshipServiceNpc : ITownshipOwnedObject
{
    string ServiceId { get; }
    TownshipState Township { get; }
    TownshipPaidServiceRecord Service { get; }
    bool ServiceActive { get; }
    void BeginManagement(Mobile from);
    void ApplyServiceMovement();
    void ApplyServiceGender(bool female);
}

public static class TownshipServiceNpcUtility
{
    public static TownshipState GetTownship(string townshipId) => TownshipService.FindById(townshipId);

    public static TownshipPaidServiceRecord GetService(string townshipId, string serviceId) =>
        TownshipService.FindPaidService(GetTownship(townshipId), serviceId);

    public static bool IsServiceActive(string townshipId, string serviceId)
    {
        var township = GetTownship(townshipId);
        var service = TownshipService.FindPaidService(township, serviceId);
        return service?.Status == TownshipPaidServiceStatus.Active && township?.IsDelinquent != true;
    }

    public static void NormalizeRuntimeState(BaseCreature npc, string legacyTownshipTitle, string title)
    {
        /* Township service NPCs use standard vendor invulnerability rather than Blessed so
         * they remain protected without drawing the yellow invulnerable healthbar.
         */
        npc.Blessed = false;
        npc.YellowHealthbar = false;

        if (string.IsNullOrWhiteSpace(npc.Title) || npc.Title == legacyTownshipTitle)
        {
            npc.Title = title;
        }
    }

    public static void ShowTownshipLabel(BaseCreature npc, Mobile from, string townshipId)
    {
        var township = GetTownship(townshipId);

        if (from?.NetState != null && township != null && !string.IsNullOrWhiteSpace(township.Name))
        {
            npc.PrivateOverheadMessage(MessageType.Regular, 1153, false, $"[ {township.Name} ]", from.NetState);
        }
    }

    public static void AddManagementEntry(
        Mobile from,
        ref PooledRefList<ContextMenuEntry> list,
        ITownshipServiceNpc npc
    )
    {
        if (TownshipService.CanManageTownship(npc.Township, from))
        {
            list.Add(new TownshipNpcManageEntry());
        }
    }

    public static void BeginManagement(BaseCreature npc, Mobile from, string townshipId)
    {
        var township = GetTownship(townshipId);

        if (!TownshipService.CanManageTownship(township, from))
        {
            from.SendMessage(0x22, "You do not have permission to modify this township NPC.");
            return;
        }

        if (!from.InRange(npc.Location, 12))
        {
            from.SendLocalizedMessage(500446);
            return;
        }

        from.SendGump(new TownshipNpcManagementGump((ITownshipServiceNpc)npc));
    }

    public static void ApplyMovement(BaseCreature npc, string townshipId, string serviceId)
    {
        var service = GetService(townshipId, serviceId);
        var range = Math.Clamp(service?.RoamRange ?? 0, 0, TownshipService.MaxTownshipNpcRoamRange);

        npc.Home = service?.HomeLocation ?? npc.Location;
        npc.RangeHome = range;
        npc.CantWalk = range <= 0;
    }

    public static void ApplyGender(Mobile npc, bool female)
    {
        if (npc.IsBodyMod)
        {
            npc.BodyMod = 0;
        }

        npc.Body = female ? 0x191 : 0x190;
        npc.Female = female;

        /* Human hair and beard item IDs can be gender-specific. Clear invalid choices before
         * forcing the incoming-mobile packet used to refresh nearby clients after customization.
         */
        if (!Race.Human.ValidateHair(female, npc.HairItemID))
        {
            npc.HairItemID = 0;
        }

        if (!Race.Human.ValidateFacialHair(female, npc.FacialHairItemID))
        {
            npc.FacialHairItemID = 0;
        }

        npc.ProcessDelta();
        npc.SendIncomingPacket();
        npc.InvalidateProperties();
    }

    public static bool CheckHouseMove(BaseCreature npc, Direction d, string townshipId, string serviceId)
    {
        var service = GetService(townshipId, serviceId);

        if (service?.AnchorHouseSerial is not { } houseSerial || houseSerial == Serial.Zero)
        {
            return true;
        }

        var house = World.FindItem(houseSerial) as BaseHouse;

        if (house == null || house.Deleted)
        {
            return false;
        }

        var destination = npc.Location;
        CalcMoves.Offset(d, ref destination);
        return house.IsInside(destination, 16);
    }

    public static void CheckAnchorHouse(BaseCreature npc, string townshipId, string serviceId, string serviceName)
    {
        var service = GetService(townshipId, serviceId);

        if (service?.AnchorHouseSerial is not { } houseSerial || houseSerial == Serial.Zero)
        {
            return;
        }

        var house = World.FindItem(houseSerial) as BaseHouse;

        if (house?.Deleted != false)
        {
            TownshipService.MarkServiceObjectMissing(townshipId, serviceId, "Anchored house no longer exists.");
            return;
        }

        if (!house.IsInside(npc) && service.HomeLocation != Point3D.Zero && npc.Map != Map.Internal)
        {
            npc.MoveToWorld(service.HomeLocation, npc.Map);
        }
    }

    public static void MarkDeath(string townshipId, string serviceId, string serviceName) =>
        TownshipService.MarkServiceObjectMissing(townshipId, serviceId, $"{serviceName} was killed.");

    public static void MarkDeleted(string townshipId, string serviceId, string serviceName) =>
        TownshipService.MarkServiceObjectMissing(townshipId, serviceId, $"{serviceName} was deleted.");

    private sealed class TownshipNpcManageEntry : ContextMenuEntry
    {
        /* Township NPCs reuse the vendor customization flow, so the context action should describe
         * customization instead of using an unrelated client localization string.
         */
        public TownshipNpcManageEntry() : base(1019069, 12) // Customize
        {
        }

        public override void OnClick(Mobile from, IEntity target)
        {
            if (target is ITownshipServiceNpc npc)
            {
                npc.BeginManagement(from);
            }
        }
    }
}

[SerializationGenerator(0)]
public partial class TownshipPatrolGuard : BaseCreature, ITownshipOwnedObject
{
    public const int ThreatScanRange = 18;
    public const int AlertRange = 36;

    private DateTime _nextPotionUse;

    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private string _townshipId;

    [Constructible]
    public TownshipPatrolGuard() : this(null)
    {
    }

    public TownshipPatrolGuard(string townshipId) : base(AIType.AI_Melee, FightMode.Aggressor, ThreatScanRange, 1)
    {
        _townshipId = townshipId;
        Title = "the militia guard";
        SetStr(200, 240);
        SetDex(130, 160);
        SetInt(70, 95);
        SetHits(285, 335);
        SetDamage(16, 24);
        SetResistance(ResistanceType.Physical, 55, 65);
        SetResistance(ResistanceType.Fire, 45, 55);
        SetResistance(ResistanceType.Cold, 45, 55);
        SetResistance(ResistanceType.Poison, 45, 55);
        SetResistance(ResistanceType.Energy, 45, 55);
        VirtualArmor = 50;
        SpeechHue = Utility.RandomDyedHue();
        Hue = Race.Human.RandomSkinHue();
        Female = Utility.RandomBool();
        Body = Female ? 0x191 : 0x190;
        Name = NameList.RandomName(Female ? "female" : "male");
        Blessed = false;
        YellowHealthbar = false;
        CantWalk = false;
        RangeHome = 8;
        ActiveSpeed = 0.12;
        PassiveSpeed = 0.25;
        CurrentSpeed = PassiveSpeed;
        Fame = 0;
        Karma = 0;

        EquipMilitiaItem(new PlateChest());
        EquipMilitiaItem(new PlateArms());
        EquipMilitiaItem(new PlateLegs());
        EquipMilitiaItem(new PlateGloves());
        EquipMilitiaItem(new PlateGorget());
        EquipMilitiaItem(new PlateHelm());
        EquipMilitiaItem(new Boots());
        EquipMilitiaItem(new Cloak(Utility.RandomBlueHue()));

        Utility.AssignRandomHair(this);

        if (!Female && Utility.RandomBool())
        {
            Utility.AssignRandomFacialHair(this, HairHue);
        }

        BaseWeapon weapon = Utility.RandomBool() ? new Halberd() : new Broadsword();
        EquipMilitiaItem(weapon);

        Skills.Anatomy.Base = 115.0;
        Skills.Tactics.Base = 115.0;
        Skills.Swords.Base = 115.0;
        Skills.MagicResist.Base = 115.0;
        Skills.Healing.Base = 105.0;
        Skills.DetectHidden.Base = 100.0;

        EnsureMounted();
    }

    public TownshipState Township => TownshipService.FindById(_townshipId);

    public override bool InitialInnocent => true;

    public override bool CanOpenDoors => false;

    public override bool ClickTitle => false;

    public override bool CanRummageCorpses => false;

    public override bool CanHeal => true;

    public override double HealTrigger => 0.72;

    public override double HealDelay => 8.0;

    public override double HealInterval => 14.0;

    public override void OnSingleClick(Mobile from)
    {
        TownshipServiceNpcUtility.ShowTownshipLabel(this, from, _townshipId);
        base.OnSingleClick(from);
    }

    public override void OnThink()
    {
        base.OnThink();

        var township = Township;

        if (township == null || !TownshipService.HasActivePerk(township, TownshipPaidServiceType.GuardedTown))
        {
            Delete();
            return;
        }

        EnsureMounted();

        if (Map != township.Map || !TownshipService.IsInsideTownshipEnvelope(township, this))
        {
            TownshipService.TryMovePatrolGuardToTownship(this, township);
            Combatant = null;
            return;
        }

        if (!ValidateCombatant(township))
        {
            Combatant = FindMilitiaThreat(township);
        }

        if (Combatant != null)
        {
            SetCurrentSpeedToActive();
        }

        UseMilitiaPotionIfNeeded();

        if (Combatant == null && !TownshipService.Contains(township, X, Y))
        {
            TownshipService.TryMovePatrolGuardToTownship(this, township);
        }
    }

    public override bool IsEnemy(Mobile m) => IsMilitiaThreat(Township, m) || base.IsEnemy(m);

    public override void GenerateLoot()
    {
    }

    public override bool OnBeforeDeath()
    {
        DeleteMount();
        return base.OnBeforeDeath();
    }

    public override void OnAfterDelete()
    {
        DeleteMount();
        TownshipService.RemovePatrolGuardSerial(_townshipId, Serial);
        base.OnAfterDelete();
    }

    public void OnTownshipDeleted(TownshipState township) => Delete();

    [AfterDeserialization]
    private void AfterDeserialization()
    {
        Title = "the militia guard";
        Blessed = false;
        YellowHealthbar = false;
        CantWalk = false;
        EnsureMounted();
    }

    private void EnsureMounted()
    {
        if (Mount == null && Body.IsHuman)
        {
            new Horse().Rider = this;
        }
    }

    private void EquipMilitiaItem(Item item)
    {
        item.Movable = false;
        AddItem(item);
    }

    private void UseMilitiaPotionIfNeeded()
    {
        if (Core.Now < _nextPotionUse)
        {
            return;
        }

        if (Poisoned)
        {
            CurePoison(this);
            FixedParticles(0x373A, 10, 15, 5012, EffectLayer.Waist);
            PlaySound(0x1E0);
            _nextPotionUse = Core.Now + TimeSpan.FromSeconds(12.0);
            return;
        }

        if (Hits < HitsMax * 0.45)
        {
            Heal(Utility.RandomMinMax(25, 40), this, false);
            FixedParticles(0x376A, 9, 32, 5005, EffectLayer.Waist);
            PlaySound(0x1F2);
            _nextPotionUse = Core.Now + TimeSpan.FromSeconds(12.0);
            return;
        }

        if (Stam < StamMax * 0.35)
        {
            Stam = StamMax;
            FixedParticles(0x375A, 10, 15, 5012, EffectLayer.Waist);
            PlaySound(0x1F7);
            _nextPotionUse = Core.Now + TimeSpan.FromSeconds(10.0);
        }
    }

    private void DeleteMount()
    {
        var mount = Mount;

        if (mount == null)
        {
            return;
        }

        mount.Rider = null;

        if (mount is Mobile mobile && !mobile.Deleted)
        {
            mobile.Delete();
        }
    }

    private bool ValidateCombatant(TownshipState township)
    {
        var combatant = Combatant;

        return combatant?.Deleted == false &&
               combatant.Alive &&
               combatant.Map == Map &&
               TownshipService.IsInsideTownshipEnvelope(township, combatant) &&
               IsMilitiaThreat(township, combatant);
    }

    private Mobile FindMilitiaThreat(TownshipState township)
    {
        Mobile best = null;
        var bestRank = double.MinValue;

        foreach (var mobile in GetMobilesInRange(ThreatScanRange))
        {
            if (!IsMilitiaThreat(township, mobile) ||
                !TownshipService.IsInsideTownshipEnvelope(township, mobile) ||
                !CanSee(mobile) ||
                !InLOS(mobile))
            {
                continue;
            }

            var rank = GetMilitiaThreatRank(mobile);

            if (rank > bestRank)
            {
                best = mobile;
                bestRank = rank;
            }
        }

        return best;
    }

    private static double GetMilitiaThreatRank(Mobile mobile)
    {
        var rank = mobile.Player ? 1000.0 : 100.0;

        if (mobile.Murderer)
        {
            rank += 250.0;
        }

        if (mobile.Criminal)
        {
            rank += 150.0;
        }

        return rank - mobile.Hits;
    }

    private static bool IsMilitiaThreat(TownshipState township, Mobile mobile)
    {
        if (township == null ||
            mobile == null ||
            mobile.Deleted ||
            !mobile.Alive ||
            mobile.AccessLevel > AccessLevel.Player ||
            mobile.Blessed ||
            mobile is TownshipPatrolGuard ||
            mobile is ITownshipServiceNpc)
        {
            return false;
        }

        if (mobile.Player)
        {
            return mobile.Criminal || mobile.Murderer;
        }

        if (mobile is not BaseCreature creature ||
            creature.Controlled ||
            creature.Summoned ||
            creature.IsInvulnerable)
        {
            return false;
        }

        return creature.Combatant is PlayerMobile || creature.AlwaysMurderer || creature.Karma < 0;
    }
}

[SerializationGenerator(0)]
public partial class TownshipBanker : Banker, ITownshipServiceNpc
{
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private string _townshipId;

    [SerializableField(1)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private string _serviceId;

    [Constructible]
    public TownshipBanker() : this(null, null)
    {
    }

    public TownshipBanker(string townshipId, string serviceId)
    {
        _townshipId = townshipId;
        _serviceId = serviceId;
        Title = "the banker";
        NormalizeRuntimeState();
        ApplyServiceMovement();
    }

    public TownshipState Township => TownshipServiceNpcUtility.GetTownship(_townshipId);
    public TownshipPaidServiceRecord Service => TownshipService.FindPaidService(Township, _serviceId);
    public bool ServiceActive => TownshipServiceNpcUtility.IsServiceActive(_townshipId, _serviceId);
    public override bool CanOpenDoors => false;
    public override bool IsActiveVendor => ServiceActive && base.IsActiveVendor;
    public override bool CheckVendorAccess(Mobile from) => ServiceActive && base.CheckVendorAccess(from);
    public override bool HandlesOnSpeech(Mobile from) => from.InRange(Location, 12) || base.HandlesOnSpeech(from);

    public void NormalizeRuntimeState() =>
        TownshipServiceNpcUtility.NormalizeRuntimeState(this, "the township banker", "the banker");

    public override void OnSingleClick(Mobile from)
    {
        TownshipServiceNpcUtility.ShowTownshipLabel(this, from, _townshipId);
        base.OnSingleClick(from);
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (TownshipService.CanManageTownship(Township, from))
        {
            BeginManagement(from);
            return;
        }

        base.OnDoubleClick(from);
    }

    public override void OnSpeech(SpeechEventArgs e)
    {
        if (!e.Handled && e.Mobile.InRange(Location, 12) && IsBankingKeyword(e) && !ServiceActive)
        {
            e.Handled = true;
            Say("Township banking services are currently unavailable.");
            return;
        }

        base.OnSpeech(e);
    }

    public override void AddCustomContextEntries(Mobile from, ref PooledRefList<ContextMenuEntry> list)
    {
        TownshipServiceNpcUtility.AddManagementEntry(from, ref list, this);

        if (ServiceActive)
        {
            base.AddCustomContextEntries(from, ref list);
        }
    }

    public void BeginManagement(Mobile from) => TownshipServiceNpcUtility.BeginManagement(this, from, _townshipId);

    public void ApplyServiceMovement()
    {
        NormalizeRuntimeState();
        TownshipServiceNpcUtility.ApplyMovement(this, _townshipId, _serviceId);
    }

    public void ApplyServiceGender(bool female)
    {
        NormalizeRuntimeState();
        TownshipServiceNpcUtility.ApplyGender(this, female);
    }

    [AfterDeserialization]
    private void AfterDeserialization()
    {
        NormalizeRuntimeState();
        ApplyServiceMovement();
    }

    protected override bool OnMove(Direction d) =>
        base.OnMove(d) && TownshipServiceNpcUtility.CheckHouseMove(this, d, _townshipId, _serviceId);

    public override void OnThink()
    {
        base.OnThink();
        TownshipServiceNpcUtility.CheckAnchorHouse(this, _townshipId, _serviceId, "Township banker");
    }

    public override void OnDeath(Container c)
    {
        TownshipServiceNpcUtility.MarkDeath(_townshipId, _serviceId, "Township banker");
        base.OnDeath(c);
    }

    public override void OnAfterDelete()
    {
        TownshipServiceNpcUtility.MarkDeleted(_townshipId, _serviceId, "Township banker");
        base.OnAfterDelete();
    }

    public void OnTownshipDeleted(TownshipState township) => Delete();

    private static bool IsBankingKeyword(SpeechEventArgs e)
    {
        for (var i = 0; i < e.Keywords.Length; i++)
        {
            if (e.Keywords[i] is >= 0x0000 and <= 0x0003)
            {
                return true;
            }
        }

        return false;
    }
}

[SerializationGenerator(0)]
public partial class TownshipMage : Mage, ITownshipServiceNpc
{
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private string _townshipId;

    [SerializableField(1)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private string _serviceId;

    [Constructible]
    public TownshipMage() : this(null, null)
    {
    }

    public TownshipMage(string townshipId, string serviceId)
    {
        _townshipId = townshipId;
        _serviceId = serviceId;
        NormalizeRuntimeState();
        ApplyServiceMovement();
    }

    public TownshipState Township => TownshipServiceNpcUtility.GetTownship(_townshipId);
    public TownshipPaidServiceRecord Service => TownshipService.FindPaidService(Township, _serviceId);
    public bool ServiceActive => TownshipServiceNpcUtility.IsServiceActive(_townshipId, _serviceId);
    public override bool CanOpenDoors => false;
    public override bool IsActiveVendor => ServiceActive && base.IsActiveVendor;
    public override bool CheckVendorAccess(Mobile from) => ServiceActive && base.CheckVendorAccess(from);

    public void NormalizeRuntimeState() =>
        TownshipServiceNpcUtility.NormalizeRuntimeState(this, "the township mage", "the mage");

    public override void OnSingleClick(Mobile from)
    {
        TownshipServiceNpcUtility.ShowTownshipLabel(this, from, _townshipId);
        base.OnSingleClick(from);
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (TownshipService.CanManageTownship(Township, from))
        {
            BeginManagement(from);
            return;
        }

        base.OnDoubleClick(from);
    }

    public override void AddCustomContextEntries(Mobile from, ref PooledRefList<ContextMenuEntry> list)
    {
        TownshipServiceNpcUtility.AddManagementEntry(from, ref list, this);

        if (ServiceActive)
        {
            base.AddCustomContextEntries(from, ref list);
        }
    }

    public void BeginManagement(Mobile from) => TownshipServiceNpcUtility.BeginManagement(this, from, _townshipId);

    public void ApplyServiceMovement()
    {
        NormalizeRuntimeState();
        TownshipServiceNpcUtility.ApplyMovement(this, _townshipId, _serviceId);
    }

    public void ApplyServiceGender(bool female)
    {
        NormalizeRuntimeState();
        TownshipServiceNpcUtility.ApplyGender(this, female);
    }

    [AfterDeserialization]
    private void AfterDeserialization()
    {
        NormalizeRuntimeState();
        ApplyServiceMovement();
    }

    protected override bool OnMove(Direction d) =>
        base.OnMove(d) && TownshipServiceNpcUtility.CheckHouseMove(this, d, _townshipId, _serviceId);

    public override void OnThink()
    {
        base.OnThink();
        TownshipServiceNpcUtility.CheckAnchorHouse(this, _townshipId, _serviceId, "Township mage");
    }

    public override void OnDeath(Container c)
    {
        TownshipServiceNpcUtility.MarkDeath(_townshipId, _serviceId, "Township mage");
        base.OnDeath(c);
    }

    public override void OnAfterDelete()
    {
        TownshipServiceNpcUtility.MarkDeleted(_townshipId, _serviceId, "Township mage");
        base.OnAfterDelete();
    }

    public void OnTownshipDeleted(TownshipState township) => Delete();
}

[SerializationGenerator(0)]
public partial class TownshipAlchemist : Alchemist, ITownshipServiceNpc
{
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private string _townshipId;

    [SerializableField(1)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private string _serviceId;

    [Constructible]
    public TownshipAlchemist() : this(null, null)
    {
    }

    public TownshipAlchemist(string townshipId, string serviceId)
    {
        _townshipId = townshipId;
        _serviceId = serviceId;
        NormalizeRuntimeState();
        ApplyServiceMovement();
    }

    public TownshipState Township => TownshipServiceNpcUtility.GetTownship(_townshipId);
    public TownshipPaidServiceRecord Service => TownshipService.FindPaidService(Township, _serviceId);
    public bool ServiceActive => TownshipServiceNpcUtility.IsServiceActive(_townshipId, _serviceId);
    public override bool CanOpenDoors => false;
    public override bool IsActiveVendor => ServiceActive && base.IsActiveVendor;
    public override bool CheckVendorAccess(Mobile from) => ServiceActive && base.CheckVendorAccess(from);

    public void NormalizeRuntimeState() =>
        TownshipServiceNpcUtility.NormalizeRuntimeState(this, "the township alchemist", "the alchemist");

    public override void OnSingleClick(Mobile from)
    {
        TownshipServiceNpcUtility.ShowTownshipLabel(this, from, _townshipId);
        base.OnSingleClick(from);
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (TownshipService.CanManageTownship(Township, from))
        {
            BeginManagement(from);
            return;
        }

        base.OnDoubleClick(from);
    }

    public override void AddCustomContextEntries(Mobile from, ref PooledRefList<ContextMenuEntry> list)
    {
        TownshipServiceNpcUtility.AddManagementEntry(from, ref list, this);

        if (ServiceActive)
        {
            base.AddCustomContextEntries(from, ref list);
        }
    }

    public void BeginManagement(Mobile from) => TownshipServiceNpcUtility.BeginManagement(this, from, _townshipId);

    public void ApplyServiceMovement()
    {
        NormalizeRuntimeState();
        TownshipServiceNpcUtility.ApplyMovement(this, _townshipId, _serviceId);
    }

    public void ApplyServiceGender(bool female)
    {
        NormalizeRuntimeState();
        TownshipServiceNpcUtility.ApplyGender(this, female);
    }

    [AfterDeserialization]
    private void AfterDeserialization()
    {
        NormalizeRuntimeState();
        ApplyServiceMovement();
    }

    protected override bool OnMove(Direction d) =>
        base.OnMove(d) && TownshipServiceNpcUtility.CheckHouseMove(this, d, _townshipId, _serviceId);

    public override void OnThink()
    {
        base.OnThink();
        TownshipServiceNpcUtility.CheckAnchorHouse(this, _townshipId, _serviceId, "Township alchemist");
    }

    public override void OnDeath(Container c)
    {
        TownshipServiceNpcUtility.MarkDeath(_townshipId, _serviceId, "Township alchemist");
        base.OnDeath(c);
    }

    public override void OnAfterDelete()
    {
        TownshipServiceNpcUtility.MarkDeleted(_townshipId, _serviceId, "Township alchemist");
        base.OnAfterDelete();
    }

    public void OnTownshipDeleted(TownshipState township) => Delete();
}

[SerializationGenerator(0)]
public partial class TownshipStablemaster : AnimalTrainer, ITownshipServiceNpc
{
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private string _townshipId;

    [SerializableField(1)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private string _serviceId;

    [Constructible]
    public TownshipStablemaster() : this(null, null)
    {
    }

    public TownshipStablemaster(string townshipId, string serviceId)
    {
        _townshipId = townshipId;
        _serviceId = serviceId;
        Title = "the stablemaster";
        NormalizeRuntimeState();
        ApplyServiceMovement();
    }

    public TownshipState Township => TownshipServiceNpcUtility.GetTownship(_townshipId);
    public TownshipPaidServiceRecord Service => TownshipService.FindPaidService(Township, _serviceId);
    public bool ServiceActive => TownshipServiceNpcUtility.IsServiceActive(_townshipId, _serviceId);
    public override bool CanOpenDoors => false;
    public override bool IsActiveVendor => ServiceActive && base.IsActiveVendor;
    public override bool CheckVendorAccess(Mobile from) => ServiceActive && base.CheckVendorAccess(from);
    public override bool HandlesOnSpeech(Mobile from) => ServiceActive && base.HandlesOnSpeech(from);

    public void NormalizeRuntimeState() =>
        TownshipServiceNpcUtility.NormalizeRuntimeState(this, "the township stablemaster", "the stablemaster");

    public override void OnSingleClick(Mobile from)
    {
        TownshipServiceNpcUtility.ShowTownshipLabel(this, from, _townshipId);
        base.OnSingleClick(from);
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (TownshipService.CanManageTownship(Township, from))
        {
            BeginManagement(from);
            return;
        }

        base.OnDoubleClick(from);
    }

    public override void AddCustomContextEntries(Mobile from, ref PooledRefList<ContextMenuEntry> list)
    {
        TownshipServiceNpcUtility.AddManagementEntry(from, ref list, this);

        if (ServiceActive)
        {
            base.AddCustomContextEntries(from, ref list);
        }
    }

    public void BeginManagement(Mobile from) => TownshipServiceNpcUtility.BeginManagement(this, from, _townshipId);

    public void ApplyServiceMovement()
    {
        NormalizeRuntimeState();
        TownshipServiceNpcUtility.ApplyMovement(this, _townshipId, _serviceId);
    }

    public void ApplyServiceGender(bool female)
    {
        NormalizeRuntimeState();
        TownshipServiceNpcUtility.ApplyGender(this, female);
    }

    [AfterDeserialization]
    private void AfterDeserialization()
    {
        NormalizeRuntimeState();
        ApplyServiceMovement();
    }

    protected override bool OnMove(Direction d) =>
        base.OnMove(d) && TownshipServiceNpcUtility.CheckHouseMove(this, d, _townshipId, _serviceId);

    public override void OnThink()
    {
        base.OnThink();
        TownshipServiceNpcUtility.CheckAnchorHouse(this, _townshipId, _serviceId, "Township stablemaster");
    }

    public override void OnDeath(Container c)
    {
        TownshipServiceNpcUtility.MarkDeath(_townshipId, _serviceId, "Township stablemaster");
        base.OnDeath(c);
    }

    public override void OnAfterDelete()
    {
        TownshipServiceNpcUtility.MarkDeleted(_townshipId, _serviceId, "Township stablemaster");
        base.OnAfterDelete();
    }

    public void OnTownshipDeleted(TownshipState township) => Delete();
}

[SerializationGenerator(0)]
public partial class TownshipInnKeeper : InnKeeper, ITownshipServiceNpc
{
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private string _townshipId;

    [SerializableField(1)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private string _serviceId;

    [Constructible]
    public TownshipInnKeeper() : this(null, null)
    {
    }

    public TownshipInnKeeper(string townshipId, string serviceId)
    {
        _townshipId = townshipId;
        _serviceId = serviceId;
        NormalizeRuntimeState();
        ApplyServiceMovement();
    }

    public TownshipState Township => TownshipServiceNpcUtility.GetTownship(_townshipId);
    public TownshipPaidServiceRecord Service => TownshipService.FindPaidService(Township, _serviceId);
    public bool ServiceActive => TownshipServiceNpcUtility.IsServiceActive(_townshipId, _serviceId);
    public override bool CanOpenDoors => false;
    public override bool IsActiveVendor => ServiceActive && base.IsActiveVendor;
    public override bool CheckVendorAccess(Mobile from) => ServiceActive && base.CheckVendorAccess(from);

    public void NormalizeRuntimeState() =>
        TownshipServiceNpcUtility.NormalizeRuntimeState(this, "the township innkeeper", "the innkeeper");

    public override void OnSingleClick(Mobile from)
    {
        TownshipServiceNpcUtility.ShowTownshipLabel(this, from, _townshipId);
        base.OnSingleClick(from);
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (TownshipService.CanManageTownship(Township, from))
        {
            BeginManagement(from);
            return;
        }

        base.OnDoubleClick(from);
    }

    public override void AddCustomContextEntries(Mobile from, ref PooledRefList<ContextMenuEntry> list)
    {
        TownshipServiceNpcUtility.AddManagementEntry(from, ref list, this);

        if (ServiceActive)
        {
            base.AddCustomContextEntries(from, ref list);
        }
    }

    public void BeginManagement(Mobile from) => TownshipServiceNpcUtility.BeginManagement(this, from, _townshipId);

    public void ApplyServiceMovement()
    {
        NormalizeRuntimeState();
        TownshipServiceNpcUtility.ApplyMovement(this, _townshipId, _serviceId);
    }

    public void ApplyServiceGender(bool female)
    {
        NormalizeRuntimeState();
        TownshipServiceNpcUtility.ApplyGender(this, female);
    }

    [AfterDeserialization]
    private void AfterDeserialization()
    {
        NormalizeRuntimeState();
        ApplyServiceMovement();
    }

    protected override bool OnMove(Direction d) =>
        base.OnMove(d) && TownshipServiceNpcUtility.CheckHouseMove(this, d, _townshipId, _serviceId);

    public override void OnThink()
    {
        base.OnThink();
        TownshipServiceNpcUtility.CheckAnchorHouse(this, _townshipId, _serviceId, "Township innkeeper");
    }

    public override void OnDeath(Container c)
    {
        TownshipServiceNpcUtility.MarkDeath(_townshipId, _serviceId, "Township innkeeper");
        base.OnDeath(c);
    }

    public override void OnAfterDelete()
    {
        TownshipServiceNpcUtility.MarkDeleted(_townshipId, _serviceId, "Township innkeeper");
        base.OnAfterDelete();
    }

    public void OnTownshipDeleted(TownshipState township) => Delete();
}
