using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;

namespace PatchworkIsland;

public partial class RuntimeSelfTest
{
    private async Task<bool> ChapterChecks()
    {
        if (ChapterPhase == 0) return await ChapterFreshChecks();
        return await ChapterResumeChecks();
    }

    private async Task<bool> ChapterFreshChecks()
    {
        Expect(!Main.Probe.Started, "chapter test starts on the title screen");
        var started = Main.Probe.StartNewGameFromTitleConfirmed(CharacterCustomisation.Boy);
        await Wait(0.8f);
        Expect(started.Success && Main.Probe.Started, "chapter test starts a fresh village");

        Main.Probe.PrepareChapterTwo();
        await Wait(0.4f);
        Expect(Main.Probe.Coins == 100, "chapter-two wallet starts at the prepared 100 coins");
        Expect(Main.Probe.SignSocketCount == 3, "three authored road-sign sockets exist");
        Expect(Main.Probe.GardenPadCount == 6, "six garden pads exist");
        Expect(Main.Probe.BakerySlotCount == 3, "three bakery display slots exist");
        Expect(Main.Probe.LibrarySlotCount == 4, "four library shelf slots exist");
        Expect(Main.Probe.BookPickupCount == 4, "four labelled books exist in the world");
        Expect(Main.Probe.MissionStatus("M11") == MissionStatus.Available,
            "M11 unlocks for a completed original chapter");
        Expect(Main.Probe.MissionStatus("M12") == MissionStatus.Locked,
            "M12 still waits for M11");

        Main.Probe.TeleportNearJobBoard();
        await Wait(0.3f);
        Expect(Main.Probe.NearestInteractableName == "JobBoardStation",
            "the job board is the nearest E interaction");
        await PressInteract();
        Expect(Main.Probe.ModalTitle == "Patchwork Jobs", "the real job board opens");
        Expect(Main.Probe.PressModalButton("ChapterOneTab"), "the Village jobs tab works");
        await Wait(0.25f);
        Expect(Main.Probe.ModalTitle == "Patchwork Jobs", "Village jobs remains the job board");
        Expect(Main.Probe.PressModalButton("ChapterTwoTab"), "the New adventures tab works");
        await Wait(0.25f);
        Expect(Main.Probe.PressModalButton("Start_M11"), "the real M11 Start button works");
        await Wait(0.4f);
        Expect(Main.Probe.ActiveMission == "M11", "M11 is the active job");
        Expect(Main.Probe.MissionCounter("M11", "stock_granted") == 1,
            "M11 grants its own card stock on start");
        Expect(Main.Probe.Coins == 100, "M11 card stock does not spend personal coins");

        await TalkTo("ada", "Ada");
        Expect(Main.Probe.PressModalButton("GiveCard_ollie"), "a wrong-card choice is available");
        await Wait(0.4f);
        Expect(Main.Probe.MissionCounter("M11", "delivered_ada") == 0,
            "wrong card is not delivered");
        Expect(Main.Probe.MissionCounter("M11", "card_ollie") == 1,
            "wrong card is not consumed");
        Expect(Main.Probe.Coins == 100, "wrong card spends nothing");
        Expect(!Main.Probe.ModalOpen, "failed card choice returns to the village");

        await TalkTo("ada", "Ada");
        Expect(Main.Probe.PressModalButton("GiveCard_ada"), "the matching card choice works");
        await Wait(0.4f);
        Expect(Main.Probe.MissionCounter("M11", "delivered_ada") == 1, "Ada delivery recorded");
        Expect(Main.Probe.MissionCounter("M11", "card_ada") == 0, "Ada card consumed");
        Expect(Main.Probe.Coins == 100, "successful card still spends nothing");
        Expect(Main.Probe.SaveNow(), "partial M11 progress saves");

        var partial = SaveService.Load(Main.Probe.SavePath, Main.Probe.Catalog);
        Expect(partial.Success, partial.Message);
        Expect(partial.Success && partial.Data.ActiveMission == "M11",
            "saved partial chapter job is M11");
        Expect(partial.Success && partial.Data.GetMissionCounter("M11", "delivered_ada") == 1,
            "saved partial chapter card is on disk");
        Expect(partial.Success && partial.Data.CompletedMissions.Count == 10,
            "saved file still holds the ten original completions");
        Expect(partial.Success && partial.Data.CelebrationSeen && !partial.Data.FestivalSeen,
            "saved file keeps the original celebration and hides the second finale");

        ChapterPhase = 1;
        Main.Probe.ReloadScene();
        return true;
    }

    private async Task<bool> ChapterResumeChecks()
    {
        Expect(!Main.Probe.Started, "chapter resume starts on the title screen");
        Expect(Main.Probe.CanContinue, "the partial chapter save offers Continue");
        Main.Probe.Continue();
        await Wait(0.5f);
        Expect(Main.Probe.Started && Main.Probe.Mode == "Walking", "Continue resumes the village");
        Expect(Main.Probe.ActiveMission == "M11", "Continue restores active M11");
        Expect(Main.Probe.MissionCounter("M11", "delivered_ada") == 1,
            "Continue restores Ada's delivered card");
        Expect(Main.Probe.MissionCounter("M11", "card_ollie") == 1 &&
               Main.Probe.MissionCounter("M11", "card_fern") == 1,
            "Continue restores the remaining card stock");
        Expect(Main.Probe.CelebrationSeen && !Main.Probe.FestivalSeen,
            "Continue keeps CelebrationSeen separate from FestivalSeen");
        Expect(Main.Probe.Coins == 100, "Continue restores the wallet");

        await TalkTo("ollie", "Ollie");
        Expect(Main.Probe.PressModalButton("GiveCard_ollie"), "Ollie's matching card is offered");
        await Wait(0.4f);
        Expect(Main.Probe.MissionCounter("M11", "delivered_ollie") == 1, "Ollie delivery recorded");
        await TalkTo("fern", "Fern");
        Expect(Main.Probe.PressModalButton("GiveCard_fern"), "Fern's matching card is offered");
        await Wait(0.45f);
        Expect(Main.Probe.MissionCompleted("M11"), "M11 completes through the real dialogue");
        Expect(Main.Probe.Coins == 112, "M11 reward is 12 coins");
        await CloseCompletion("M11");

        // M12: wrong bakery count, then all three real orders.
        Expect(await StartJobFromBoard("M12"), "M12 starts from the job board");
        await TalkTo("ada", "Ada");
        Expect(Main.Probe.PressModalButton("BakeryHelp"), "Ada's bakery option opens the counter");
        await Wait(0.35f);
        Expect(Main.Probe.ModalTitle == "Bakery Busy Day", "the real bakery modal opens");
        Expect(Main.Probe.PressModalButton("BreadAdd_milo"), "bakery stepper works");
        Expect(Main.Probe.PressModalButton("Serve_milo"), "bakery Serve button works");
        await Wait(0.3f);
        Expect(Main.Probe.MissionCounter("M12", "order_milo") == 0,
            "wrong bakery count is not accepted");
        Expect(Main.Probe.ModalLabelText("BakeryStatus").Contains("Milo"),
            "wrong bakery count gives a specific hint");
        Expect(Main.Probe.Coins == 112, "wrong bakery count spends nothing");
        Expect(Main.Probe.PressModalButton("BreadAdd_milo"), "second milo bread");
        Expect(Main.Probe.PressModalButton("CakeAdd_milo"), "milo cake");
        Expect(Main.Probe.PressModalButton("Serve_milo"), "Milo served");
        await Wait(0.3f);
        Expect(Main.Probe.MissionCounter("M12", "order_milo") == 1, "Milo order recorded");
        for (int i = 0; i < 3; i++) Expect(Main.Probe.PressModalButton("BreadAdd_fern"), $"fern bread {i}");
        for (int i = 0; i < 2; i++) Expect(Main.Probe.PressModalButton("CakeAdd_fern"), $"fern cake {i}");
        Expect(Main.Probe.PressModalButton("Serve_fern"), "Fern served");
        await Wait(0.25f);
        Expect(Main.Probe.PressModalButton("BreadAdd_ivy"), "ivy bread");
        for (int i = 0; i < 3; i++) Expect(Main.Probe.PressModalButton("CakeAdd_ivy"), $"ivy cake {i}");
        Expect(Main.Probe.PressModalButton("Serve_ivy"), "Ivy served");
        await Wait(0.45f);
        Expect(Main.Probe.MissionCompleted("M12"), "M12 completes through the real counter");
        Expect(Main.Probe.Coins == 126, "M12 reward is 14 coins");
        Expect(Main.Probe.BakerySlotVisible(0) && Main.Probe.BakerySlotVisible(1) && Main.Probe.BakerySlotVisible(2),
            "all three bakery display slots fill");
        await CloseCompletion("M12");

        // M13: collect four real books, make one wrong shelf choice.
        Expect(await StartJobFromBoard("M13"), "M13 starts from the job board");
        foreach (string book in ChapterTwoRules.BookIds)
        {
            Main.Probe.TeleportNearPickup("book_" + book);
            await Wait(0.3f);
            Expect(Main.Probe.NearestInteractableName == "Pickup_book_" + book,
                $"{book} pickup is the nearest E interaction");
            await PressInteract();
            Expect(Main.Probe.MissionCounter("M13", "has_" + book) == 1,
                $"{book} book collected through its pickup");
        }
        Expect(Main.Probe.BookPickupCount == 0, "all four book pickups are gone");

        await TalkTo("ollie", "Ollie");
        Expect(Main.Probe.PressModalButton("LibraryHelp"), "Ollie's shelf option opens the library");
        await Wait(0.35f);
        Expect(Main.Probe.ModalTitle == "Lost Library Books", "the real library modal opens");
        Expect(Main.Probe.PressModalButton("Book_SUN"), "SUN book selected");
        Expect(Main.Probe.PressModalButton("Shelf_MOON"), "wrong shelf selected");
        await Wait(0.3f);
        Expect(Main.Probe.MissionCounter("M13", "has_sun") == 1,
            "wrong shelf keeps the book in hand");
        Expect(Main.Probe.MissionCounter("M13", "shelf_moon") == 0,
            "wrong shelf stays empty");
        Expect(Main.Probe.ModalLabelText("LibraryStatus").Contains("does not belong"),
            "wrong shelf gives a specific hint");
        Expect(Main.Probe.Coins == 126, "wrong shelf spends nothing");
        Expect(Main.Probe.PressModalButton("Shelf_SUN"), "SUN shelved correctly");
        await Wait(0.25f);
        foreach (string book in new[] { "MOON", "TREE", "STAR" })
        {
            Expect(Main.Probe.PressModalButton("Book_" + book), $"{book} selected");
            Expect(Main.Probe.PressModalButton("Shelf_" + book), $"{book} shelved");
            await Wait(0.25f);
        }
        await Wait(0.25f);
        Expect(Main.Probe.MissionCompleted("M13"), "M13 completes through the real library");
        Expect(Main.Probe.Coins == 140, "M13 reward is 14 coins");
        Expect(Main.Probe.LibrarySlotVisible(0) && Main.Probe.LibrarySlotVisible(1) &&
               Main.Probe.LibrarySlotVisible(2) && Main.Probe.LibrarySlotVisible(3),
            "all four library shelf slots fill");
        await CloseCompletion("M13");

        // M14: wrong sign and arrow, removable sign, then three correct fits.
        Expect(await StartJobFromBoard("M14"), "M14 starts from the job board");
        Main.Probe.TeleportNearSignSocket(0);
        await Wait(0.3f);
        Expect(Main.Probe.NearestInteractableName == "SignSocketStation_0",
            "MARKET socket is the nearest E interaction");
        await PressInteract();
        Expect(Main.Probe.ModalTitle == "Road Sign: MARKET", "the MARKET socket modal opens");
        Expect(Main.Probe.PressModalButton("Sign_WORKSHOP"), "wrong sign selectable");
        Expect(Main.Probe.PressModalButton("Direction_WEST"), "WEST arrow selectable");
        Expect(Main.Probe.PressModalButton("InstallSign"), "install button works");
        await Wait(0.3f);
        Expect(Main.Probe.MissionCounter("M14", "sign_market") == 0,
            "wrong sign is not installed");
        Expect(Main.Probe.ModalLabelText("SignSocketStatus").Contains("does not fit"),
            "wrong sign gives a specific hint");
        Expect(Main.Probe.Coins == 140, "wrong sign spends nothing");
        Expect(Main.Probe.PressModalButton("Sign_MARKET"), "MARKET sign selected");
        Expect(Main.Probe.PressModalButton("InstallSign"), "MARKET sign installed");
        await Wait(0.3f);
        Expect(Main.Probe.MissionCounter("M14", "sign_market") == 1, "MARKET socket recorded");
        Expect(Main.Probe.SignVisualVisible(0), "MARKET repaired sign becomes visible");
        Expect(Main.Probe.PressModalButton("RemoveSign"), "repair is reversible");
        await Wait(0.25f);
        Expect(Main.Probe.MissionCounter("M14", "sign_market") == 0, "MARKET socket cleared");
        Expect(!Main.Probe.SignVisualVisible(0), "removed sign is hidden");
        Expect(Main.Probe.PressModalButton("Sign_MARKET"), "MARKET sign reselected");
        Expect(Main.Probe.PressModalButton("Direction_WEST"), "WEST arrow reselected");
        Expect(Main.Probe.PressModalButton("InstallSign"), "MARKET sign reinstalled");
        await Wait(0.25f);
        Expect(Main.Probe.PressModalButton("SignStepAway"), "step away closes the socket");

        Main.Probe.TeleportNearSignSocket(1);
        await Wait(0.3f);
        Expect(Main.Probe.NearestInteractableName == "SignSocketStation_1",
            "WORKSHOP socket is the nearest E interaction");
        await PressInteract();
        Expect(Main.Probe.ModalTitle == "Road Sign: WORKSHOP", "the WORKSHOP socket modal opens");
        Expect(Main.Probe.PressModalButton("Sign_WORKSHOP"), "WORKSHOP sign selected");
        Expect(Main.Probe.PressModalButton("Direction_NORTH"), "NORTH arrow selected");
        Expect(Main.Probe.PressModalButton("InstallSign"), "WORKSHOP sign installed");
        await Wait(0.3f);
        Expect(Main.Probe.MissionCounter("M14", "sign_workshop") == 1, "WORKSHOP socket recorded");
        Expect(Main.Probe.PressModalButton("SignStepAway"), "leave the WORKSHOP socket");

        Main.Probe.TeleportNearSignSocket(2);
        await Wait(0.3f);
        Expect(Main.Probe.NearestInteractableName == "SignSocketStation_2",
            "GREEN socket is the nearest E interaction");
        await PressInteract();
        Expect(Main.Probe.ModalTitle == "Road Sign: GREEN", "the GREEN socket modal opens");
        Expect(Main.Probe.PressModalButton("Sign_GREEN"), "GREEN sign selected");
        Expect(Main.Probe.PressModalButton("Direction_SOUTH"), "SOUTH arrow selected");
        Expect(Main.Probe.PressModalButton("InstallSign"), "GREEN sign installed");
        await Wait(0.45f);
        Expect(Main.Probe.MissionCompleted("M14"), "M14 completes through the real sockets");
        Expect(Main.Probe.Coins == 156, "M14 reward is 16 coins");
        Expect(Main.Probe.SignVisualVisible(0) && Main.Probe.SignVisualVisible(1) && Main.Probe.SignVisualVisible(2),
            "all three repaired signs are visible");
        await CloseCompletion("M14");

        // M15: reversible wrong colour, then the six-pad pattern.
        Expect(await StartJobFromBoard("M15"), "M15 starts from the job board");
        Expect(Main.Probe.MissionCounter("M15", "pad0") == 1 &&
               Main.Probe.MissionCounter("M15", "pad1") == 2,
            "M15 starts with the first RED/BLUE example pair");
        Main.Probe.TeleportNearGardenPad(0);
        await Wait(0.3f);
        Expect(Main.Probe.NearestInteractableName == "GardenPadStation_0",
            "garden pad 1 is the nearest E interaction");
        await PressInteract();
        Expect(Main.Probe.ModalTitle == "Garden Pad 1", "the garden pad modal opens");
        Expect(Main.Probe.PressModalButton("GardenBlue"), "wrong flower selectable");
        await Wait(0.25f);
        Expect(Main.Probe.MissionCounter("M15", "pad0") == 2, "blue flower is visible");
        Expect(Main.Probe.ModalLabelText("GardenPadStatus").Contains("different colour"),
            "wrong flower gives a specific hint");
        Expect(Main.Probe.Coins == 156, "garden uses job stock, never coins");
        Expect(Main.Probe.PressModalButton("GardenRed"), "flower corrected to red");
        await Wait(0.2f);
        Expect(Main.Probe.MissionCounter("M15", "pad0") == 1, "pad 1 corrected");
        Expect(Main.Probe.PressModalButton("GardenDone"), "garden modal closes");
        for (int i = 1; i < ChapterTwoRules.GardenPattern.Length; i++)
        {
            Main.Probe.TeleportNearGardenPad(i);
            await Wait(0.3f);
            Expect(Main.Probe.NearestInteractableName == $"GardenPadStation_{i}",
                $"garden pad {i + 1} is nearest");
            await PressInteract();
            Expect(Main.Probe.ModalTitle == $"Garden Pad {i + 1}",
                $"garden pad {i + 1} modal opens");
            string color = ChapterTwoRules.GardenPattern[i];
            string button = color == "red" ? "GardenRed" : "GardenBlue";
            Expect(Main.Probe.PressModalButton(button), $"plant {color} on pad {i + 1}");
            await Wait(0.25f);
            if (i < ChapterTwoRules.GardenPattern.Length - 1)
                Expect(Main.Probe.PressModalButton("GardenDone"), "garden modal closes");
        }
        await Wait(0.4f);
        Expect(Main.Probe.MissionCompleted("M15"), "M15 completes through the real garden pads");
        Expect(Main.Probe.Coins == 172, "M15 reward is 16 coins");
        for (int i = 0; i < ChapterTwoRules.GardenPattern.Length; i++)
            Expect(Main.Probe.GardenFlowerVisible(i), $"garden flower {i + 1} is visible");
        await CloseCompletion("M15");

        Expect(Main.Probe.MissionStatus("M16") == MissionStatus.Available,
            "M16 unlocks after both M14 and M15");
        Expect(Main.Probe.MissionStatus("M17") == MissionStatus.Locked,
            "M17 still waits for M16");
        Expect(Main.Probe.Coins == 172, "M11-M15 paid exactly their authored rewards");
        Expect(Main.Probe.SaveNow(), "completed chapter-two state saves");
        var completed = SaveService.Load(Main.Probe.SavePath, Main.Probe.Catalog);
        Expect(completed.Success, completed.Message);
        Expect(completed.Success && completed.Data.CompletedMissions.Count == 15,
            "saved chapter-two state holds fifteen completions");
        Expect(completed.Success && completed.Data.Coins == 172, "saved wallet is exact");
        Expect(completed.Success && !completed.Data.FestivalSeen,
            "second finale stays unseen until M20");
        Expect(completed.Success && completed.Data.IsCompleted("M11") &&
               completed.Data.IsCompleted("M12") && completed.Data.IsCompleted("M13") &&
               completed.Data.IsCompleted("M14") && completed.Data.IsCompleted("M15"),
            "all five implemented chapter-two jobs are persisted");
        return false;
    }

    // --------------------------------------------- v1.1 -> v1.2 release migration
}
