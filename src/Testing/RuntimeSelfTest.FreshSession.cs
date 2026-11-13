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
    private async Task FreshSessionChecks()
    {
        WorldSceneParityChecks();

        // Title + processing modes.
        Expect(!Main.Probe.Started, "starts on the title screen");
        Expect(Main.Probe.Paused, "title screen keeps the world paused");
        Expect(Main.Probe.Mode == "Title", "title control mode");
        Expect(Main.Probe.PlayerProcessMode == ProcessModeEnum.Pausable, "player is pausable");
        Expect(Main.Probe.VanProcessMode == ProcessModeEnum.Pausable, "van is pausable");
        Expect(Main.Probe.WorldProcessMode == ProcessModeEnum.Pausable, "world is pausable");
        Expect(Main.Probe.AudioBusesPresent, "audio buses exist");
        Expect(Main.Probe.MusicId == "title", "title music context starts");
        await Wait(0.45f);
        Expect(Main.Probe.HasMusicStream && Main.Probe.MusicPlaying, "title music stream is playing");
        Expect(Main.Probe.MusicPosition > 0.05, "title music playback position advances");
        Expect(!Main.Probe.MusicBusMuted, "music starts unmuted");

        Main.Probe.OpenSettingsFromTitle();
        await Wait(0.2f);
        Expect(Main.Probe.ModalOpen && Main.Probe.ModalTitle == "Settings", "title opens settings");
        Expect(Main.Probe.Paused && Main.Probe.Mode == "Title", "title settings remains paused");
        Main.Probe.CloseModal();
        await Wait(0.25f);
        Expect(Main.Probe.ModalOpen && !Main.Probe.Started && Main.Probe.Paused && Main.Probe.Mode == "Title",
            "settings returns to the title screen");
        Expect(Main.Probe.CanContinue == false, "fresh self-test has no continue");

        // Starting a new game asks for a character before the village begins.
        Main.Probe.RequestNewGameFromTitle();
        await Wait(0.2f);
        Expect(Main.Probe.ModalOpen && Main.Probe.ModalTitle == "Who would you like to play?",
            "new game opens the character chooser");
        Expect(!Main.Probe.Started && Main.Probe.Paused && Main.Probe.Mode == "Title",
            "character chooser keeps the title safe and paused");

        // The actual Cancel button returns to the title without creating a save.
        Expect(Main.Probe.PressModalButton("CharacterCancel"), "the real character Cancel button works");
        await Wait(0.25f);
        Expect(!Main.Probe.Started && Main.Probe.ModalTitle == "" && Main.Probe.Mode == "Title",
            "cancelling the fresh character chooser returns to the title");
        Expect(!Main.Probe.HasSaveFile && !Main.Probe.CanContinue && !File.Exists(Main.Probe.SavePath),
            "cancelling a fresh character chooser writes no save");

        // Choose Boy through the real button, then prove the choice survives Continue.
        Main.Probe.RequestNewGameFromTitle();
        await Wait(0.2f);
        Expect(Main.Probe.ModalTitle == "Who would you like to play?",
            "character chooser opens again after Cancel");
        Expect(Main.Probe.PressModalButton("Choose_boy"), "the real Play as Boy button works");
        await Wait(0.35f);
        Expect(Main.Probe.SfxPlayCount("ui_confirm") > 0, "real button press triggers the UI sound cue");
        Expect(Main.Probe.RallySampleCount >= 300, "rally road has a dense continuous mesh");
        Expect(Main.Probe.RallyLength > 190f, "rally circuit is larger than the old meadow loop");
        int rallyCoverage = Main.Probe.RallyCollisionCoverage();
        Expect(rallyCoverage >= 50, $"rally collision covers the loop ({rallyCoverage}/52)");
        Expect(Main.Probe.Started && Main.Probe.CharacterPreset == CharacterCustomisation.Boy,
            "choosing Boy starts the new game and stores the preset");
        Expect(Main.Probe.PlayerPreset == CharacterCustomisation.Boy && !Main.Probe.PlayerHasLongHair &&
               !Main.Probe.PlayerWearsDress, "boy appearance is applied to the player");
        Expect(Main.Probe.EquippedOutfit == CharacterCustomisation.DefaultOutfit &&
               Main.Probe.OwnedOutfitCount == 0, "new game starts in the free Everyday outfit");
        var boySave = SaveService.Load(Main.Probe.SavePath, Main.Probe.Catalog);
        Expect(boySave.Success && boySave.Data.SchemaVersion == SaveService.CurrentVersion &&
               boySave.Data.CharacterPreset == CharacterCustomisation.Boy,
            "the chosen boy preset is saved at the current schema");

        Main.Probe.ReturnToTitle();
        await Wait(0.25f);
        Expect(!Main.Probe.Started && Main.Probe.CanContinue && Main.Probe.MemoryMatchesPersisted,
            "the saved boy session offers Continue");
        Main.Probe.Continue();
        await Wait(0.35f);
        Expect(Main.Probe.Started && Main.Probe.CharacterPreset == CharacterCustomisation.Boy &&
               Main.Probe.PlayerPreset == CharacterCustomisation.Boy && !Main.Probe.PlayerHasLongHair,
            "Continue restores the chosen boy appearance");

        // Cancelling character selection while replacing an existing game must
        // leave both the live session and the persisted save untouched.
        Main.Probe.ReturnToTitle();
        await Wait(0.25f);
        byte[] boyPersisted = File.ReadAllBytes(Main.Probe.SavePath);
        Main.Probe.RequestNewGameFromTitle();
        await Wait(0.2f);
        Expect(Main.Probe.ModalTitle == "Start a new game?", "existing save asks before replacement");
        Expect(Main.Probe.PressModalButton("ModalConfirm"), "the real replacement confirm button works");
        await Wait(0.2f);
        Expect(Main.Probe.ModalTitle == "Who would you like to play?",
            "confirming replacement opens the character chooser");
        Expect(Main.Probe.PressModalButton("CharacterCancel"),
            "the real Cancel button closes the replacement chooser");
        await Wait(0.25f);
        Expect(!Main.Probe.Started && Main.Probe.CanContinue && Main.Probe.Mode == "Title",
            "cancelling replacement returns to the title and keeps Continue");
        CheckBytes(Main.Probe.SavePath, boyPersisted,
            "cancelling replacement leaves the previous save untouched");
        Main.Probe.Continue();
        await Wait(0.35f);
        Expect(Main.Probe.Started && Main.Probe.CharacterPreset == CharacterCustomisation.Boy &&
               Main.Probe.PlayerPreset == CharacterCustomisation.Boy,
            "Continue after a cancelled replacement restores the boy session");

        // Choose Girl through the real button and verify the saved choice again.
        Main.Probe.ReturnToTitle();
        await Wait(0.25f);
        Main.Probe.RequestNewGameFromTitle();
        await Wait(0.2f);
        Expect(Main.Probe.ModalTitle == "Start a new game?", "existing save asks before the girl choice");
        Expect(Main.Probe.PressModalButton("ModalConfirm"), "replacement confirmation opens for Girl");
        await Wait(0.2f);
        Expect(Main.Probe.ModalTitle == "Who would you like to play?",
            "character chooser opens after replacement confirmation");
        Expect(Main.Probe.PressModalButton("Choose_girl"), "the real Play as Girl button works");
        await Wait(0.35f);
        Expect(Main.Probe.Started && Main.Probe.CharacterPreset == CharacterCustomisation.Girl,
            "choosing Girl starts the new game and stores the preset");
        Expect(Main.Probe.PlayerPreset == CharacterCustomisation.Girl && Main.Probe.PlayerWearsDress &&
               Main.Probe.PlayerHasLongHair, "girl appearance is applied to the player");
        Expect(Main.Probe.EquippedOutfit == CharacterCustomisation.DefaultOutfit &&
               Main.Probe.OwnedOutfitCount == 0, "girl new game starts in the free Everyday outfit");
        var girlSave = SaveService.Load(Main.Probe.SavePath, Main.Probe.Catalog);
        Expect(girlSave.Success && girlSave.Data.SchemaVersion == SaveService.CurrentVersion &&
               girlSave.Data.CharacterPreset == CharacterCustomisation.Girl,
            "the chosen girl preset is saved at the current schema");

        Main.Probe.ReturnToTitle();
        await Wait(0.25f);
        Main.Probe.Continue();
        await Wait(0.35f);
        Expect(Main.Probe.Started && Main.Probe.CharacterPreset == CharacterCustomisation.Girl &&
               Main.Probe.PlayerPreset == CharacterCustomisation.Girl && Main.Probe.PlayerWearsDress &&
               Main.Probe.PlayerHasLongHair, "Continue restores the chosen girl appearance");

        // Start and walk.
        Main.Probe.Start();
        await Wait(0.3f);
        Expect(Main.Probe.Started && !Main.Probe.Paused && Main.Probe.Mode == "Walking", "start resumes walking");
        Expect(Main.Probe.PlayerControlled && !Main.Probe.VanControlled, "walking owns player input only");
        Vector3 walkStart = Main.Probe.PlayerPosition;
        Input.ActionPress("move_forward");
        await Wait(0.45f);
        Input.ActionRelease("move_forward");
        await Wait(0.15f);
        Expect(walkStart.DistanceTo(Main.Probe.PlayerPosition) > 0.05f, "player moves when unpaused");
        Expect(Main.Probe.MusicId == "village", "roaming selects village music");

        Main.Probe.ToggleMusic();
        await Wait(0.1f);
        Expect(Main.Probe.MusicBusMuted && !Main.Probe.MusicEnabled, "music toggle mutes the music bus");
        Main.Probe.ToggleMusic();
        await Wait(0.1f);
        Expect(!Main.Probe.MusicBusMuted && Main.Probe.MusicEnabled, "music toggle restores the music bus");
        Main.Probe.ToggleSfx();
        await Wait(0.1f);
        Expect(Main.Probe.SfxBusMuted && Main.Probe.UiBusMuted && Main.Probe.VehiclesBusMuted && !Main.Probe.SfxEnabled,
            "sound toggle mutes SFX, UI and vehicle buses");
        Main.Probe.ToggleSfx();
        await Wait(0.1f);
        Expect(!Main.Probe.SfxBusMuted && !Main.Probe.UiBusMuted && !Main.Probe.VehiclesBusMuted && Main.Probe.SfxEnabled,
            "sound toggle restores SFX, UI and vehicle buses");

        // Named villagers: presence, readable labels, real E conversation and
        // pause safety.
        Expect(Main.Probe.NpcCount == 6, "six named villagers are present");
        Expect(Main.Probe.NpcNames == "Milo, Ada, Pip, Fern, Ollie, Ivy", "villager names are readable");
        foreach (string id in new[] { "milo", "ada", "pip", "fern", "ollie", "ivy" })
        {
            Expect(Main.Probe.NpcPresent(id), $"villager '{id}' present");
            Expect(!string.IsNullOrWhiteSpace(Main.Probe.NpcNameLabel(id)), $"villager '{id}' has a name label");
            Expect(Main.Probe.NpcProcessMode(id) == ProcessModeEnum.Pausable,
                $"villager '{id}' is pausable");
        }

        await Wait(1.1f);
        bool anyWalked = new[] { "milo", "ada", "pip", "fern", "ollie", "ivy" }
            .Any(id => Main.Probe.NpcMoving(id));
        Expect(anyWalked, "at least one villager walks its verified patrol");

        Main.Probe.TeleportPlayerNearNpc("ada");
        await Wait(0.35f);
        Expect(Main.Probe.NearestInteractableName == "NpcTalk_ada", "Ada has the nearest E interaction");
        Input.ActionPress("interact");
        Input.ActionRelease("interact");
        await Wait(0.35f);
        Expect(Main.Probe.ModalOpen && Main.Probe.ModalTitle == "Ada", "E opens Ada's conversation");
        Expect(Main.Probe.NpcTalking("ada"), "Ada stops and talks while the dialogue is open");
        Main.Probe.CloseModal();
        await Wait(0.3f);
        Expect(!Main.Probe.NpcTalking("ada"), "closing the dialogue resumes Ada's patrol");

        // Two road-bound drivers, no physical blocking and a real yield sensor.
        Expect(Main.Probe.TrafficCount == 2, "two active NPC-driven vehicles");
        Expect(Main.Probe.TrafficIds.Contains("town_scooter") && Main.Probe.TrafficIds.Contains("delivery_car"),
            "both free-roam traffic routes are active");
        Expect(Main.Probe.TrafficHasEngineAudio("town_scooter"), "NPC vehicle engine audio exists");
        await Wait(1.0f);
        foreach (string id in new[] { "town_scooter", "delivery_car" })
        {
            Expect(Main.Probe.TrafficOnRoad(id), $"traffic '{id}' starts on the road");
            Expect(Main.Probe.TrafficSpeed(id) > 0.05f, $"traffic '{id}' is moving");
        }

        Main.Probe.TeleportPlayerAheadOfTraffic("town_scooter", 4.0f);
        await Wait(2.2f);
        Expect(Main.Probe.TrafficYielding("town_scooter"), "scooter yields to the player");
        Expect(Main.Probe.TrafficSpeed("town_scooter") < 0.8f, "scooter slows to a safe stop");
        Main.Probe.ResetPlayerToSpawn();
        await Wait(2.0f);
        Expect(Main.Probe.TrafficSpeed("town_scooter") > 0.2f, "scooter resumes after the player moves");

        var npcBefore = new Dictionary<string, Vector3>();
        foreach (string id in new[] { "milo", "ada", "pip", "fern", "ollie", "ivy" })
            npcBefore[id] = Main.Probe.NpcPosition(id);
        var trafficBefore = new Dictionary<string, Vector3>();
        foreach (string id in new[] { "town_scooter", "delivery_car" })
            trafficBefore[id] = Main.Probe.TrafficPosition(id);
        Main.Probe.Pause();
        await Wait(0.7f);
        foreach (var pair in npcBefore)
            Expect(pair.Value.DistanceTo(Main.Probe.NpcPosition(pair.Key)) < 0.01f,
                $"villager '{pair.Key}' pauses with the world");
        foreach (var pair in trafficBefore)
            Expect(pair.Value.DistanceTo(Main.Probe.TrafficPosition(pair.Key)) < 0.01f,
                $"traffic '{pair.Key}' pauses with the world");
        Main.Probe.Resume();
        await Wait(0.3f);

        // Walking pause; physics must stop even with a living player.
        Main.Probe.Pause();
        await Wait(0.2f);
        Expect(Main.Probe.ModalOpen && Main.Probe.ModalTitle == "Paused", "pause modal opens");
        Expect(Main.Probe.Paused && Main.Probe.Mode == "Modal", "pause owns the world");
        Expect(!Main.Probe.PlayerControlled && !Main.Probe.VanControlled, "pause disables gameplay input");
        Main.Probe.SetPlayerAirborne();
        Vector3 airborne = Main.Probe.PlayerPosition;
        await Wait(0.6f);
        Expect(airborne.DistanceTo(Main.Probe.PlayerPosition) < 0.001f,
            "player physics stops while paused");

        // Pause -> Settings -> Pause.
        Main.Probe.OpenSettingsFromPause();
        await Wait(0.2f);
        Expect(Main.Probe.ModalOpen && Main.Probe.ModalTitle == "Settings", "pause opens settings");
        Expect(Main.Probe.Paused && Main.Probe.Mode == "Modal", "settings stays paused");
        Main.Probe.CloseModal();
        await Wait(0.25f);
        Expect(Main.Probe.ModalOpen && Main.Probe.ModalTitle == "Paused" && Main.Probe.Paused &&
               Main.Probe.Mode == "Modal", "settings returns to pause without unpausing");
        Main.Probe.Resume();
        await Wait(0.25f);
        Expect(!Main.Probe.Paused && Main.Probe.Mode == "Walking" && Main.Probe.PlayerControlled,
            "resume returns to walking");

        // Pause -> New Game confirmation -> Cancel -> Pause.
        Main.Probe.Pause();
        await Wait(0.15f);
        Main.Probe.ConfirmNewGame();
        await Wait(0.2f);
        Expect(Main.Probe.ModalTitle == "Start a new game?" && Main.Probe.Paused, "new-game confirmation opens");
        Main.Probe.CloseModal();
        await Wait(0.25f);
        Expect(Main.Probe.ModalOpen && Main.Probe.ModalTitle == "Paused" && Main.Probe.Paused &&
               Main.Probe.Mode == "Modal", "cancelling new game returns to pause");
        Main.Probe.Resume();
        await Wait(0.2f);

        // New Game reset through the same coordinator used by the button.
        var newGame = Main.Probe.StartNewGame(CharacterCustomisation.Girl);
        await Wait(0.25f);
        Expect(newGame.Success, "new game persists successfully");
        Expect(Main.Probe.Mode == "Walking" && !Main.Probe.Paused, "new game resumes walking");
        Expect(Main.Probe.Coins == 0 && Main.Probe.CompletedCount == 0 && Main.Probe.ActiveMission == "",
            "new game resets wallet, missions and active job");
        Expect(Main.Probe.CharacterPreset == CharacterCustomisation.Girl &&
               Main.Probe.EquippedOutfit == CharacterCustomisation.DefaultOutfit &&
               Main.Probe.OwnedOutfitCount == 0, "new game keeps the chosen preset and clears outfits");
        Expect(Main.Probe.ForceAutosave(), "automatic save path succeeds");

        // Mission completion modal pauses, then resumes.
        Main.Probe.CompleteM01();
        await Wait(0.35f);
        Expect(Main.Probe.ModalOpen && Main.Probe.ModalTitle == "JOB COMPLETE!", "completion modal opens");
        Expect(Main.Probe.Paused && Main.Probe.Mode == "Modal", "completion modal pauses gameplay");
        Main.Probe.CloseModal();
        await Wait(0.25f);
        Expect(!Main.Probe.Paused && Main.Probe.Mode == "Walking", "closing completion resumes walking");

        // Clothes shop: the prompt must come from the building's scene-owned
        // DoorMarker, so moving the shop in the editor also moves this stop.
        Expect(Main.Probe.Coins == 10 && Main.Probe.CoinText == "10", "M01 coins are visible and available to spend");
        int beforeShopLook = Main.Probe.PlayerAppearanceRevision;
        Main.Probe.TeleportPlayerToClothesDoor();
        await Wait(0.3f);
        Expect(Main.Probe.NearestInteractableName == "ClothesShopStation",
            "clothes-shop prompt follows its scene-owned door");
        Input.ActionPress("interact");
        await Wait(0.25f);
        Input.ActionRelease("interact");
        await Wait(0.25f);
        Expect(Main.Probe.ModalOpen && Main.Probe.ModalTitle == "Clothes Shop", "clothes shop opens");
        Expect(Main.Probe.Paused && Main.Probe.Mode == "Modal", "clothes shop pauses the village");
        Expect(Main.Probe.PressModalButton("Outfit_sunny"), "the real Sunny Day Buy button works");
        await Wait(0.25f);
        Expect(Main.Probe.Coins == 2 && Main.Probe.CoinText == "2", "outfit price deducted once and shown in the wallet");
        Expect(Main.Probe.OwnsOutfit("sunny") && Main.Probe.EquippedOutfit == "sunny",
            "bought outfit is owned and worn");
        Expect(Main.Probe.PlayerOutfit == "sunny" && Main.Probe.PlayerAppearanceRevision > beforeShopLook,
            "equipped outfit visibly refreshes the player model");
        Expect(Main.Probe.PlayerHasLongHair && Main.Probe.PlayerWearsDress,
            "girl silhouette survives an outfit change");

        // Every outfit can be previewed, including an unaffordable one. The
        // preview control stays enabled and never buys or equips anything.
        Expect(Main.Probe.FocusModalButton("Preview_meadow"),
            "focusing an outfit preview control works");
        await Wait(0.1f);
        Expect(Main.Probe.ModalLabelText("PreviewOutfitName") == "Meadow Green",
            "focusing an outfit updates the preview");
        Expect(Main.Probe.PressModalButton("Preview_starry"),
            "an unaffordable outfit still has a selectable preview control");
        await Wait(0.1f);
        Expect(Main.Probe.ModalLabelText("PreviewOutfitName") == "Starry Night",
            "pressing Preview shows the unaffordable outfit");
        Expect(Main.Probe.ModalLabelText("PreviewOutfitPrice").Contains("15"),
            "the preview shows the selected outfit price");
        Expect(Main.Probe.ModalLabelText("PreviewWallet").Contains("2"),
            "the preview shows the current coin balance");
        Expect(Main.Probe.ModalLabelText("PreviewNotice").Contains("Not enough coins"),
            "the preview clearly reports insufficient coins");
        Expect(Main.Probe.EquippedOutfit == "sunny" && Main.Probe.Coins == 2 &&
               !Main.Probe.OwnsOutfit("starry"),
            "previewing changes neither the equipped outfit, wallet nor ownership");

        // The shop disables unaffordable and already-worn purchase actions.
        Expect(!Main.Probe.PressModalButton("Outfit_starry"), "unaffordable outfit button is disabled");
        var tooExpensive = Main.Probe.ShopBuy("starry");
        Expect(!tooExpensive.Success, "insufficient coins are refused by the domain");
        Expect(Main.Probe.Coins == 2 && !Main.Probe.OwnsOutfit("starry"),
            "refused purchase changes neither wallet nor ownership");
        Expect(!Main.Probe.PressModalButton("Outfit_sunny"), "already-worn outfit cannot be bought twice");
        var twice = Main.Probe.ShopBuy("sunny");
        Expect(!twice.Success && Main.Probe.Coins == 2, "owned outfit cannot be bought twice");
        var notOwned = Main.Probe.ShopWear("meadow");
        Expect(!notOwned.Success && Main.Probe.EquippedOutfit == "sunny",
            "unowned outfit cannot be equipped");

        // Equip through the UI: back to Everyday with a failed save, then a
        // successful Sunny Day save must clear the old error message.
        SaveService.SetTestWriteFailure(full =>
            string.Equals(full, Path.GetFullPath(Main.Probe.SavePath), StringComparison.Ordinal)
                ? new IOException("injected clothes-shop save failure")
                : null);
        Expect(Main.Probe.PressModalButton("Outfit_default"), "the real Everyday Wear button works");
        await Wait(0.25f);
        Expect(Main.Probe.EquippedOutfit == CharacterCustomisation.DefaultOutfit &&
               Main.Probe.PlayerOutfit == CharacterCustomisation.DefaultOutfit,
            "a failed clothes save still applies the outfit for this session");
        Expect(Main.Probe.Coins == 2, "wearing an owned outfit is free");
        Expect(Main.Probe.ModalStatusVisible && Main.Probe.ModalStatus.Contains("could not be saved"),
            "a failed clothes-shop save shows its error in the modal");
        SaveService.ClearTestWriteFailure();
        Expect(Main.Probe.PressModalButton("Outfit_sunny"), "the first owned shop outfit can be re-worn");
        await Wait(0.25f);
        Expect(Main.Probe.EquippedOutfit == "sunny" && Main.Probe.PlayerOutfit == "sunny",
            "owned shop outfit re-equipped through the shop UI");
        Expect(Main.Probe.Coins == 2, "re-equipping spends no extra coins");
        Expect(!Main.Probe.ModalStatusVisible,
            "a later successful clothes save clears the previous error message");

        Main.Probe.CloseModal();
        await Wait(0.25f);
        Expect(!Main.Probe.Paused && Main.Probe.Mode == "Walking", "leaving the shop resumes walking");

        // New v5 saves must earn a licence before the regular van unlocks.
        Expect(!Main.Probe.Data.LegacyVanPermit && !Main.Probe.Data.DrivingLicenceIssued,
            "a new v5 save starts without regular van access");
        Main.Probe.EnterVan();
        await Wait(0.25f);
        Expect(!Main.Probe.VanControlled &&
               Main.Probe.ModalTitle == "DRIVING LICENCE NEEDED",
            "the regular van is gated behind the driving licence");
        Main.Probe.CloseModal();
        await Wait(0.2f);

        // The rest of this legacy runtime fixture keeps van-driving coverage by
        // marking the save as an older campaign, not by granting a licence.
        Main.Probe.Data.LegacyVanPermit = true;

        // Driving pause: input, physics and race timer must all stop.
        Main.Probe.EnterVan();
        await Wait(0.3f);
        Expect(Main.Probe.Mode == "Driving" && Main.Probe.VanControlled && !Main.Probe.PlayerControlled,
            "entering the van switches control");
        Expect(!Main.Probe.PlayerVisible, "player is hidden while driving");
        bool vanAudioStarted = Main.Probe.VanHasEngineAudio && Main.Probe.VanEngineIdlePlaying;
        for (int attempt = 0; attempt < 20 && !vanAudioStarted; attempt++)
        {
            await Wait(0.1f);
            vanAudioStarted = Main.Probe.VanHasEngineAudio && Main.Probe.VanEngineIdlePlaying;
        }
        Expect(Main.Probe.VanHasEngineAudio, "van has engine audio players");
        Expect(vanAudioStarted, "van engine audio starts with the vehicle");
        Expect(Main.Probe.PrepareRace(), "race state prepared for timer test");

        Main.Probe.Pause();
        await Wait(0.2f);
        double pausedRaceTime = Main.Probe.RaceElapsed;
        Vector3 parkedVan = Main.Probe.VanPosition;
        Input.ActionPress("move_forward");
        await Wait(0.6f);
        Input.ActionRelease("move_forward");
        Expect(Math.Abs(Main.Probe.RaceElapsed - pausedRaceTime) < 0.001,
            "race timer does not advance while paused");
        Expect(parkedVan.DistanceTo(Main.Probe.VanPosition) < 0.001f,
            "van does not move while paused");
        Expect(Main.Probe.Mode == "Modal" && !Main.Probe.VanControlled, "driving pause releases van input");

        Main.Probe.Resume();
        await Wait(0.75f);
        Expect(Main.Probe.Mode == "Driving" && Main.Probe.VanControlled && !Main.Probe.Paused,
            "driving resumes after pause");
        Expect(Main.Probe.RaceElapsed > pausedRaceTime + 0.1, "race timer resumes after pause");

        Vector3 driveStart = Main.Probe.VanPosition;
        Input.ActionPress("move_forward");
        await Wait(0.5f);
        Input.ActionRelease("move_forward");
        await Wait(1.2f);
        Expect(driveStart.DistanceTo(Main.Probe.VanPosition) > 0.05f, "van drives after resume");

        Main.Probe.ExitVan();
        await Wait(0.3f);
        Expect(Main.Probe.Mode == "Walking" && Main.Probe.PlayerControlled && Main.Probe.PlayerVisible,
            "player exits the van");

        // Save while driving for the resume phase. Persist non-default audio
        // settings so the relaunch proves they are applied from disk.
        Main.Probe.EnterVan();
        await Wait(0.3f);
        Expect(Main.Probe.Mode == "Driving", "re-entered van for save");
        Main.Probe.SetMusicEnabled(false);
        Main.Probe.SetSfxEnabled(false);
        Expect(Main.Probe.SaveNow(), "manual save succeeds while driving");
        await Wait(0.2f);
    }

}
