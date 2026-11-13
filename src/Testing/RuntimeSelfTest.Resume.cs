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
    private async Task ResumeChecks()
    {
        Expect(!Main.Probe.Started, "resume phase starts at the title screen");
        Expect(Main.Probe.Paused && Main.Probe.Mode == "Title", "resume title is paused");
        Expect(Main.Probe.CanContinue, "driving save offers Continue");
        Expect(Main.Probe.MusicBusMuted && !Main.Probe.MusicEnabled,
            "saved music preference is applied at relaunch");
        Expect(Main.Probe.SfxBusMuted && Main.Probe.UiBusMuted && Main.Probe.VehiclesBusMuted && !Main.Probe.SfxEnabled,
            "saved sound preference is applied at relaunch");

        Main.Probe.Continue();
        await Wait(0.45f);
        Expect(Main.Probe.Started && !Main.Probe.Paused && Main.Probe.Mode == "Driving",
            "Continue restores driving mode");
        Expect(Main.Probe.VanControlled && !Main.Probe.PlayerControlled && !Main.Probe.PlayerVisible,
            "driving control and hidden player restored");
        Expect(Main.Probe.CharacterPreset == CharacterCustomisation.Girl,
            "Continue restores the chosen girl preset");
        Expect(Main.Probe.EquippedOutfit == "sunny" && Main.Probe.OwnsOutfit("sunny"),
            "Continue restores the bought outfit");
        Expect(Main.Probe.PlayerPreset == CharacterCustomisation.Girl && Main.Probe.PlayerOutfit == "sunny" &&
               Main.Probe.PlayerWearsDress && Main.Probe.PlayerHasLongHair,
            "Continue restores the visible character appearance");
        Expect(Main.Probe.Coins == 2 && Main.Probe.CoinText == "2",
            "Continue restores coins after the clothing purchase");

        Vector3 hiddenStart = Main.Probe.PlayerPosition;
        Vector3 vanStart = Main.Probe.VanPosition;
        Input.ActionPress("move_forward");
        await Wait(0.5f);
        Input.ActionRelease("move_forward");
        await Wait(0.2f);
        Expect(hiddenStart.DistanceTo(Main.Probe.PlayerPosition) < 0.01f,
            "hidden player does not walk while driving after reload");
        Expect(vanStart.DistanceTo(Main.Probe.VanPosition) > 0.05f, "restored van drives");

        Main.Probe.Pause();
        await Wait(0.2f);
        Expect(Main.Probe.Mode == "Modal" && Main.Probe.Paused && !Main.Probe.VanControlled,
            "restored driving session pauses correctly");
        Main.Probe.Resume();
        await Wait(0.25f);
        Expect(Main.Probe.Mode == "Driving" && !Main.Probe.Paused && Main.Probe.VanControlled,
            "restored driving session resumes correctly");

        Main.Probe.ExitVan();
        await Wait(0.3f);
        Expect(Main.Probe.Mode == "Walking" && Main.Probe.PlayerControlled && Main.Probe.PlayerVisible,
            "restored session exits the van");

        // A fresh new game must clear purchased outfits and the old preset.
        var reset = Main.Probe.StartNewGame(CharacterCustomisation.Boy);
        await Wait(0.3f);
        Expect(reset.Success, "new game reset after reload saves successfully");
        Expect(Main.Probe.CharacterPreset == CharacterCustomisation.Boy &&
               Main.Probe.PlayerPreset == CharacterCustomisation.Boy,
            "new game reset returns to the chosen boy preset");
        Expect(!Main.Probe.PlayerWearsDress && !Main.Probe.PlayerHasLongHair,
            "new game reset restores the boy silhouette");
        Expect(Main.Probe.EquippedOutfit == CharacterCustomisation.DefaultOutfit &&
               Main.Probe.OwnedOutfitCount == 0,
            "new game reset clears bought outfits and returns to Everyday");
        Expect(Main.Probe.Coins == 0, "new game reset clears the wallet");
    }

    // ------------------------------------------------- recovery integration
    // These phases reload the real scene so they exercise the same startup,
    // builder, pickup, snapshot and save path as a player relaunch.

    // ------------------------------------------------- living-village stability
    // Runs the authored NPC and traffic simulation for a sustained period with
    // real physics. It proves routes stay on the road, no vehicle teleports,
    // critical stations stay clear and the drivers resume after yielding.
}
