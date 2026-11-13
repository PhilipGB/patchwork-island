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
    private static void CommunityTests(MissionCatalog catalog, CityMissionCatalog cityCatalog, string root, string json)
    {
        Run("chapter two unlocks only after all ten original jobs", () =>
        {
            var s = Fresh(catalog);
            Equal(MissionStatus.Locked, s.GetStatus("M11"), "M11 locked on a fresh save");
            s = CompleteM10(s);
            Equal(MissionStatus.Locked, s.GetStatus("M11"),
                "M11 still locked with races and post outstanding");
            s = CompleteOriginalExtras(s);
            Equal(10, s.Data.CompletedMissions.Distinct().Count(), "all ten original jobs recorded");
            Equal(MissionStatus.Available, s.GetStatus("M11"), "M11 available after all ten");
            Equal(MissionStatus.Locked, s.GetStatus("M12"), "M12 waits for M11");
            Equal(MissionStatus.Locked, s.GetStatus("M13"), "M13 waits for M11");
        });

        Run("M11 grants cards once, refuses wrong names and pays 12 once", () =>
        {
            var s = CompleteOriginalTen(Fresh(catalog));
            int wallet = s.Data.Coins;
            Check(s.StartMission("M11").Success, "start M11");
            foreach (string npc in ChapterTwoRules.CardNpcIds)
                Equal(1, s.Data.GetMissionCounter("M11", $"card_{npc}"), $"{npc} card granted");
            Equal(1, s.Data.GetMissionCounter("M11", "stock_granted"), "stock recorded");
            Equal(wallet, s.Data.Coins, "welcome cards cost no personal coins");

            var wrong = s.HandWelcomeCard("ada", "card_ollie");
            Check(!wrong.Success, "wrong card refused");
            Equal(1, s.Data.GetMissionCounter("M11", "card_ollie"), "wrong card not consumed");
            Equal(0, s.Data.GetMissionCounter("M11", "delivered_ada"), "wrong card not delivered");
            Equal(wallet, s.Data.Coins, "wrong card spends nothing");

            var ada = s.HandWelcomeCard("ada", "card_ada");
            Check(ada.Success && ada.Completion == null, "first correct card is a partial step");
            Equal(0, s.Data.GetMissionCounter("M11", "card_ada"), "card consumed");
            Equal(1, s.Data.GetMissionCounter("M11", "delivered_ada"), "Ada delivery recorded");
            Check(!s.HandWelcomeCard("ada", "card_ada").Success, "Ada cannot receive a second card");

            Check(s.HandWelcomeCard("ollie", "card_ollie").Success, "Ollie card delivered");
            var last = s.HandWelcomeCard("fern", "card_fern");
            Check(last.Success && last.Completion != null, "third card completes M11");
            Equal(12, last.Completion.CoinsAwarded, "M11 reward");
            Equal(wallet + 12, s.Data.Coins, "M11 pays exactly once");
            Check(last.Completion.OriginalChapterComplete, "original chapter stays complete");
            Check(!last.Completion.AllTwentyComplete, "twenty-job flag stays false in P13");
            Equal(MissionStatus.Completed, s.GetStatus("M11"), "M11 recorded complete");
            Check(!s.HandWelcomeCard("fern", "card_fern").Success, "repeat delivery rejected");
            Equal(wallet + 12, s.Data.Coins, "repeat awards nothing");
        });

        Run("M12 branches from M11 and rejects wrong quantities without loss", () =>
        {
            var s = CompleteM11(CompleteOriginalTen(Fresh(catalog)));
            Equal(MissionStatus.Available, s.GetStatus("M12"), "M12 after M11");
            Equal(MissionStatus.Available, s.GetStatus("M13"), "M13 after M11");
            Equal(MissionStatus.Locked, s.GetStatus("M14"), "M14 waits for M12");
            Equal(MissionStatus.Locked, s.GetStatus("M15"), "M15 waits for M13");
            int wallet = s.Data.Coins;

            Check(s.StartMission("M12").Success, "start M12");
            var wrong = s.ServeBakeryOrder("milo", 1, 1);
            Check(!wrong.Success, "wrong quantities refused");
            Equal(0, s.Data.GetMissionCounter("M12", "order_milo"), "wrong order not served");
            Equal(wallet, s.Data.Coins, "wrong order spends nothing");

            var milo = s.ServeBakeryOrder("milo", 2, 1);
            Check(milo.Success && milo.Completion == null, "Milo order served");
            Equal(1, s.Data.GetMissionCounter("M12", "order_milo"), "Milo recorded");
            Check(!s.ServeBakeryOrder("milo", 2, 1).Success, "Milo cannot be served twice");
            Check(s.ServeBakeryOrder("fern", 3, 2).Success, "Fern order served");
            var last = s.ServeBakeryOrder("ivy", 1, 3);
            Check(last.Success && last.Completion != null, "third order completes M12");
            Equal(14, last.Completion.CoinsAwarded, "M12 reward");
            Equal(wallet + 14, s.Data.Coins, "M12 pays exactly once");
            Equal(MissionStatus.Available, s.GetStatus("M14"), "M14 opens after M12");
            Equal(MissionStatus.Locked, s.GetStatus("M16"), "M16 still needs M15");
            Check(!s.ServeBakeryOrder("ivy", 1, 3).Success, "completed bakery refuses more orders");
            Equal(wallet + 14, s.Data.Coins, "repeat bakery award nothing");
        });

        Run("M13 keeps wrong-shelf books and pays 14 once", () =>
        {
            var s = CompleteM11(CompleteOriginalTen(Fresh(catalog)));
            int wallet = s.Data.Coins;
            Check(s.StartMission("M13").Success, "start M13");

            Check(!s.ShelveBook("sun", "sun").Success, "cannot shelve an uncollected book");
            Check(s.CollectBook("sun").Success, "collect SUN");
            Check(s.Data.CollectedPickups.Contains("book_sun"), "SUN pickup recorded");
            Equal(1, s.Data.GetMissionCounter("M13", "has_sun"), "SUN in hand");
            Check(!s.CollectBook("sun").Success, "SUN cannot be collected twice");

            var wrong = s.ShelveBook("sun", "moon");
            Check(!wrong.Success, "wrong shelf refused");
            Equal(1, s.Data.GetMissionCounter("M13", "has_sun"), "wrong shelf keeps SUN");
            Equal(0, s.Data.GetMissionCounter("M13", "shelf_moon"), "wrong shelf not filled");
            Equal(wallet, s.Data.Coins, "wrong shelf spends nothing");

            Check(s.ShelveBook("sun", "sun").Success, "SUN shelved correctly");
            Equal(0, s.Data.GetMissionCounter("M13", "has_sun"), "SUN leaves hand");
            Equal(1, s.Data.GetMissionCounter("M13", "shelf_sun"), "SUN shelf filled");
            foreach (string book in new[] { "moon", "tree" })
            {
                Check(s.CollectBook(book).Success, $"collect {book}");
                Check(s.ShelveBook(book, book).Success, $"shelve {book}");
            }
            Check(s.CollectBook("star").Success, "collect STAR");
            var last = s.ShelveBook("star", "star");
            Check(last.Success && last.Completion != null, "fourth book completes M13");
            Equal(14, last.Completion.CoinsAwarded, "M13 reward");
            Equal(wallet + 14, s.Data.Coins, "M13 pays exactly once");
            Equal(MissionStatus.Available, s.GetStatus("M15"), "M15 opens after M13");
            Equal(MissionStatus.Locked, s.GetStatus("M16"), "M16 still needs M14");
            Check(!s.ShelveBook("star", "star").Success, "completed library refuses more books");
            Equal(wallet + 14, s.Data.Coins, "repeat library award nothing");
        });

        Run("M14 validates sign and arrow, allows undo and pays 16 once", () =>
        {
            var s = CompleteM12(CompleteM11(CompleteOriginalTen(Fresh(catalog))));
            Equal(MissionStatus.Available, s.GetStatus("M14"), "M14 after M12");
            int wallet = s.Data.Coins;
            Check(s.StartMission("M14").Success, "start M14");

            Check(!s.InstallRoadSign("market", "workshop", "west").Success,
                "wrong sign refused");
            Check(!s.InstallRoadSign("market", "market", "north").Success,
                "wrong arrow refused");
            Equal(0, s.Data.GetMissionCounter("M14", "sign_market"), "failed fits install nothing");
            Equal(wallet, s.Data.Coins, "failed fits spend nothing");

            Check(s.InstallRoadSign("market", "market", "west").Success, "MARKET sign installed");
            Equal(1, s.Data.GetMissionCounter("M14", "sign_market"), "MARKET socket filled");
            Check(s.RemoveRoadSign("market").Success, "MARKET sign removable for retry");
            Equal(0, s.Data.GetMissionCounter("M14", "sign_market"), "MARKET socket cleared");
            Check(s.InstallRoadSign("market", "market", "west").Success, "MARKET reinstalled");

            Check(s.InstallRoadSign("workshop", "workshop", "north").Success,
                "WORKSHOP sign installed");
            var last = s.InstallRoadSign("green", "green", "south");
            Check(last.Success && last.Completion != null, "GREEN sign completes M14");
            Equal(16, last.Completion.CoinsAwarded, "M14 reward");
            Equal(wallet + 16, s.Data.Coins, "M14 pays exactly once");
            Equal(MissionStatus.Locked, s.GetStatus("M16"), "M16 still needs M15");
            Check(!s.InstallRoadSign("green", "green", "south").Success,
                "completed signs are permanent");
            Equal(wallet + 16, s.Data.Coins, "repeat sign award nothing");
        });

        Run("M15 repeats the colour pattern, allows correction and pays 16 once", () =>
        {
            var s = CompleteM13(CompleteM11(CompleteOriginalTen(Fresh(catalog))));
            Equal(MissionStatus.Available, s.GetStatus("M15"), "M15 after M13");
            int wallet = s.Data.Coins;
            Check(s.StartMission("M15").Success, "start M15");
            Equal(1, s.Data.GetMissionCounter("M15", "pad0"),
                "first RED example is planted on start");
            Equal(2, s.Data.GetMissionCounter("M15", "pad1"),
                "second BLUE example is planted on start");

            Check(s.PlantGarden(0, "blue").Success, "wrong colour is allowed as a correction step");
            Equal(2, s.Data.GetMissionCounter("M15", "pad0"), "pad shows blue");
            Check(s.PlantGarden(0, "empty").Success, "pad can be cleared");
            Equal(0, s.Data.GetMissionCounter("M15", "pad0"), "pad cleared");
            Check(s.StartMission("M12").Success, "switch away from M15");
            Check(s.StartMission("M15").Success, "switch back to M15");
            Equal(0, s.Data.GetMissionCounter("M15", "pad0"),
                "a cleared example pad is not silently re-seeded");
            Equal(2, s.Data.GetMissionCounter("M15", "pad1"), "the second example stays blue");
            Check(s.PlantGarden(0, "red").Success, "pad replanted red");
            Equal(wallet, s.Data.Coins, "garden uses unlimited job stock, not coins");
            Check(!MissionRules.IsObjectiveComplete(s.Data, "M15"), "one pad is not the pattern");
            Check(!s.PlantGarden(6, "red").Success, "non-existent pad is rejected");
            Check(s.StartMission("M15").Success, "M15 can be resumed after correction");
            Equal(wallet, s.Data.Coins, "garden corrections never touch the wallet");
        });

        Run("M15 final plant awards 16 once and opens M16 with M14", () =>
        {
            var s = CompleteM14(CompleteM12(CompleteM11(CompleteOriginalTen(Fresh(catalog)))));
            s = CompleteM13(s);
            int wallet = s.Data.Coins;
            Check(s.StartMission("M15").Success, "start M15 for final pattern");
            MissionCompletionResult last = null;
            for (int i = 0; i < ChapterTwoRules.GardenPattern.Length; i++)
            {
                var planted = s.PlantGarden(i, ChapterTwoRules.GardenPattern[i]);
                Check(planted.Success, $"plant pattern pad {i + 1}");
                if (planted.Completion != null) last = planted.Completion;
            }
            Check(last != null, "final pad completes M15");
            Equal(16, last.CoinsAwarded, "M15 reward");
            Equal(wallet + 16, s.Data.Coins, "M15 pays exactly once");
            Equal(MissionStatus.Available, s.GetStatus("M16"),
                "M16 opens after both M14 and M15");
            Check(!s.PlantGarden(0, "red").Success, "completed garden refuses more planting");
            Equal(wallet + 16, s.Data.Coins, "repeat garden award nothing");

            int before = s.Data.Coins;
            Check(!s.CompleteActive().Success, "no active mission remains after M15");
            Equal(before, s.Data.Coins, "no stray completion reward");
        });

        Run("M12 and M13 branch in either order", () =>
        {
            var s = CompleteM11(CompleteOriginalTen(Fresh(catalog)));
            s = CompleteM13(s);
            Equal(MissionStatus.Available, s.GetStatus("M12"), "M12 remains available after M13");
            Equal(MissionStatus.Available, s.GetStatus("M15"), "M15 opens from M13");
            Equal(MissionStatus.Locked, s.GetStatus("M14"), "M14 still waits for M12");
            s = CompleteM12(s);
            Equal(MissionStatus.Available, s.GetStatus("M14"), "M14 opens after the later M12");
            s = CompleteM14(s);
            s = CompleteM15(s);
            Equal(MissionStatus.Available, s.GetStatus("M16"),
                "M16 opens after both branches complete in reverse order");
            Equal(15, s.Data.CompletedMissions.Distinct().Count(),
                "fifteen completions after the first chapter-two half");
        });

    }
}
