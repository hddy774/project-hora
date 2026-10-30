namespace Chronicles.Game.Inventory;

public static partial class InventoryEngine
{
    internal sealed partial class OwnedTransaction
    {
        // The caller supplies the unchanged reference fingerprint. The default JSON factory does not select this
        // prototype; explicit typed-copy experiments may call it.
        private static InventoryState CreateTypedProjectedSnapshot(GameState world, string baseFingerprint)
        {
            ArgumentNullException.ThrowIfNull(world); ArgumentNullException.ThrowIfNull(baseFingerprint);
            var copy = new InventoryState
            {
                BaseFingerprint = baseFingerprint, Month = world.Month, NextId = world.NextId, Revision = world.Revision,
                Items = world.Items.Select(CopyOwnedItem).ToList(), People = world.People.Select(CopyOwnedPerson).ToList(),
                Materials = CopyOwnedMaterials(world.Materials), Ledger = CopyOwnedLedger(world.Inventory)
            };
            Validate(copy); return copy;
        }

        // The other Person properties deliberately retain normal new Person() defaults, exactly like the JSON projection.
        private static Person CopyOwnedPerson(Person source) => new()
        {
            Slot = source.Slot, SlotGeneration = source.SlotGeneration, Id = source.Id, BodyId = source.BodyId,
            SoulId = source.SoulId, Status = source.Status, Place = source.Place, Money = source.Money,
            Health = source.Health, Work = source.Work, Skills = new(source.Skills, StringComparer.Ordinal),
            Equipment = new(source.Equipment), Action = source.Action, Due = source.Due, ActionTarget = source.ActionTarget
        };

        private static Item CopyOwnedItem(Item source) => new()
        {
            Id = source.Id, Definition = source.Definition, Owner = source.Owner, Place = source.Place,
            Quantity = source.Quantity, Quality = source.Quality, Damage = source.Damage, Defects = source.Defects,
            Container = source.Container, Reserved = source.Reserved, BoundSoul = source.BoundSoul, Source = source.Source
        };

        private static SortedDictionary<string, MaterialFlow> CopyOwnedMaterials(SortedDictionary<string, MaterialFlow> source)
        {
            var copy = new SortedDictionary<string, MaterialFlow>(StringComparer.Ordinal);
            foreach (var (key, value) in source)
                copy.Add(key, new() { Opening = value.Opening, Extraction = value.Extraction, Production = value.Production,
                    Consumption = value.Consumption, Loss = value.Loss });
            return copy;
        }

        private static InventoryLedger CopyOwnedLedger(InventoryLedger source)
        {
            var receipts = new SortedDictionary<string, InventoryReceipt>(StringComparer.Ordinal);
            foreach (var (key, value) in source.Receipts) receipts.Add(key, value with { });
            return new()
            {
                NextEventSequence = source.NextEventSequence, Initialized = source.Initialized,
                BaseCarryCapacity = source.BaseCarryCapacity, ProductionWork = source.ProductionWork,
                ProfessionalWork = source.ProfessionalWork, ExtractionWorkPerUnit = source.ExtractionWorkPerUnit,
                Metadata = source.Metadata.Select(CopyOwnedMetadata).ToList(),
                Reservations = source.Reservations.Select(value => new AssetReservation { Id = value.Id, Actor = value.Actor,
                    Purpose = value.Purpose, Items = new(value.Items), Active = value.Active }).ToList(),
                Labor = source.Labor.Select(value => new LaborReservation { Id = value.Id, Person = value.Person,
                    Purpose = value.Purpose, Start = value.Start, End = value.End, Units = value.Units,
                    Facility = value.Facility, FacilityCapacity = value.FacilityCapacity, Active = value.Active }).ToList(),
                Jobs = source.Jobs.Select(CopyOwnedJob).ToList(), Contracts = source.Contracts.Select(CopyOwnedContract).ToList(),
                Sources = source.Sources.Select(value => new NaturalStock { Id = value.Id, Place = value.Place,
                    Definition = value.Definition, Activity = value.Activity, Remaining = value.Remaining, Capacity = value.Capacity,
                    EcologyWeight = value.EcologyWeight, Regeneration = value.Regeneration, LastMonth = value.LastMonth, Origin = value.Origin }).ToList(),
                Effects = source.Effects.Select(value => value with { }).ToList(),
                Appraisals = source.Appraisals.Select(value => value with { }).ToList(),
                Observations = source.Observations.Select(value => value with { }).ToList(),
                Arrays = source.Arrays.Select(value => new ArrayInstallation { Id = value.Id, Item = value.Item,
                    Owner = value.Owner, Place = value.Place, Range = value.Range, Condition = value.Condition,
                    Active = value.Active, PaidUntil = value.PaidUntil }).ToList(),
                Bindings = source.Bindings.Select(value => value with { }).ToList(),
                Events = source.Events.Select(value => value with { }).ToList(), Receipts = receipts
            };
        }

        private static ItemMetadata CopyOwnedMetadata(ItemMetadata source) => new()
        {
            Item = source.Item, Custodian = source.Custodian, Carrier = source.Carrier, UseRights = new(source.UseRights),
            Provenance = new(source.Provenance), Capacity = source.Capacity, AllowedContents = new(source.AllowedContents),
            AllowNestedContainers = source.AllowNestedContainers
        };

        private static ProductionJob CopyOwnedJob(ProductionJob source) => new()
        {
            Id = source.Id, Actor = source.Actor, Recipe = source.Recipe, Status = source.Status, Place = source.Place,
            Due = source.Due, Reservation = source.Reservation, Labor = source.Labor, KnowledgeActivity = source.KnowledgeActivity,
            Target = source.Target, Source = source.Source, OutputDefinition = source.OutputDefinition,
            OutputQuantity = source.OutputQuantity, Inputs = new(source.Inputs),
            Consumed = source.Consumed.Select(value => value with { }).ToList(), Quality = source.Quality,
            Damage = source.Damage, Defects = source.Defects, BoundSoul = source.BoundSoul, Provenance = new(source.Provenance)
        };

        private static InventoryContract CopyOwnedContract(InventoryContract source) => new()
        {
            Id = source.Id, Kind = source.Kind, Status = source.Status, AdvertisedDefinition = source.AdvertisedDefinition,
            AdvertisedName = source.AdvertisedName, AdvertisedQuantity = source.AdvertisedQuantity,
            Seller = source.Seller, Buyer = source.Buyer, Carrier = source.Carrier, Item = source.Item,
            Reservation = source.Reservation, Labor = source.Labor, PaidAmount = source.PaidAmount,
            PaidQuantity = source.PaidQuantity, PaidQuality = source.PaidQuality, EndedMonth = source.EndedMonth,
            Price = source.Price, Escrow = source.Escrow, Fee = source.Fee, Due = source.Due, Origin = source.Origin,
            Destination = source.Destination
        };
    }
}
