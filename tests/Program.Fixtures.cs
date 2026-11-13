using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using PatchworkIsland;

namespace PatchworkIsland.Tests;

internal static partial class Program
{
    private static MissionService CompleteOriginalExtras(MissionService service)
    {
        var m05 = service.StartMission("M05");
        if (!m05.Success) throw new Exception("M05 setup start failed: " + m05.Message);
        for (int gate = 1; gate <= 4; gate++)
            if (!service.AdvanceCheckpoint(gate).Success)
                throw new Exception($"M05 setup gate {gate} failed");
        if (!service.CompleteActive(0).Success) throw new Exception("M05 setup completion failed");

        var m06 = service.StartMission("M06");
        if (!m06.Success) throw new Exception("M06 setup start failed: " + m06.Message);
        if (!service.CollectPickup("blue_parcel_0", "blue_parcel", "M06", 2).Success)
            throw new Exception("M06 setup blue 1 failed");
        if (!service.CollectPickup("blue_parcel_1", "blue_parcel", "M06", 2).Success)
            throw new Exception("M06 setup blue 2 failed");
        if (!service.CollectPickup("orange_parcel_0", "orange_parcel", "M06", 1).Success)
            throw new Exception("M06 setup orange failed");
        if (!service.DeliverBlueParcels().Success) throw new Exception("M06 setup blue delivery failed");
        if (!service.DeliverOrangeParcel().Success) throw new Exception("M06 setup orange delivery failed");
        if (!service.CompleteActive(0).Success) throw new Exception("M06 setup completion failed");

        var m09 = service.StartMission("M09");
        if (!m09.Success) throw new Exception("M09 setup start failed: " + m09.Message);
        for (int gate = 1; gate <= 4; gate++)
            if (!service.AdvanceCheckpoint(gate).Success)
                throw new Exception($"M09 setup gate {gate} failed");
        if (!service.CompleteActive(0).Success) throw new Exception("M09 setup completion failed");
        return service;
    }

    private static MissionService CompleteOriginalTen(MissionService service) =>
        CompleteOriginalExtras(CompleteM10(service));

    private static MissionService CompleteM11(MissionService service)
    {
        var started = service.StartMission("M11");
        if (!started.Success) throw new Exception("M11 setup start failed: " + started.Message);
        foreach (string npc in ChapterTwoRules.CardNpcIds)
        {
            var result = service.HandWelcomeCard(npc, ChapterTwoRules.CardFor(npc));
            if (!result.Success) throw new Exception($"M11 setup card for {npc} failed: " + result.Message);
        }
        if (!service.Data.IsCompleted("M11")) throw new Exception("M11 setup did not complete");
        return service;
    }

    private static MissionService CompleteM12(MissionService service)
    {
        var started = service.StartMission("M12");
        if (!started.Success) throw new Exception("M12 setup start failed: " + started.Message);
        foreach (var order in ChapterTwoRules.BakeryOrders)
        {
            var result = service.ServeBakeryOrder(order.CustomerId, order.Bread, order.Cakes);
            if (!result.Success) throw new Exception($"M12 setup {order.CustomerId} failed: " + result.Message);
        }
        if (!service.Data.IsCompleted("M12")) throw new Exception("M12 setup did not complete");
        return service;
    }

    private static MissionService CompleteM13(MissionService service)
    {
        var started = service.StartMission("M13");
        if (!started.Success) throw new Exception("M13 setup start failed: " + started.Message);
        foreach (string book in ChapterTwoRules.BookIds)
        {
            if (!service.CollectBook(book).Success)
                throw new Exception($"M13 setup collect {book} failed");
            if (!service.ShelveBook(book, book).Success)
                throw new Exception($"M13 setup shelve {book} failed");
        }
        if (!service.Data.IsCompleted("M13")) throw new Exception("M13 setup did not complete");
        return service;
    }

    private static MissionService CompleteM14(MissionService service)
    {
        var started = service.StartMission("M14");
        if (!started.Success) throw new Exception("M14 setup start failed: " + started.Message);
        foreach (var socket in ChapterTwoRules.SignSockets)
        {
            var result = service.InstallRoadSign(socket.SocketId, socket.SignId, socket.Direction);
            if (!result.Success) throw new Exception($"M14 setup {socket.SocketId} failed: " + result.Message);
        }
        if (!service.Data.IsCompleted("M14")) throw new Exception("M14 setup did not complete");
        return service;
    }

    private static MissionService CompleteM15(MissionService service)
    {
        var started = service.StartMission("M15");
        if (!started.Success) throw new Exception("M15 setup start failed: " + started.Message);
        for (int i = 0; i < ChapterTwoRules.GardenPattern.Length; i++)
            if (!service.PlantGarden(i, ChapterTwoRules.GardenPattern[i]).Success)
                throw new Exception($"M15 setup pad {i + 1} failed");
        if (!service.Data.IsCompleted("M15")) throw new Exception("M15 setup did not complete");
        return service;
    }

    private static MissionService CompleteFirstHalf(MissionService service) =>
        CompleteM15(CompleteM14(CompleteM13(CompleteM12(CompleteM11(CompleteOriginalTen(service))))));

    private static MissionService CompleteM16(MissionService service)
    {
        var started = service.StartMission("M16");
        if (!started.Success) throw new Exception("M16 setup start failed: " + started.Message);
        if (!service.LoadDeliveryParcels().Success)
            throw new Exception("M16 setup parcel loading failed");
        foreach (string npc in FinaleRules.DeliveryRecipients)
            if (!service.DeliverParcel(npc, npc).Success)
                throw new Exception($"M16 setup delivery to {npc} failed");
        if (!service.Data.IsCompleted("M16")) throw new Exception("M16 setup did not complete");
        return service;
    }

    private static MissionService CompleteM17(MissionService service)
    {
        var started = service.StartMission("M17");
        if (!started.Success) throw new Exception("M17 setup start failed: " + started.Message);
        foreach (var pair in FinaleRules.MarketRequired)
            for (int i = 0; i < pair.Value; i++)
                if (!service.AddMarketItem(pair.Key).Success)
                    throw new Exception($"M17 setup add {pair.Key} failed");
        var completion = service.FinaliseMarket();
        if (!completion.Success || completion.Completion == null)
            throw new Exception("M17 setup completion failed: " + completion.Message);
        return service;
    }

    private static MissionService CompleteM18(MissionService service)
    {
        var started = service.StartMission("M18");
        if (!started.Success) throw new Exception("M18 setup start failed: " + started.Message);
        if (!service.InspectVehicle().Success) throw new Exception("M18 setup inspect failed");
        foreach (var pair in new[]
                 {
                     ("tyre", "part_tyre_0"), ("tyre", "part_tyre_1"),
                     ("lamp", "part_lamp_0"), ("battery", "part_battery_0"),
                 })
        {
            if (!service.CollectRepairPart(pair.Item1, pair.Item2).Success)
                throw new Exception($"M18 setup collect {pair.Item1} failed");
            if (!service.FitRepairPart(pair.Item1).Success)
                throw new Exception($"M18 setup fit {pair.Item1} failed");
        }
        if (!service.Data.IsCompleted("M18")) throw new Exception("M18 setup did not complete");
        return service;
    }

    private static MissionService CompleteM19(MissionService service)
    {
        var started = service.StartMission("M19");
        if (!started.Success) throw new Exception("M19 setup start failed: " + started.Message);
        for (int gate = 1; gate <= FinaleRules.RallyGateCount; gate++)
            if (!service.AdvanceCheckpoint(gate).Success)
                throw new Exception($"M19 setup gate {gate} failed");
        var completion = service.CompleteActive(0);
        if (!completion.Success) throw new Exception("M19 setup completion failed: " + completion.Message);
        return service;
    }

    private static MissionService CompleteM20(MissionService service)
    {
        var started = service.StartMission("M20");
        if (!started.Success) throw new Exception("M20 setup start failed: " + started.Message);
        foreach (string npc in FinaleRules.FestivalInvitees)
            if (!service.InviteForFestival(npc).Success)
                throw new Exception($"M20 setup invite {npc} failed");
        for (int i = 0; i < FinaleRules.FestivalTableCount; i++)
            if (!service.PlaceFestivalTable(i).Success)
                throw new Exception($"M20 setup table {i} failed");
        for (int i = 0; i < FinaleRules.FestivalLanternCount; i++)
            if (!service.HangFestivalLantern(i).Success)
                throw new Exception($"M20 setup lantern {i} failed");
        if (!service.Data.IsCompleted("M20")) throw new Exception("M20 setup did not complete");
        return service;
    }

    private static GameData BuildOriginalTenSave()
    {
        var data = new GameData { Coins = 130, CelebrationSeen = true };
        foreach (string id in MissionRules.OriginalMissionIds)
            data.CompletedMissions.Add(id);
        data.BridgePieces.AddRange(BuildRules.BridgeSockets);
        data.SwingPieces.AddRange(BuildRules.SwingSockets);
        data.ParkPlacements.Add(new PlacedItem("bench", "park0"));
        data.ParkPlacements.Add(new PlacedItem("flowerbed", "park1"));
        data.ParkPlacements.Add(new PlacedItem("lamp", "park2"));
        foreach (var pair in FurnitureRules.Required)
            data.FurnitureOwned[pair.Key] = pair.Value;
        return data;
    }

    private static MissionService CompleteM01(MissionService service)
    {
        service.StartMission("M01");
        for (int i = 0; i < 3; i++)
            service.CollectPickup($"apple_{i}", "apple", "M01", 3);
        var result = service.DeliverApples();
        if (!result.Success) throw new Exception("M01 setup completion failed: " + result.Message);
        return service;
    }

    private static MissionService CompleteM07(MissionService service)
    {
        CompleteBridge(service);
        var started = service.StartMission("M07");
        if (!started.Success) throw new Exception("M07 setup start failed: " + started.Message);
        foreach (string id in new[] { "s1_rope_l", "s1_rope_r", "s2_rope_l", "s2_rope_r" })
            service.PlaceSwingPiece("rope", id);
        service.PlaceSwingPiece("seat", "s1_seat");
        service.PlaceSwingPiece("seat", "s2_seat");
        var result = service.CompleteActive(0);
        if (!result.Success) throw new Exception("M07 setup completion failed: " + result.Message);
        return service;
    }

    private static MissionService CompleteM08(MissionService service)
    {
        CompleteM07(service);
        var started = service.StartMission("M08");
        if (!started.Success) throw new Exception("M08 setup start failed: " + started.Message);
        ParkRules.Place(service.Data, "bench", "park0");
        ParkRules.Place(service.Data, "flowerbed", "park1");
        ParkRules.Place(service.Data, "lamp", "park2");
        var result = service.CompleteActive(0);
        if (!result.Success) throw new Exception("M08 setup completion failed: " + result.Message);
        return service;
    }

    private static MissionService CompleteM05(MissionService service)
    {
        var started = service.StartMission("M05");
        if (!started.Success) throw new Exception("M05 setup start failed: " + started.Message);
        for (int gate = 1; gate <= 4; gate++)
            service.AdvanceCheckpoint(gate);
        var result = service.CompleteActive(0);
        if (!result.Success) throw new Exception("M05 setup completion failed: " + result.Message);
        return service;
    }

    private static MissionService CompleteM10(MissionService service)
    {
        CompleteM04(service);
        CompleteM08(service);
        var started = service.StartMission("M10");
        if (!started.Success) throw new Exception("M10 setup start failed: " + started.Message);
        foreach (var (item, required) in FurnitureRules.Required)
            for (int i = 0; i < required; i++)
                if (!FurnitureRules.Buy(service.Data, item).Success)
                    FurnitureRules.GrantFreeStarterSet(service.Data);
        if (!FurnitureRules.Place(service.Data, "table", "house0").Success) throw new Exception("place table");
        if (!FurnitureRules.Place(service.Data, "chair", "house1").Success) throw new Exception("place chair 1");
        if (!FurnitureRules.Place(service.Data, "chair", "house2").Success) throw new Exception("place chair 2");
        if (!FurnitureRules.Place(service.Data, "plant", "house3").Success) throw new Exception("place plant");
        var result = service.CompleteActive(0);
        if (!result.Success) throw new Exception("M10 setup completion failed: " + result.Message);
        return service;
    }

    private static MissionService CompleteM04(MissionService service)
    {
        CompleteM01(service);
        var started = service.StartMission("M04");
        if (!started.Success) throw new Exception("M04 setup start failed: " + started.Message);
        service.AddGrocery("bread");
        service.AddGrocery("bread");
        service.AddGrocery("milk");
        var result = service.DeliverGroceries();
        if (!result.Success) throw new Exception("M04 setup completion failed: " + result.Message);
        return service;
    }

    private static MissionService CompleteBridge(MissionService service)
    {
        CompleteM02(service);
        var started = service.StartMission("M03");
        if (!started.Success) throw new Exception("M03 setup start failed: " + started.Message);
        for (int i = 0; i < 3; i++)
            service.PlaceBridgePiece("support", $"support_{i}");
        for (int i = 0; i < 4; i++)
            service.PlaceBridgePiece("plank", $"plank_{i}");
        var result = service.CompleteActive(0);
        if (!result.Success) throw new Exception("M03 setup completion failed: " + result.Message);
        return service;
    }

    private static MissionService CompleteM02(MissionService service)
    {
        var started = service.StartMission("M02");
        if (!started.Success) throw new Exception("M02 setup start failed: " + started.Message);
        for (int i = 0; i < 5; i++)
            service.CollectPickup($"wood_{i}", "wood", "M02", 5);
        for (int i = 0; i < 3; i++)
            service.CollectPickup($"stone_{i}", "stone", "M02", 3);
        var result = service.SubmitSupplies();
        if (!result.Success) throw new Exception("M02 setup completion failed: " + result.Message);
        return service;
    }

}
