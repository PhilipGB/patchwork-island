using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace PatchworkIsland;

/// <summary>
/// Verifies authored quantities against executable rules. This is a release
/// contract, not another mission interpreter or an independent balance table.
/// </summary>
public static class MissionMechanicContract
{
    public static bool Validate(MissionCatalog catalog, out string error)
    {
        try
        {
            JsonElement M(string id) => catalog.Get(id).Mechanics;
            int N(JsonElement value, string field) => value.GetProperty(field).GetInt32();
            void Equal(int actual, int expected, string field)
            { if (actual != expected) throw new InvalidOperationException(field + " disagrees with runtime rules"); }
            void Sequence(JsonElement values, IEnumerable<string> expected, string field)
            {
                if (!values.EnumerateArray().Select(v => v.GetString()).SequenceEqual(expected))
                    throw new InvalidOperationException(field + " disagrees with runtime rules");
            }
            void Map(JsonElement values, IEnumerable<KeyValuePair<string, int>> expected, string field)
            { foreach (var pair in expected) Equal(N(values, pair.Key), pair.Value, field + "." + pair.Key); }
            int apples = N(M("M01"), "required");
            var data = new GameData(); data.Inventory["apple"] = apples;
            if (!MissionRules.CanDeliverApples(data)) throw new InvalidOperationException("M01 required");
            data.Inventory["apple"] = apples - 1;
            if (MissionRules.CanDeliverApples(data)) throw new InvalidOperationException("M01 required boundary");
            var supplies = M("M02").GetProperty("requirements");
            int wood = N(supplies, "wood"), stone = N(supplies, "stone");
            Equal(N(M("M02"), "total"), wood + stone, "M02 total");
            data.Inventory["wood"] = wood; data.Inventory["stone"] = stone;
            if (!MissionRules.CanSubmitSupplies(data)) throw new InvalidOperationException("M02 requirements");
            data.Inventory["wood"]--;
            if (MissionRules.CanSubmitSupplies(data)) throw new InvalidOperationException("M02 wood boundary");
            data.Inventory["wood"]++; data.Inventory["stone"]--;
            if (MissionRules.CanSubmitSupplies(data)) throw new InvalidOperationException("M02 stone boundary");
            Equal(N(M("M03").GetProperty("parts"), "support"), BuildRules.BridgeSockets.Count(s => s.StartsWith("support")), "M03 supports");
            Equal(N(M("M03").GetProperty("parts"), "plank"), BuildRules.BridgeSockets.Count(s => s.StartsWith("plank")), "M03 planks");
            Equal(N(M("M03"), "socketCount"), BuildRules.BridgeSockets.Length, "M03 sockets");
            Equal(N(M("M04"), "budget"), BudgetRules.GroceryBudget, "M04 budget");
            foreach (var item in new[] { "bread", "milk" }) Equal(N(M("M04").GetProperty("prices"), item), BudgetRules.GroceryPrice(item), "M04 price " + item);
            int bread = N(M("M04").GetProperty("required"), "bread"), milk = N(M("M04").GetProperty("required"), "milk");
            if (!BudgetRules.IsGroceryCorrect(bread, milk) || BudgetRules.IsGroceryCorrect(bread + 1, milk) || BudgetRules.IsGroceryCorrect(bread, milk + 1)) throw new InvalidOperationException("M04 list");
            Equal(N(M("M04"), "expectedTotal"), BudgetRules.GroceryTotal(bread, milk), "M04 total");
            Equal(N(M("M04"), "expectedChange"), BudgetRules.GroceryChange(bread, milk), "M04 change");
            foreach (var id in new[] { "M05", "M09", "M19" })
            {
                var mechanic = M(id);
                double limit = mechanic.GetProperty("optionalBonusTimeSeconds").GetDouble();
                Equal(N(mechanic, "optionalBonusCoins"), MissionRules.OptionalRaceBonus(id, limit), id + " bonus");
                Equal(MissionRules.OptionalRaceBonus(id, limit + .001), 0, id + " bonus boundary");
                if (mechanic.GetProperty("requiredTimeLimitSeconds").ValueKind != JsonValueKind.Null) throw new InvalidOperationException(id + " must remain untimed");
                int gates = id == "M19" ? N(mechanic, "gateCount") : mechanic.GetProperty("checkpoints").GetArrayLength();
                var race = new GameData(); race.SetMissionCounter(id, "checkpoint", gates);
                if (!MissionRules.IsObjectiveComplete(race, id)) throw new InvalidOperationException(id + " gate count");
                race.SetMissionCounter(id, "checkpoint", gates - 1);
                if (MissionRules.IsObjectiveComplete(race, id)) throw new InvalidOperationException(id + " gate boundary");
            }
            foreach (var delivery in M("M06").GetProperty("deliveries").EnumerateArray())
            {
                string key = delivery.GetProperty("item").GetString() == "blue_parcel" ? "blueDelivered" : "orangeDelivered";
                var parcels = new GameData(); parcels.SetMissionCounter("M06", "blueDelivered", 2); parcels.SetMissionCounter("M06", "orangeDelivered", 1);
                parcels.SetMissionCounter("M06", key, N(delivery, "count") - 1);
                if (MissionRules.IsObjectiveComplete(parcels, "M06")) throw new InvalidOperationException("M06 delivery quantity");
            }
            Equal(N(M("M07").GetProperty("parts"), "rope"), BuildRules.SwingSockets.Count(s => s.Contains("_rope_")), "M07 ropes");
            Equal(N(M("M07").GetProperty("parts"), "seat"), BuildRules.SwingSockets.Count(s => s.EndsWith("_seat")), "M07 seats");
            Equal(N(M("M08"), "budget"), BudgetRules.ParkBudget, "M08 budget");
            foreach (var item in ParkRules.Items) Equal(N(M("M08").GetProperty("prices"), item), BudgetRules.ParkPrice(item), "M08 price " + item);
            Map(M("M10").GetProperty("required"), FurnitureRules.Required, "M10 required");
            foreach (var item in FurnitureRules.Items) Equal(N(M("M10").GetProperty("prices"), item), BudgetRules.FurniturePrice(item), "M10 price " + item);
            Sequence(M("M11").GetProperty("targets"), ChapterTwoRules.CardNpcIds, "M11 targets");
            var orders = M("M12").GetProperty("orders").EnumerateArray().ToArray();
            Equal(orders.Length, ChapterTwoRules.BakeryOrders.Count, "M12 orders");
            for (int i = 0; i < orders.Length; i++)
            {
                Equal(N(orders[i], "bread"), ChapterTwoRules.BakeryOrders[i].Bread, "M12 bread");
                Equal(N(orders[i], "cakes"), ChapterTwoRules.BakeryOrders[i].Cakes, "M12 cakes");
            }
            Sequence(M("M13").GetProperty("books"), ChapterTwoRules.BookIds.Select(s => s.ToUpperInvariant()), "M13 books");
            if (!M("M14").GetProperty("signs").EnumerateArray().Select(v => v.GetProperty("id").GetString()).SequenceEqual(ChapterTwoRules.SignIds)) throw new InvalidOperationException("M14 signs");
            Sequence(M("M15").GetProperty("pattern"), ChapterTwoRules.GardenPattern, "M15 pattern");
            Sequence(M("M16").GetProperty("recipients"), FinaleRules.DeliveryRecipients, "M16 recipients");
            Equal(N(M("M17"), "budget"), FinaleRules.MarketBudget, "M17 budget");
            Map(M("M17").GetProperty("prices"), FinaleRules.MarketPrices, "M17 prices");
            Map(M("M17").GetProperty("required"), FinaleRules.MarketRequired, "M17 list");
            Equal(N(M("M18").GetProperty("parts"), "tyre"), FinaleRules.RequiredTyres, "M18 tyres");
            Equal(N(M("M18").GetProperty("parts"), "lamp"), FinaleRules.RequiredLamps, "M18 lamp");
            Equal(N(M("M18").GetProperty("parts"), "battery"), FinaleRules.RequiredBatteries, "M18 battery");
            Equal(N(M("M20"), "invitations"), FinaleRules.FestivalInvitees.Length, "M20 invitations");
            Equal(N(M("M20"), "tables"), FinaleRules.FestivalTableCount, "M20 tables");
            Equal(N(M("M20"), "lanterns"), FinaleRules.FestivalLanternCount, "M20 lanterns");
            Equal(N(M("M21").GetProperty("items"), "safety_cone"), RoadworksRules.ConeCount, "M21 cones");
            Equal(N(M("M21").GetProperty("items"), "barrier_panel"), RoadworksRules.PanelCount, "M21 panels");
            Equal(N(M("M21").GetProperty("items"), "toolbox"), RoadworksRules.ToolboxCount, "M21 toolbox");
            Sequence(M("M21").GetProperty("order"), RoadworksRules.CorrectOrder, "M21 order");
            error = ""; return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException or NullReferenceException or FormatException)
        { error = ex.Message; return false; }
    }
}
