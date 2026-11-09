using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;

namespace PatchworkIsland;

/// <summary>
/// Godot runtime verification for pause/processing and modal state. Run with:
///   godot --headless --path . -- --selftest
///   godot --headless --path . -- --selftest-resume
///   godot --headless --path . -- --selftest-recovery
/// It uses isolated user://selftest*.json data and exits non-zero on failure.
/// </summary>
public partial class RuntimeSelfTest : Node
{
    public Main Main { get; set; }

    // Static because the recovery phase deliberately reloads the real scene.
    internal static int RecoveryPhase { get; set; }

    private static int _passed;
    private static int _failed;
    private static Vector3 _savedPlayerPosition;
    private static byte[] _seedBytes;
    private static byte[] _newerOriginalBytes;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _ = Run();
    }

    private async Task Run()
    {
        bool reloading = false;
        try
        {
            await Wait(0.7f);
            if (HasArg("--selftest-resume"))
                await ResumeChecks();
            else if (HasArg("--selftest-recovery"))
                reloading = await RecoveryChecks();
            else if (HasArg("--selftest-living"))
                await LivingChecks();
            else
                await FreshSessionChecks();
        }
        catch (Exception ex)
        {
            Fail("unexpected runtime-test exception: " + ex);
        }

        if (reloading) return;
        Main.RtStopAudio();
        await Wait(0.6f);
        GD.Print($"RUNTIME RESULT: {_passed} passed, {_failed} failed");
        GetTree().Quit(_failed == 0 ? 0 : 1);
    }

    private async Task FreshSessionChecks()
    {
        // Title + processing modes.
        Expect(!Main.RtStarted, "starts on the title screen");
        Expect(Main.RtPaused, "title screen keeps the world paused");
        Expect(Main.RtMode == "Title", "title control mode");
        Expect(Main.RtPlayerProcessMode == ProcessModeEnum.Pausable, "player is pausable");
        Expect(Main.RtVanProcessMode == ProcessModeEnum.Pausable, "van is pausable");
        Expect(Main.RtWorldProcessMode == ProcessModeEnum.Pausable, "world is pausable");
        Expect(Main.RtAudioBusesPresent, "audio buses exist");
        Expect(Main.RtMusicId == "title", "title music context starts");
        await Wait(0.45f);
        Expect(Main.RtHasMusicStream && Main.RtMusicPlaying, "title music stream is playing");
        Expect(Main.RtMusicPosition > 0.05, "title music playback position advances");
        Expect(!Main.RtMusicBusMuted, "music starts unmuted");

        Main.RtOpenSettingsFromTitle();
        await Wait(0.2f);
        Expect(Main.RtModalOpen && Main.RtModalTitle == "Settings", "title opens settings");
        Expect(Main.RtPaused && Main.RtMode == "Title", "title settings remains paused");
        Main.RtCloseModal();
        await Wait(0.25f);
        Expect(Main.RtModalOpen && !Main.RtStarted && Main.RtPaused && Main.RtMode == "Title",
            "settings returns to the title screen");
        Expect(Main.RtCanContinue == false, "fresh self-test has no continue");

        // Starting a new game asks for a character before the village begins.
        Main.RtRequestNewGameFromTitle();
        await Wait(0.2f);
        Expect(Main.RtModalOpen && Main.RtModalTitle == "Who would you like to play?",
            "new game opens the character chooser");
        Expect(!Main.RtStarted && Main.RtPaused && Main.RtMode == "Title",
            "character chooser keeps the title safe and paused");

        // The actual Cancel button returns to the title without creating a save.
        Expect(Main.RtPressModalButton("CharacterCancel"), "the real character Cancel button works");
        await Wait(0.25f);
        Expect(!Main.RtStarted && Main.RtModalTitle == "" && Main.RtMode == "Title",
            "cancelling the fresh character chooser returns to the title");
        Expect(!Main.RtHasSaveFile && !Main.RtCanContinue && !File.Exists(Main.RtSavePath),
            "cancelling a fresh character chooser writes no save");

        // Choose Boy through the real button, then prove the choice survives Continue.
        Main.RtRequestNewGameFromTitle();
        await Wait(0.2f);
        Expect(Main.RtModalTitle == "Who would you like to play?",
            "character chooser opens again after Cancel");
        Expect(Main.RtPressModalButton("Choose_boy"), "the real Play as Boy button works");
        await Wait(0.35f);
        Expect(Main.RtSfxPlayCount("ui_confirm") > 0, "real button press triggers the UI sound cue");
        Expect(Main.RtStarted && Main.RtCharacterPreset == CharacterCustomisation.Boy,
            "choosing Boy starts the new game and stores the preset");
        Expect(Main.RtPlayerPreset == CharacterCustomisation.Boy && !Main.RtPlayerHasLongHair &&
               !Main.RtPlayerWearsDress, "boy appearance is applied to the player");
        Expect(Main.RtEquippedOutfit == CharacterCustomisation.DefaultOutfit &&
               Main.RtOwnedOutfitCount == 0, "new game starts in the free Everyday outfit");
        var boySave = SaveService.Load(Main.RtSavePath, Main.RtCatalog);
        Expect(boySave.Success && boySave.Data.SchemaVersion == SaveService.CurrentVersion &&
               boySave.Data.CharacterPreset == CharacterCustomisation.Boy,
            "the chosen boy preset is saved at the current schema");

        Main.RtReturnToTitle();
        await Wait(0.25f);
        Expect(!Main.RtStarted && Main.RtCanContinue && Main.RtMemoryMatchesPersisted,
            "the saved boy session offers Continue");
        Main.RtContinue();
        await Wait(0.35f);
        Expect(Main.RtStarted && Main.RtCharacterPreset == CharacterCustomisation.Boy &&
               Main.RtPlayerPreset == CharacterCustomisation.Boy && !Main.RtPlayerHasLongHair,
            "Continue restores the chosen boy appearance");

        // Cancelling character selection while replacing an existing game must
        // leave both the live session and the persisted save untouched.
        Main.RtReturnToTitle();
        await Wait(0.25f);
        byte[] boyPersisted = File.ReadAllBytes(Main.RtSavePath);
        Main.RtRequestNewGameFromTitle();
        await Wait(0.2f);
        Expect(Main.RtModalTitle == "Start a new game?", "existing save asks before replacement");
        Expect(Main.RtPressModalButton("ModalConfirm"), "the real replacement confirm button works");
        await Wait(0.2f);
        Expect(Main.RtModalTitle == "Who would you like to play?",
            "confirming replacement opens the character chooser");
        Expect(Main.RtPressModalButton("CharacterCancel"),
            "the real Cancel button closes the replacement chooser");
        await Wait(0.25f);
        Expect(!Main.RtStarted && Main.RtCanContinue && Main.RtMode == "Title",
            "cancelling replacement returns to the title and keeps Continue");
        CheckBytes(Main.RtSavePath, boyPersisted,
            "cancelling replacement leaves the previous save untouched");
        Main.RtContinue();
        await Wait(0.35f);
        Expect(Main.RtStarted && Main.RtCharacterPreset == CharacterCustomisation.Boy &&
               Main.RtPlayerPreset == CharacterCustomisation.Boy,
            "Continue after a cancelled replacement restores the boy session");

        // Choose Girl through the real button and verify the saved choice again.
        Main.RtReturnToTitle();
        await Wait(0.25f);
        Main.RtRequestNewGameFromTitle();
        await Wait(0.2f);
        Expect(Main.RtModalTitle == "Start a new game?", "existing save asks before the girl choice");
        Expect(Main.RtPressModalButton("ModalConfirm"), "replacement confirmation opens for Girl");
        await Wait(0.2f);
        Expect(Main.RtModalTitle == "Who would you like to play?",
            "character chooser opens after replacement confirmation");
        Expect(Main.RtPressModalButton("Choose_girl"), "the real Play as Girl button works");
        await Wait(0.35f);
        Expect(Main.RtStarted && Main.RtCharacterPreset == CharacterCustomisation.Girl,
            "choosing Girl starts the new game and stores the preset");
        Expect(Main.RtPlayerPreset == CharacterCustomisation.Girl && Main.RtPlayerWearsDress &&
               Main.RtPlayerHasLongHair, "girl appearance is applied to the player");
        Expect(Main.RtEquippedOutfit == CharacterCustomisation.DefaultOutfit &&
               Main.RtOwnedOutfitCount == 0, "girl new game starts in the free Everyday outfit");
        var girlSave = SaveService.Load(Main.RtSavePath, Main.RtCatalog);
        Expect(girlSave.Success && girlSave.Data.SchemaVersion == SaveService.CurrentVersion &&
               girlSave.Data.CharacterPreset == CharacterCustomisation.Girl,
            "the chosen girl preset is saved at the current schema");

        Main.RtReturnToTitle();
        await Wait(0.25f);
        Main.RtContinue();
        await Wait(0.35f);
        Expect(Main.RtStarted && Main.RtCharacterPreset == CharacterCustomisation.Girl &&
               Main.RtPlayerPreset == CharacterCustomisation.Girl && Main.RtPlayerWearsDress &&
               Main.RtPlayerHasLongHair, "Continue restores the chosen girl appearance");

        // Start and walk.
        Main.RtStart();
        await Wait(0.3f);
        Expect(Main.RtStarted && !Main.RtPaused && Main.RtMode == "Walking", "start resumes walking");
        Expect(Main.RtPlayerControlled && !Main.RtVanControlled, "walking owns player input only");
        Vector3 walkStart = Main.RtPlayerPosition;
        Input.ActionPress("move_forward");
        await Wait(0.45f);
        Input.ActionRelease("move_forward");
        await Wait(0.15f);
        Expect(walkStart.DistanceTo(Main.RtPlayerPosition) > 0.05f, "player moves when unpaused");
        Expect(Main.RtMusicId == "village", "roaming selects village music");

        Main.RtToggleMusic();
        await Wait(0.1f);
        Expect(Main.RtMusicBusMuted && !Main.RtMusicEnabled, "music toggle mutes the music bus");
        Main.RtToggleMusic();
        await Wait(0.1f);
        Expect(!Main.RtMusicBusMuted && Main.RtMusicEnabled, "music toggle restores the music bus");
        Main.RtToggleSfx();
        await Wait(0.1f);
        Expect(Main.RtSfxBusMuted && Main.RtUiBusMuted && Main.RtVehiclesBusMuted && !Main.RtSfxEnabled,
            "sound toggle mutes SFX, UI and vehicle buses");
        Main.RtToggleSfx();
        await Wait(0.1f);
        Expect(!Main.RtSfxBusMuted && !Main.RtUiBusMuted && !Main.RtVehiclesBusMuted && Main.RtSfxEnabled,
            "sound toggle restores SFX, UI and vehicle buses");

        // Named villagers: presence, readable labels, real E conversation and
        // pause safety.
        Expect(Main.RtNpcCount == 6, "six named villagers are present");
        Expect(Main.RtNpcNames == "Milo, Ada, Pip, Fern, Ollie, Ivy", "villager names are readable");
        foreach (string id in new[] { "milo", "ada", "pip", "fern", "ollie", "ivy" })
        {
            Expect(Main.RtNpcPresent(id), $"villager '{id}' present");
            Expect(!string.IsNullOrWhiteSpace(Main.RtNpcNameLabel(id)), $"villager '{id}' has a name label");
            Expect(Main.RtNpcProcessMode(id) == ProcessModeEnum.Pausable,
                $"villager '{id}' is pausable");
        }

        await Wait(1.1f);
        bool anyWalked = new[] { "milo", "ada", "pip", "fern", "ollie", "ivy" }
            .Any(id => Main.RtNpcMoving(id));
        Expect(anyWalked, "at least one villager walks its verified patrol");

        Main.RtTeleportPlayerNearNpc("ada");
        await Wait(0.35f);
        Expect(Main.RtNearestInteractableName == "NpcTalk_ada", "Ada has the nearest E interaction");
        Input.ActionPress("interact");
        Input.ActionRelease("interact");
        await Wait(0.35f);
        Expect(Main.RtModalOpen && Main.RtModalTitle == "Ada", "E opens Ada's conversation");
        Expect(Main.RtNpcTalking("ada"), "Ada stops and talks while the dialogue is open");
        Main.RtCloseModal();
        await Wait(0.3f);
        Expect(!Main.RtNpcTalking("ada"), "closing the dialogue resumes Ada's patrol");

        // Two road-bound drivers, no physical blocking and a real yield sensor.
        Expect(Main.RtTrafficCount == 2, "two active NPC-driven vehicles");
        Expect(Main.RtTrafficIds.Contains("town_scooter") && Main.RtTrafficIds.Contains("delivery_car"),
            "both free-roam traffic routes are active");
        Expect(Main.RtTrafficHasEngineAudio("town_scooter"), "NPC vehicle engine audio exists");
        await Wait(1.0f);
        foreach (string id in new[] { "town_scooter", "delivery_car" })
        {
            Expect(Main.RtTrafficOnRoad(id), $"traffic '{id}' starts on the road");
            Expect(Main.RtTrafficSpeed(id) > 0.05f, $"traffic '{id}' is moving");
        }

        Main.RtTeleportPlayerAheadOfTraffic("town_scooter", 4.0f);
        await Wait(2.2f);
        Expect(Main.RtTrafficYielding("town_scooter"), "scooter yields to the player");
        Expect(Main.RtTrafficSpeed("town_scooter") < 0.8f, "scooter slows to a safe stop");
        Main.RtResetPlayerToSpawn();
        await Wait(2.0f);
        Expect(Main.RtTrafficSpeed("town_scooter") > 0.2f, "scooter resumes after the player moves");

        var npcBefore = new Dictionary<string, Vector3>();
        foreach (string id in new[] { "milo", "ada", "pip", "fern", "ollie", "ivy" })
            npcBefore[id] = Main.RtNpcPosition(id);
        var trafficBefore = new Dictionary<string, Vector3>();
        foreach (string id in new[] { "town_scooter", "delivery_car" })
            trafficBefore[id] = Main.RtTrafficPosition(id);
        Main.RtPause();
        await Wait(0.7f);
        foreach (var pair in npcBefore)
            Expect(pair.Value.DistanceTo(Main.RtNpcPosition(pair.Key)) < 0.01f,
                $"villager '{pair.Key}' pauses with the world");
        foreach (var pair in trafficBefore)
            Expect(pair.Value.DistanceTo(Main.RtTrafficPosition(pair.Key)) < 0.01f,
                $"traffic '{pair.Key}' pauses with the world");
        Main.RtResume();
        await Wait(0.3f);

        // Walking pause; physics must stop even with a living player.
        Main.RtPause();
        await Wait(0.2f);
        Expect(Main.RtModalOpen && Main.RtModalTitle == "Paused", "pause modal opens");
        Expect(Main.RtPaused && Main.RtMode == "Modal", "pause owns the world");
        Expect(!Main.RtPlayerControlled && !Main.RtVanControlled, "pause disables gameplay input");
        Main.RtSetPlayerAirborne();
        Vector3 airborne = Main.RtPlayerPosition;
        await Wait(0.6f);
        Expect(airborne.DistanceTo(Main.RtPlayerPosition) < 0.001f,
            "player physics stops while paused");

        // Pause -> Settings -> Pause.
        Main.RtOpenSettingsFromPause();
        await Wait(0.2f);
        Expect(Main.RtModalOpen && Main.RtModalTitle == "Settings", "pause opens settings");
        Expect(Main.RtPaused && Main.RtMode == "Modal", "settings stays paused");
        Main.RtCloseModal();
        await Wait(0.25f);
        Expect(Main.RtModalOpen && Main.RtModalTitle == "Paused" && Main.RtPaused &&
               Main.RtMode == "Modal", "settings returns to pause without unpausing");
        Main.RtResume();
        await Wait(0.25f);
        Expect(!Main.RtPaused && Main.RtMode == "Walking" && Main.RtPlayerControlled,
            "resume returns to walking");

        // Pause -> New Game confirmation -> Cancel -> Pause.
        Main.RtPause();
        await Wait(0.15f);
        Main.RtConfirmNewGame();
        await Wait(0.2f);
        Expect(Main.RtModalTitle == "Start a new game?" && Main.RtPaused, "new-game confirmation opens");
        Main.RtCloseModal();
        await Wait(0.25f);
        Expect(Main.RtModalOpen && Main.RtModalTitle == "Paused" && Main.RtPaused &&
               Main.RtMode == "Modal", "cancelling new game returns to pause");
        Main.RtResume();
        await Wait(0.2f);

        // New Game reset through the same coordinator used by the button.
        var newGame = Main.RtStartNewGame(CharacterCustomisation.Girl);
        await Wait(0.25f);
        Expect(newGame.Success, "new game persists successfully");
        Expect(Main.RtMode == "Walking" && !Main.RtPaused, "new game resumes walking");
        Expect(Main.RtCoins == 0 && Main.RtCompletedCount == 0 && Main.RtActiveMission == "",
            "new game resets wallet, missions and active job");
        Expect(Main.RtCharacterPreset == CharacterCustomisation.Girl &&
               Main.RtEquippedOutfit == CharacterCustomisation.DefaultOutfit &&
               Main.RtOwnedOutfitCount == 0, "new game keeps the chosen preset and clears outfits");
        Expect(Main.RtForceAutosave(), "automatic save path succeeds");

        // Mission completion modal pauses, then resumes.
        Main.RtCompleteM01();
        await Wait(0.35f);
        Expect(Main.RtModalOpen && Main.RtModalTitle == "JOB COMPLETE!", "completion modal opens");
        Expect(Main.RtPaused && Main.RtMode == "Modal", "completion modal pauses gameplay");
        Main.RtCloseModal();
        await Wait(0.25f);
        Expect(!Main.RtPaused && Main.RtMode == "Walking", "closing completion resumes walking");

        // Clothes shop: buy with real earned coins, wear, and enforce ownership.
        Expect(Main.RtCoins == 10 && Main.RtCoinText == "10", "M01 coins are visible and available to spend");
        int beforeShopLook = Main.RtPlayerAppearanceRevision;
        Main.RtOpenClothesShop();
        await Wait(0.25f);
        Expect(Main.RtModalOpen && Main.RtModalTitle == "Clothes Shop", "clothes shop opens");
        Expect(Main.RtPaused && Main.RtMode == "Modal", "clothes shop pauses the village");
        Expect(Main.RtPressModalButton("Outfit_sunny"), "the real Sunny Day Buy button works");
        await Wait(0.25f);
        Expect(Main.RtCoins == 2 && Main.RtCoinText == "2", "outfit price deducted once and shown in the wallet");
        Expect(Main.RtOwnsOutfit("sunny") && Main.RtEquippedOutfit == "sunny",
            "bought outfit is owned and worn");
        Expect(Main.RtPlayerOutfit == "sunny" && Main.RtPlayerAppearanceRevision > beforeShopLook,
            "equipped outfit visibly refreshes the player model");
        Expect(Main.RtPlayerHasLongHair && Main.RtPlayerWearsDress,
            "girl silhouette survives an outfit change");

        // Every outfit can be previewed, including an unaffordable one. The
        // preview control stays enabled and never buys or equips anything.
        Expect(Main.RtFocusModalButton("Preview_meadow"),
            "focusing an outfit preview control works");
        await Wait(0.1f);
        Expect(Main.RtModalLabelText("PreviewOutfitName") == "Meadow Green",
            "focusing an outfit updates the preview");
        Expect(Main.RtPressModalButton("Preview_starry"),
            "an unaffordable outfit still has a selectable preview control");
        await Wait(0.1f);
        Expect(Main.RtModalLabelText("PreviewOutfitName") == "Starry Night",
            "pressing Preview shows the unaffordable outfit");
        Expect(Main.RtModalLabelText("PreviewOutfitPrice").Contains("15"),
            "the preview shows the selected outfit price");
        Expect(Main.RtModalLabelText("PreviewWallet").Contains("2"),
            "the preview shows the current coin balance");
        Expect(Main.RtModalLabelText("PreviewNotice").Contains("Not enough coins"),
            "the preview clearly reports insufficient coins");
        Expect(Main.RtEquippedOutfit == "sunny" && Main.RtCoins == 2 &&
               !Main.RtOwnsOutfit("starry"),
            "previewing changes neither the equipped outfit, wallet nor ownership");

        // The shop disables unaffordable and already-worn purchase actions.
        Expect(!Main.RtPressModalButton("Outfit_starry"), "unaffordable outfit button is disabled");
        var tooExpensive = Main.RtShopBuy("starry");
        Expect(!tooExpensive.Success, "insufficient coins are refused by the domain");
        Expect(Main.RtCoins == 2 && !Main.RtOwnsOutfit("starry"),
            "refused purchase changes neither wallet nor ownership");
        Expect(!Main.RtPressModalButton("Outfit_sunny"), "already-worn outfit cannot be bought twice");
        var twice = Main.RtShopBuy("sunny");
        Expect(!twice.Success && Main.RtCoins == 2, "owned outfit cannot be bought twice");
        var notOwned = Main.RtShopWear("meadow");
        Expect(!notOwned.Success && Main.RtEquippedOutfit == "sunny",
            "unowned outfit cannot be equipped");

        // Equip through the UI: back to Everyday with a failed save, then a
        // successful Sunny Day save must clear the old error message.
        SaveService.SetTestWriteFailure(full =>
            string.Equals(full, Path.GetFullPath(Main.RtSavePath), StringComparison.Ordinal)
                ? new IOException("injected clothes-shop save failure")
                : null);
        Expect(Main.RtPressModalButton("Outfit_default"), "the real Everyday Wear button works");
        await Wait(0.25f);
        Expect(Main.RtEquippedOutfit == CharacterCustomisation.DefaultOutfit &&
               Main.RtPlayerOutfit == CharacterCustomisation.DefaultOutfit,
            "a failed clothes save still applies the outfit for this session");
        Expect(Main.RtCoins == 2, "wearing an owned outfit is free");
        Expect(Main.RtModalStatusVisible && Main.RtModalStatus.Contains("could not be saved"),
            "a failed clothes-shop save shows its error in the modal");
        SaveService.ClearTestWriteFailure();
        Expect(Main.RtPressModalButton("Outfit_sunny"), "the first owned shop outfit can be re-worn");
        await Wait(0.25f);
        Expect(Main.RtEquippedOutfit == "sunny" && Main.RtPlayerOutfit == "sunny",
            "owned shop outfit re-equipped through the shop UI");
        Expect(Main.RtCoins == 2, "re-equipping spends no extra coins");
        Expect(!Main.RtModalStatusVisible,
            "a later successful clothes save clears the previous error message");

        Main.RtCloseModal();
        await Wait(0.25f);
        Expect(!Main.RtPaused && Main.RtMode == "Walking", "leaving the shop resumes walking");

        // Driving pause: input, physics and race timer must all stop.
        Main.RtEnterVan();
        await Wait(0.3f);
        Expect(Main.RtMode == "Driving" && Main.RtVanControlled && !Main.RtPlayerControlled,
            "entering the van switches control");
        Expect(!Main.RtPlayerVisible, "player is hidden while driving");
        Expect(Main.RtVanHasEngineAudio && Main.RtVanEngineIdlePlaying,
            "van engine audio starts with the vehicle");
        Expect(Main.RtPrepareRace(), "race state prepared for timer test");

        Main.RtPause();
        await Wait(0.2f);
        double pausedRaceTime = Main.RtRaceElapsed;
        Vector3 parkedVan = Main.RtVanPosition;
        Input.ActionPress("move_forward");
        await Wait(0.6f);
        Input.ActionRelease("move_forward");
        Expect(Math.Abs(Main.RtRaceElapsed - pausedRaceTime) < 0.001,
            "race timer does not advance while paused");
        Expect(parkedVan.DistanceTo(Main.RtVanPosition) < 0.001f,
            "van does not move while paused");
        Expect(Main.RtMode == "Modal" && !Main.RtVanControlled, "driving pause releases van input");

        Main.RtResume();
        await Wait(0.75f);
        Expect(Main.RtMode == "Driving" && Main.RtVanControlled && !Main.RtPaused,
            "driving resumes after pause");
        Expect(Main.RtRaceElapsed > pausedRaceTime + 0.1, "race timer resumes after pause");

        Vector3 driveStart = Main.RtVanPosition;
        Input.ActionPress("move_forward");
        await Wait(0.5f);
        Input.ActionRelease("move_forward");
        await Wait(1.2f);
        Expect(driveStart.DistanceTo(Main.RtVanPosition) > 0.05f, "van drives after resume");

        Main.RtExitVan();
        await Wait(0.3f);
        Expect(Main.RtMode == "Walking" && Main.RtPlayerControlled && Main.RtPlayerVisible,
            "player exits the van");

        // Save while driving for the resume phase. Persist non-default audio
        // settings so the relaunch proves they are applied from disk.
        Main.RtEnterVan();
        await Wait(0.3f);
        Expect(Main.RtMode == "Driving", "re-entered van for save");
        Main.RtSetMusicEnabled(false);
        Main.RtSetSfxEnabled(false);
        Expect(Main.RtSaveNow(), "manual save succeeds while driving");
        await Wait(0.2f);
    }

    private async Task ResumeChecks()
    {
        Expect(!Main.RtStarted, "resume phase starts at the title screen");
        Expect(Main.RtPaused && Main.RtMode == "Title", "resume title is paused");
        Expect(Main.RtCanContinue, "driving save offers Continue");
        Expect(Main.RtMusicBusMuted && !Main.RtMusicEnabled,
            "saved music preference is applied at relaunch");
        Expect(Main.RtSfxBusMuted && Main.RtUiBusMuted && Main.RtVehiclesBusMuted && !Main.RtSfxEnabled,
            "saved sound preference is applied at relaunch");

        Main.RtContinue();
        await Wait(0.45f);
        Expect(Main.RtStarted && !Main.RtPaused && Main.RtMode == "Driving",
            "Continue restores driving mode");
        Expect(Main.RtVanControlled && !Main.RtPlayerControlled && !Main.RtPlayerVisible,
            "driving control and hidden player restored");
        Expect(Main.RtCharacterPreset == CharacterCustomisation.Girl,
            "Continue restores the chosen girl preset");
        Expect(Main.RtEquippedOutfit == "sunny" && Main.RtOwnsOutfit("sunny"),
            "Continue restores the bought outfit");
        Expect(Main.RtPlayerPreset == CharacterCustomisation.Girl && Main.RtPlayerOutfit == "sunny" &&
               Main.RtPlayerWearsDress && Main.RtPlayerHasLongHair,
            "Continue restores the visible character appearance");
        Expect(Main.RtCoins == 2 && Main.RtCoinText == "2",
            "Continue restores coins after the clothing purchase");

        Vector3 hiddenStart = Main.RtPlayerPosition;
        Vector3 vanStart = Main.RtVanPosition;
        Input.ActionPress("move_forward");
        await Wait(0.5f);
        Input.ActionRelease("move_forward");
        await Wait(0.2f);
        Expect(hiddenStart.DistanceTo(Main.RtPlayerPosition) < 0.01f,
            "hidden player does not walk while driving after reload");
        Expect(vanStart.DistanceTo(Main.RtVanPosition) > 0.05f, "restored van drives");

        Main.RtPause();
        await Wait(0.2f);
        Expect(Main.RtMode == "Modal" && Main.RtPaused && !Main.RtVanControlled,
            "restored driving session pauses correctly");
        Main.RtResume();
        await Wait(0.25f);
        Expect(Main.RtMode == "Driving" && !Main.RtPaused && Main.RtVanControlled,
            "restored driving session resumes correctly");

        Main.RtExitVan();
        await Wait(0.3f);
        Expect(Main.RtMode == "Walking" && Main.RtPlayerControlled && Main.RtPlayerVisible,
            "restored session exits the van");

        // A fresh new game must clear purchased outfits and the old preset.
        var reset = Main.RtStartNewGame(CharacterCustomisation.Boy);
        await Wait(0.3f);
        Expect(reset.Success, "new game reset after reload saves successfully");
        Expect(Main.RtCharacterPreset == CharacterCustomisation.Boy &&
               Main.RtPlayerPreset == CharacterCustomisation.Boy,
            "new game reset returns to the chosen boy preset");
        Expect(!Main.RtPlayerWearsDress && !Main.RtPlayerHasLongHair,
            "new game reset restores the boy silhouette");
        Expect(Main.RtEquippedOutfit == CharacterCustomisation.DefaultOutfit &&
               Main.RtOwnedOutfitCount == 0,
            "new game reset clears bought outfits and returns to Everyday");
        Expect(Main.RtCoins == 0, "new game reset clears the wallet");
    }

    // ------------------------------------------------- recovery integration
    // These phases reload the real scene so they exercise the same startup,
    // builder, pickup, snapshot and save path as a player relaunch.

    // ------------------------------------------------- living-village stability
    // Runs the authored NPC and traffic simulation for a sustained period with
    // real physics. It proves routes stay on the road, no vehicle teleports,
    // critical stations stay clear and the drivers resume after yielding.
    private async Task LivingChecks()
    {
        Expect(!Main.RtStarted, "living test starts on the title screen");
        var started = Main.RtStartNewGameFromTitleConfirmed(CharacterCustomisation.Boy);
        await Wait(0.8f);
        Expect(started.Success && Main.RtStarted, "living test starts a fresh village");
        Expect(Main.RtNpcCount == 6, "living test sees six villagers");
        Expect(Main.RtTrafficCount == 2, "living test sees two drivers");

        string[] npcIds = { "milo", "ada", "pip", "fern", "ollie", "ivy" };
        string[] trafficIds = { "town_scooter", "delivery_car" };
        foreach (string id in npcIds)
        {
            Vector3 position = Main.RtNpcPosition(id);
            Expect(Finite(position), $"villager '{id}' has a finite position");
            Expect(WorldAnchors.DistanceToNearestStation(position.X, position.Z) >= 3.3f,
                $"villager '{id}' does not block a mission station");
        }
        foreach (string id in trafficIds)
        {
            Expect(Main.RtTrafficOnRoad(id), $"traffic '{id}' starts on the road");
            Expect(Main.RtTrafficHasEngineAudio(id), $"traffic '{id}' has bounded engine audio");
        }

        var lastPositions = new Dictionary<string, Vector3>();
        var startDistances = new Dictionary<string, float>();
        foreach (string id in trafficIds)
        {
            lastPositions[id] = Main.RtTrafficPosition(id);
            startDistances[id] = Main.RtTrafficDistance(id);
        }

        for (int second = 1; second <= 24; second++)
        {
            await Wait(1f);
            foreach (string id in trafficIds)
            {
                Vector3 position = Main.RtTrafficPosition(id);
                Expect(Finite(position), $"traffic '{id}' position is finite at {second}s");
                Expect(Main.RtTrafficOnRoad(id), $"traffic '{id}' stays on the road at {second}s");
                float gap = position.DistanceTo(lastPositions[id]);
                Expect(gap < 5.5f, $"traffic '{id}' does not teleport (gap {gap:0.0}m at {second}s)");
                Expect(WorldAnchors.DistanceToNearestStation(position.X, position.Z) >= 1.7f,
                    $"traffic '{id}' keeps clear of critical stations at {second}s");
                lastPositions[id] = position;
            }
        }

        foreach (string id in trafficIds)
        {
            float travelled = Main.RtTrafficDistance(id) - startDistances[id];
            Expect(travelled > 45f, $"traffic '{id}' completes a sustained route ({travelled:0.0}m)");
        }
        Main.RtResetPlayerToSpawn();
        await Wait(0.5f);

        foreach (string id in trafficIds)
        {
            Main.RtTeleportPlayerAheadOfTraffic(id, 4f);
            await Wait(2.4f);
            Expect(Main.RtTrafficYielding(id), $"traffic '{id}' yields to the player");
            Expect(Main.RtTrafficSpeed(id) < 0.9f, $"traffic '{id}' stops safely for the player");
            Main.RtResetPlayerToSpawn();
            await Wait(1.4f);
            Expect(Main.RtTrafficSpeed(id) > 0.15f, $"traffic '{id}' resumes after yielding");
        }

        foreach (string id in npcIds)
        {
            Vector3 position = Main.RtNpcPosition(id);
            Expect(Finite(position), $"villager '{id}' still has a finite position");
            Expect(WorldAnchors.DistanceToNearestStation(position.X, position.Z) >= 3.3f,
                $"villager '{id}' still keeps clear of mission stations");
            Expect(Main.RtNpcProcessMode(id) == ProcessModeEnum.Pausable,
                $"villager '{id}' remains pausable");
        }
    }

    private static bool Finite(Vector3 value) =>
        !float.IsNaN(value.X) && !float.IsNaN(value.Y) && !float.IsNaN(value.Z) &&
        !float.IsInfinity(value.X) && !float.IsInfinity(value.Y) && !float.IsInfinity(value.Z);

    private async Task<bool> RecoveryChecks()
    {
        switch (RecoveryPhase)
        {
            case 0: return await RecoverySeedSave();
            case 1: return await RecoveryFailedNewGame();
            case 2: return await RecoveryDiscardedChanges();
            case 3: return await RecoverySuccessfulNewGame();
            case 4: return await RecoveryNoSave();
            case 5: return await RecoveryBackup();
            case 6: return await RecoveryAfterBackup();
            case 7: return await RecoveryNewerSave();
            case 8: return await RecoveryRelaunch();
            default:
                Fail("unknown recovery phase " + RecoveryPhase);
                return false;
        }
    }

    private async Task<bool> RecoverySeedSave()
    {
        SaveService.ClearTestWriteFailure();
        string path = Main.RtSavePath;
        SaveService.Save(BuildNineMissionSave(), path, Main.RtCatalog);
        Expect(File.Exists(path), "nine-mission seed save written");
        RecoveryPhase = 1;
        Main.RtReloadScene();
        return true;
    }

    private async Task<bool> RecoveryFailedNewGame()
    {
        string path = Main.RtSavePath;
        byte[] seed = File.ReadAllBytes(path);
        _seedBytes = seed;
        Expect(Main.RtCanContinue && Main.RtHasSaveFile, "seed save offers Continue");
        Expect(Main.RtMemoryMatchesPersisted, "loaded seed matches memory");

        Main.RtRequestNewGameFromTitle();
        await Wait(0.2f);
        Expect(Main.RtModalOpen && Main.RtModalTitle == "Start a new game?",
            "new game confirmation opens from title");
        Main.RtCloseModal();
        await Wait(0.25f);
        Expect(!Main.RtStarted && Main.RtMode == "Title", "cancelled confirmation returns to title");
        CheckBytes(path, seed, "cancelled confirmation leaves the save untouched");

        SaveService.SetTestWriteFailure(full =>
            string.Equals(full, Path.GetFullPath(path), StringComparison.Ordinal)
                ? new IOException("injected save failure")
                : null);
        var result = Main.RtStartNewGameFromTitleConfirmed();
        await Wait(0.3f);
        Expect(!result.Success, "failed New Game reports failure");
        Expect(Main.RtStarted && Main.RtCoins == 0 && Main.RtCompletedCount == 0,
            "failed New Game runs the new in-memory session");
        Expect(!Main.RtMemoryMatchesPersisted, "failed New Game is not marked persisted");
        CheckBytes(path, seed, "failed New Game preserves the previous save");

        Main.RtReturnToTitle();
        await Wait(0.2f);
        Expect(Main.RtModalOpen && Main.RtModalTitle == "Return to title without saving?",
            "failed return-to-title asks before discarding");
        Main.RtReturnAnyway();
        await Wait(0.25f);
        Expect(!Main.RtStarted && Main.RtMode == "Title", "return anyway shows the title");
        Expect(Main.RtCanContinue && !Main.RtMemoryMatchesPersisted,
            "Continue is still offered for the persisted save, not discarded memory");
        Expect(Main.RtTitleNotice.Contains("last successfully saved"),
            "title explains that Continue reloads the persisted village");
        CheckBytes(path, seed, "discarding the new session leaves the save untouched");

        SaveService.ClearTestWriteFailure();
        RecoveryPhase = 2;
        Main.RtContinue();
        bool reloadRequested = Main.RtContinueReloadPending;
        Expect(reloadRequested, "Continue requested a persisted-session reload");
        return reloadRequested;
    }

    private async Task<bool> RecoveryDiscardedChanges()
    {
        string path = Main.RtSavePath;
        Expect(Main.RtStarted && Main.RtCoins == 130 && Main.RtCompletedCount == 9,
            "Continue restored the persisted nine-mission village");
        Expect(Main.RtMemoryMatchesPersisted, "reloaded village matches memory");
        Expect(Main.RtBridgeBuilt && Main.RtSwingPieceCount == 6 && Main.RtParkPlacementCount == 3,
            "Continue rebuilt bridge, swings and park");
        Expect(Main.RtPlayerPosition.DistanceTo(new Vector3(-38.8f, -0.04f, 21.4f)) < 0.1f,
            "Continue restored the saved player position");
        Expect(Main.RtActiveMission == "M10", "Continue restored the active mission");
        Expect(Main.RtCharacterPreset == CharacterCustomisation.Girl &&
               Main.RtPlayerPreset == CharacterCustomisation.Girl && Main.RtPlayerWearsDress,
            "Continue reload restores the saved girl character");
        Expect(Main.RtEquippedOutfit == "berry" && Main.RtOwnsOutfit("berry") &&
               Main.RtPlayerOutfit == "berry",
            "Continue reload restores the saved outfit ownership and appearance");
        Expect(Main.RtCanContinue, "loaded save remains continuable");
        CheckBytes(path, _seedBytes, "Continue reload does not overwrite the persisted save");

        // Settings failures must be visible in the settings modal and must not
        // consume the automatic-failure throttle used by autosave.
        Main.RtPause();
        await Wait(0.2f);
        Main.RtOpenSettingsFromPause();
        await Wait(0.2f);
        Expect(Main.RtModalOpen && Main.RtModalTitle == "Settings", "settings modal opens from pause");
        SaveService.SetTestWriteFailure(full =>
            string.Equals(full, Path.GetFullPath(path), StringComparison.Ordinal)
                ? new IOException("injected settings failure")
                : null);
        Main.RtToggleMusic();
        await Wait(0.15f);
        Expect(Main.RtModalStatusVisible && Main.RtModalStatus.Contains("could not be saved"),
            "settings failure is shown inside the modal");
        Expect(!Main.RtMusicEnabled, "settings change takes effect in memory before persistence");
        Expect(Main.RtModalTitle == "Settings", "settings failure keeps the modal open");
        Main.RtCloseModal();
        await Wait(0.25f);
        Expect(Main.RtModalOpen && Main.RtModalTitle == "Paused" && Main.RtPaused,
            "settings returns to pause after failure");
        Main.RtResume();
        await Wait(0.2f);

        SaveService.SetTestWriteFailure(full =>
            string.Equals(full, Path.GetFullPath(path), StringComparison.Ordinal)
                ? new IOException("injected autosave failure")
                : null);
        Expect(!Main.RtForceAutosave(), "autosave failure is reported");
        await Wait(0.1f);
        Expect(Main.RtToastVisible && Main.RtToastText.Contains("Autosave failed"),
            "first autosave warning survives a settings failure");
        SaveService.ClearTestWriteFailure();
        Expect(Main.RtForceAutosave(), "autosave succeeds after recovery");

        Main.RtPause();
        await Wait(0.2f);
        Main.RtOpenSettingsFromPause();
        await Wait(0.2f);
        Main.RtToggleMusic();
        await Wait(0.15f);
        Expect(Main.RtModalStatusVisible && Main.RtModalStatus.Contains("saved"),
            "settings retry replaces the failure message");
        Expect(Main.RtMusicEnabled, "settings retry applies the persisted value");
        Main.RtCloseModal();
        await Wait(0.25f);
        Expect(Main.RtModalOpen && Main.RtModalTitle == "Paused" && Main.RtPaused,
            "settings returns to pause after success");
        Main.RtResume();
        await Wait(0.2f);

        // Scenario B: a discarded gameplay change must not be continued.
        byte[] persisted = File.ReadAllBytes(path);
        _savedPlayerPosition = Main.RtPlayerPosition;
        Input.ActionPress("move_forward");
        await Wait(0.55f);
        Input.ActionRelease("move_forward");
        await Wait(0.2f);
        Expect(_savedPlayerPosition.DistanceTo(Main.RtPlayerPosition) > 0.05f,
            "unsaved gameplay movement changed the live session");
        SaveService.SetTestWriteFailure(full =>
            string.Equals(full, Path.GetFullPath(path), StringComparison.Ordinal)
                ? new IOException("injected return failure")
                : null);
        Main.RtReturnToTitle();
        await Wait(0.2f);
        Expect(Main.RtModalOpen && Main.RtModalTitle == "Return to title without saving?",
            "failed return offers discard");
        Main.RtReturnAnyway();
        await Wait(0.25f);
        Expect(!Main.RtStarted && Main.RtCanContinue && !Main.RtMemoryMatchesPersisted,
            "discarded movement leaves Continue pointing at the persisted save");
        CheckBytes(path, persisted, "failed return leaves the disk save unchanged");
        SaveService.ClearTestWriteFailure();
        RecoveryPhase = 3;
        Main.RtContinue();
        return true;
    }

    private async Task<bool> RecoverySuccessfulNewGame()
    {
        string path = Main.RtSavePath;
        Expect(Main.RtStarted && Main.RtCoins == 130 && Main.RtCompletedCount == 9,
            "Continue after discard restores persisted progress");
        Expect(_savedPlayerPosition.DistanceTo(Main.RtPlayerPosition) < 0.1f,
            "Continue after discard restores the persisted player position");
        Expect(Main.RtActiveMission == "M10", "Continue after discard restores the active mission");

        // Scenario C: a successful New Game becomes the persisted session.
        Main.RtReturnToTitle();
        await Wait(0.25f);
        Expect(!Main.RtStarted && Main.RtCanContinue && Main.RtMemoryMatchesPersisted,
            "successful return keeps the current session continuable");
        var result = Main.RtStartNewGameFromTitleConfirmed();
        await Wait(0.35f);
        Expect(result.Success, "successful New Game persists");
        Expect(Main.RtStarted && Main.RtCoins == 0 && Main.RtCompletedCount == 0,
            "successful New Game starts at zero progress");
        Expect(Main.RtMemoryMatchesPersisted, "successful New Game is marked persisted");
        var disk = SaveService.Load(path, Main.RtCatalog);
        Expect(disk.Success && disk.Data.Coins == 0 && disk.Data.CompletedMissions.Count == 0,
            "successful New Game is on disk");

        Main.RtReturnToTitle();
        await Wait(0.25f);
        Expect(Main.RtCanContinue && Main.RtMemoryMatchesPersisted,
            "title continues the successful new session");
        Main.RtContinue();
        await Wait(0.35f);
        Expect(Main.RtStarted && Main.RtCoins == 0 && Main.RtCompletedCount == 0,
            "Continue restores the successful new session, not the previous village");

        // Scenario D starts from no save at all.
        File.Delete(path);
        if (File.Exists(SaveService.BackupPath(path))) File.Delete(SaveService.BackupPath(path));
        RecoveryPhase = 4;
        Main.RtReloadScene();
        return true;
    }

    private async Task<bool> RecoveryNoSave()
    {
        string path = Main.RtSavePath;
        Expect(!Main.RtStarted && !Main.RtHasSaveFile && !Main.RtCanContinue,
            "missing save shows no false Continue");
        Expect(!File.Exists(path), "no save is present for the first-ever attempt");
        SaveService.SetTestWriteFailure(full =>
            string.Equals(full, Path.GetFullPath(path), StringComparison.Ordinal)
                ? new IOException("injected first-save failure")
                : null);
        var result = Main.RtStartNewGameFromTitleConfirmed();
        await Wait(0.3f);
        Expect(!result.Success, "failed first-ever New Game reports failure");
        Expect(Main.RtStarted && Main.RtCoins == 0, "failed first-ever New Game still runs");
        Expect(!Main.RtHasSaveFile && !Main.RtCanContinue && !Main.RtMemoryMatchesPersisted,
            "failed first-ever save creates no false Continue state");
        Expect(!File.Exists(path), "failed first-ever save writes no file");

        Main.RtReturnToTitle();
        await Wait(0.2f);
        Expect(Main.RtModalOpen && Main.RtModalTitle == "Return to title without saving?",
            "failed first-ever return asks before discarding");
        Main.RtReturnAnyway();
        await Wait(0.25f);
        Expect(!Main.RtStarted && !Main.RtCanContinue && !Main.RtHasSaveFile,
            "failed first-ever session has no Continue after discard");
        Expect(!Main.RtTitleNotice.Contains("Continue loads"),
            "no-save discard does not promise a Continue that does not exist");
        SaveService.ClearTestWriteFailure();

        Main.RtRequestNewGameFromTitle();
        await Wait(0.3f);
        Expect(Main.RtModalOpen && Main.RtModalTitle == "Who would you like to play?",
            "no-save Start adventure asks for a character");
        var startedAfterDiscard = Main.RtStartNewGameFromTitleConfirmed(CharacterCustomisation.Boy);
        await Wait(0.35f);
        Expect(startedAfterDiscard.Success && Main.RtStarted && Main.RtCoins == 0 &&
               Main.RtCompletedCount == 0,
            "choosing a character resets a discarded session when no save exists");
        Expect(File.Exists(path), "Start adventure writes the fresh no-save session");

        // Scenario E: create a valid backup-only recovery seed.
        File.Delete(path);
        if (File.Exists(SaveService.BackupPath(path))) File.Delete(SaveService.BackupPath(path));
        SaveService.Save(BuildNineMissionSave(), path, Main.RtCatalog);
        File.Copy(path, SaveService.BackupPath(path), true);
        File.Delete(path);
        Expect(!File.Exists(path) && File.Exists(SaveService.BackupPath(path)),
            "backup-only recovery seed prepared");
        RecoveryPhase = 5;
        Main.RtReloadScene();
        return true;
    }

    private async Task<bool> RecoveryBackup()
    {
        string path = Main.RtSavePath;
        string backup = SaveService.BackupPath(path);
        byte[] backupBytes = File.ReadAllBytes(backup);
        Expect(!Main.RtStarted && Main.RtCanContinue && Main.RtHasSaveFile,
            "backup recovery offers Continue");
        Expect(Main.RtMemoryMatchesPersisted, "recovered backup matches memory");

        SaveService.SetTestWriteFailure(full =>
            string.Equals(full, Path.GetFullPath(path), StringComparison.Ordinal)
                ? new IOException("injected backup replacement failure")
                : null);
        var result = Main.RtStartNewGameFromTitleConfirmed();
        await Wait(0.3f);
        Expect(!result.Success && Main.RtStarted, "failed New Game after recovery is reported");
        Expect(Main.RtCoins == 0 && Main.RtCompletedCount == 0, "failed recovery reset is in memory");
        Expect(!File.Exists(path), "failed write does not create a main save");
        CheckBytes(backup, backupBytes, "failed write leaves the recovered backup untouched");
        Main.RtReturnToTitle();
        await Wait(0.2f);
        Expect(Main.RtModalTitle == "Return to title without saving?", "recovered backup discard is confirmed");
        Main.RtReturnAnyway();
        await Wait(0.25f);
        Expect(Main.RtCanContinue && !Main.RtMemoryMatchesPersisted,
            "recovered backup remains continuable after discard");
        SaveService.ClearTestWriteFailure();
        RecoveryPhase = 6;
        Main.RtContinue();
        return true;
    }

    private async Task<bool> RecoveryAfterBackup()
    {
        string path = Main.RtSavePath;
        Expect(Main.RtStarted && Main.RtCoins == 130 && Main.RtCompletedCount == 9,
            "Continue after recovered backup restores the backup village");
        Expect(Main.RtMemoryMatchesPersisted, "recovered Continue matches memory");

        // Scenario: an unsupported newer save must remain protected until the
        // player explicitly confirms New Game.
        File.WriteAllText(path, "{\"SchemaVersion\":99,\"Coins\":5}");
        _newerOriginalBytes = File.ReadAllBytes(path);
        RecoveryPhase = 7;
        Main.RtReloadScene();
        return true;
    }

    private async Task<bool> RecoveryNewerSave()
    {
        string path = Main.RtSavePath;
        string directory = Path.GetDirectoryName(path);
        string newerPattern = Path.GetFileName(path) + ".newer-v99-*";
        Expect(!Main.RtStarted && !Main.RtCanContinue, "unsupported newer save has no Continue");
        Expect(Main.RtNewerSavePresent && !Main.RtNewerReplacementAuthorized,
            "newer save is protected before confirmation");
        Expect(Main.RtTitleNotice.Contains("newer"), "title explains the newer save");
        CheckBytes(path, _newerOriginalBytes, "newer save is untouched on load");

        Main.RtRequestNewGameFromTitle();
        await Wait(0.2f);
        Expect(Main.RtModalOpen && Main.RtModalTitle == "Start a new game?",
            "newer-save replacement confirmation opens");
        Main.RtCloseModal();
        await Wait(0.25f);
        Expect(!Main.RtStarted && !Main.RtNewerReplacementAuthorized,
            "cancelled confirmation grants no replacement permission");
        CheckBytes(path, _newerOriginalBytes, "cancelled confirmation leaves newer save untouched");

        SaveService.SetTestWriteFailure(full =>
            string.Equals(full, Path.GetFullPath(path), StringComparison.Ordinal)
                ? new IOException("injected newer replacement failure")
                : null);
        var failed = Main.RtStartNewGameFromTitleConfirmed();
        await Wait(0.3f);
        Expect(!failed.Success, "failed newer-save replacement is reported");
        Expect(Main.RtStarted && Main.RtNewerSavePresent && Main.RtNewerReplacementAuthorized,
            "failed replacement keeps the confirmed permission for Save Now");
        Expect(Main.RtCoins == 0 && Main.RtCompletedCount == 0, "newer replacement session is in memory");
        CheckBytes(path, _newerOriginalBytes, "failed replacement leaves newer save untouched");
        Expect(Directory.GetFiles(directory, newerPattern).Length == 0,
            "failed replacement creates no misleading preserved copy");

        Main.RtReturnToTitle();
        await Wait(0.2f);
        Expect(Main.RtModalTitle == "Return to title without saving?", "newer replacement discard is confirmed");
        Main.RtReturnAnyway();
        await Wait(0.25f);
        Expect(!Main.RtCanContinue && Main.RtNewerSavePresent && !Main.RtNewerReplacementAuthorized,
            "discard clears replacement permission and keeps newer protection");
        Expect(Main.RtTitleNotice.Contains("newer") && Main.RtTitleNotice.Contains("protected"),
            "newer-save discard notice explains the protected file");
        CheckBytes(path, _newerOriginalBytes, "discard leaves newer save untouched");

        var failedAgain = Main.RtStartNewGameFromTitleConfirmed();
        await Wait(0.3f);
        Expect(!failedAgain.Success && Main.RtNewerReplacementAuthorized,
            "second confirmed replacement regains retry permission");
        SaveService.ClearTestWriteFailure();
        Expect(Main.RtSaveNow(), "Save Now retries and replaces the newer save");
        await Wait(0.2f);
        Expect(!Main.RtNewerSavePresent && !Main.RtNewerReplacementAuthorized,
            "successful replacement clears newer protection and permission");
        Expect(Main.RtMemoryMatchesPersisted && Main.RtCoins == 0 && Main.RtCompletedCount == 0,
            "replacement save records the new session");
        var loaded = SaveService.Load(path, Main.RtCatalog);
        Expect(loaded.Success && loaded.Data.SchemaVersion == SaveService.CurrentVersion,
            "replacement file is a current-schema save");
        string[] preserved = Directory.GetFiles(directory, newerPattern);
        Expect(preserved.Length == 1, "exactly one preserved newer-version copy exists");
        if (preserved.Length == 1)
            CheckBytes(preserved[0], _newerOriginalBytes, "preserved copy matches the original newer save");

        Main.RtReturnToTitle();
        await Wait(0.25f);
        Expect(Main.RtCanContinue && Main.RtMemoryMatchesPersisted,
            "successful replacement session is continuable");
        Main.RtContinue();
        await Wait(0.35f);
        Expect(Main.RtStarted && Main.RtCoins == 0 && Main.RtCompletedCount == 0,
            "Continue loads the successful replacement session");

        RecoveryPhase = 8;
        Main.RtReloadScene();
        return true;
    }

    private async Task<bool> RecoveryRelaunch()
    {
        Expect(!Main.RtStarted && Main.RtCanContinue && Main.RtMemoryMatchesPersisted,
            "replacement session survives a relaunch");
        Expect(!Main.RtNewerSavePresent, "relaunched replacement is a normal save");
        Main.RtContinue();
        await Wait(0.35f);
        Expect(Main.RtStarted && Main.RtCoins == 0 && Main.RtCompletedCount == 0,
            "Continue after relaunch restores the replacement new game");
        return false;
    }

    private static GameData BuildNineMissionSave()
    {
        var data = new GameData
        {
            Coins = 130,
            ActiveMission = "M10",
            CharacterPreset = CharacterCustomisation.Girl,
            EquippedOutfit = "berry",
        };
        data.CompletedMissions.AddRange(new[] { "M01", "M02", "M03", "M04", "M05", "M06", "M07", "M08", "M09" });
        data.TutorialsSeen.AddRange(new[] { "walk", "drive", "build" });
        data.UnlockedCosmetics.Add("red_cap");
        data.OwnedOutfits.Add("berry");
        data.BridgePieces.AddRange(BuildRules.BridgeSockets);
        data.SwingPieces.AddRange(BuildRules.SwingSockets);
        data.ParkPlacements.Add(new PlacedItem("bench", "park0"));
        data.ParkPlacements.Add(new PlacedItem("flowerbed", "park1"));
        data.ParkPlacements.Add(new PlacedItem("lamp", "park2"));
        data.MissionProgress["M01"] = new Dictionary<string, int> { ["delivered"] = 1 };
        data.MissionProgress["M02"] = new Dictionary<string, int> { ["delivered"] = 1 };
        data.MissionProgress["M04"] = new Dictionary<string, int>
        {
            ["bread"] = 2, ["milk"] = 1, ["delivered"] = 1,
        };
        data.MissionProgress["M05"] = new Dictionary<string, int>
        {
            ["checkpoint"] = 4, ["started"] = 1, ["elapsedMs"] = 30_000,
        };
        data.MissionProgress["M06"] = new Dictionary<string, int> { ["blueDelivered"] = 2, ["orangeDelivered"] = 1 };
        data.MissionProgress["M09"] = new Dictionary<string, int>
        {
            ["checkpoint"] = 4, ["started"] = 1, ["elapsedMs"] = 30_000,
        };
        data.Player = new PlayerSnapshot
        {
            X = -38.8f, Y = -0.04f, Z = 21.4f, RotationY = 3.14f, Valid = true,
        };
        data.Van = new VehicleSnapshot
        {
            X = -30f, Y = -0.05f, Z = 14.5f, RotationY = 0f, Valid = true,
        };
        return data;
    }

    private void CheckBytes(string path, byte[] expected, string name)
    {
        try
        {
            Expect(File.Exists(path) && File.ReadAllBytes(path).SequenceEqual(expected), name);
        }
        catch (Exception ex)
        {
            Fail(name + ": " + ex.Message);
        }
    }

    private async Task Wait(float seconds)
    {
        await ToSignal(GetTree().CreateTimer(seconds, processAlways: true),
            SceneTreeTimer.SignalName.Timeout);
    }

    private static bool HasArg(string needle)
    {
        foreach (string arg in OS.GetCmdlineUserArgs())
            if (arg == needle) return true;
        return false;
    }

    private void Expect(bool condition, string name)
    {
        if (condition)
        {
            _passed++;
            GD.Print("PASS  " + name);
        }
        else
        {
            Fail(name);
        }
    }

    private void Fail(string name)
    {
        _failed++;
        GD.PrintErr("FAIL  " + name);
    }
}
