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
    private async Task<bool> ReleaseChecks()
    {
        if (ReleasePhase == 0)
        {
            var old = BuildV110ReleaseSave();
            var node = JsonSerializer.SerializeToNode(old,
                new JsonSerializerOptions { WriteIndented = true })!;
            node.AsObject().Remove("FestivalSeen");
            File.WriteAllText(Main.Probe.SavePath, node.ToJsonString());
            ReleasePhase = 1;
            Main.Probe.ReloadScene();
            return true;
        }

        if (ReleasePhase == 1)
        {
            Expect(!Main.Probe.Started, "release test starts at the title after the v1.1 save load");
            Expect(Main.Probe.CanContinue, "v1.1 save offers Continue");
            Main.Probe.Continue();
            await Wait(0.6f);
            Expect(Main.Probe.Started, "v1.1 save continues");
            Expect(Main.Probe.Coins == 137, "v1.1 coins preserved");
            Expect(Main.Probe.CharacterPreset == CharacterCustomisation.Girl, "v1.1 girl preset preserved");
            Expect(Main.Probe.EquippedOutfit == "berry" && Main.Probe.OwnsOutfit("berry"),
                "v1.1 outfit preserved");
            Expect(Main.Probe.CelebrationSeen && !Main.Probe.FestivalSeen,
                "original celebration is preserved and the new finale starts unseen");
            Expect(Main.Probe.BridgeBuilt, "v1.1 bridge world state restored");
            Expect(Main.Probe.MissionCompleted("M10"), "M10 remains complete");
            Expect(Main.Probe.MissionStatus("M11") == MissionStatus.Available,
                "M11 unlocks immediately on the v1.1 save");
            Expect(Main.Probe.MissionStatus("M12") == MissionStatus.Locked,
                "M12 still waits for M11");
            Expect(Main.Probe.ModalTitle != "VILLAGE FIXED!",
                "original ten-job celebration does not repeat");

            Main.Probe.TeleportNearJobBoard();
            await Wait(0.3f);
            Expect(Main.Probe.NearestInteractableName == "JobBoardStation",
                "migrated save reaches the job board");
            await PressInteract();
            Expect(Main.Probe.ModalTitle == "Patchwork Jobs", "migrated save opens the job board");
            Expect(Main.Probe.PressModalButton("Start_M11"), "M11 Start is offered on the migrated save");
            await Wait(0.4f);
            Expect(Main.Probe.ActiveMission == "M11", "M11 starts from the migrated save");
            Expect(Main.Probe.MissionCounter("M11", "stock_granted") == 1,
                "M11 stock is granted on the migrated save");
            Expect(Main.Probe.SaveNow(), "migrated save writes at schema 3");
            var migrated = SaveService.Load(Main.Probe.SavePath, Main.Probe.Catalog);
            Expect(migrated.Success, migrated.Message);
            Expect(migrated.Success && migrated.Data.SchemaVersion == SaveService.CurrentVersion,
                "migrated save is schema 3");
            Expect(migrated.Success && migrated.Data.Coins == 137,
                "migrated save keeps the wallet");
            Expect(migrated.Success && migrated.Data.ActiveMission == "M11",
                "migrated save keeps active M11");

            Main.Probe.ReturnToTitle();
            await Wait(0.4f);
            var current = BuildV121ReleaseSave();
            File.WriteAllText(Main.Probe.SavePath,
                JsonSerializer.Serialize(current, new JsonSerializerOptions { WriteIndented = true }));
            ReleasePhase = 2;
            Main.Probe.ReloadScene();
            return true;
        }

        Expect(!Main.Probe.Started, "v1.2.1 fixture starts at the title");
        Expect(Main.Probe.CanContinue, "v1.2.1 save offers Continue");
        Main.Probe.Continue();
        await Wait(0.7f);
        Expect(Main.Probe.Started, "v1.2.1 save continues");
        Expect(Main.Probe.Coins == 158, "v1.2.1 wallet restores at exactly 158 coins");
        Expect(Main.Probe.CharacterPreset == CharacterCustomisation.Girl,
            "v1.2.1 girl preset restores");
        Expect(Main.Probe.EquippedOutfit == "sunny" && Main.Probe.OwnsOutfit("sunny") &&
               Main.Probe.OwnsOutfit("berry"), "v1.2.1 owned and equipped outfits restore");
        Expect(Main.Probe.CelebrationSeen && !Main.Probe.FestivalSeen,
            "v1.2.1 celebration state restores without replaying the finale");
        Expect(Main.Probe.BridgeBuilt && !Main.Probe.BridgeBarrierVisible,
            "v1.2.1 bridge pieces restore the built crossing and hide the barrier");
        Expect(Main.Probe.SwingBuilt && Main.Probe.SwingPieceCount == BuildRules.SwingSockets.Length &&
               Main.Probe.PlaygroundFixHidden,
            "v1.2.1 swing pieces restore the fixed playground");
        Expect(Main.Probe.ParkPlacementCount == 3 && Main.Probe.ParkVisualCount == 3,
            "v1.2.1 village-green placements restore visually");
        Expect(Main.Probe.FurniturePlacementCount == 4 && Main.Probe.FurnitureVisualCount == 4,
            "v1.2.1 house furniture placements restore visually");
        Expect(Main.Probe.MissionCompleted("M01") && Main.Probe.MissionCompleted("M10") &&
               Main.Probe.MissionStatus("M11") == MissionStatus.Available,
            "v1.2.1 mission state restores and opens chapter two");
        Expect(Main.Probe.ActiveMission == "", "v1.2.1 idle session restores without an active job");
        Expect(Main.Probe.PlayerPosition.DistanceTo(new Vector3(-38.8f, -0.04f, 21.4f)) < 0.2f,
            "v1.2.1 player transform restores");
        Expect(Main.Probe.VanPosition.DistanceTo(new Vector3(-30f, -0.05f, 14.5f)) < 0.2f,
            "v1.2.1 van transform restores");
        Expect(Main.Probe.Mode != "Driving", "v1.2.1 walking session restores out of the van");
        Expect(Main.Probe.WorldNodeVisible("PipCarBroken"), "unfinished M18 car stays broken");
        Expect(Main.Probe.ModalTitle != "VILLAGE FIXED!",
            "the original ten-job celebration does not replay on Continue");
        return false;
    }

    private static GameData BuildV110ReleaseSave()
    {
        var data = new GameData
        {
            SchemaVersion = 2,
            Coins = 137,
            CelebrationSeen = true,
            CharacterPreset = CharacterCustomisation.Girl,
            EquippedOutfit = "berry",
        };
        foreach (string id in MissionRules.OriginalMissionIds)
            data.CompletedMissions.Add(id);
        data.OwnedOutfits.Add("berry");
        data.UnlockedCosmetics.Add("red_cap");
        data.TutorialsSeen.AddRange(new[] { "walk", "drive", "build" });
        data.BridgePieces.AddRange(BuildRules.BridgeSockets);
        data.SwingPieces.AddRange(BuildRules.SwingSockets);
        data.ParkPlacements.Add(new PlacedItem("bench", "park0"));
        data.ParkPlacements.Add(new PlacedItem("flowerbed", "park1"));
        data.ParkPlacements.Add(new PlacedItem("lamp", "park2"));
        foreach (var pair in FurnitureRules.Required)
            data.FurnitureOwned[pair.Key] = pair.Value;
        data.Player = new PlayerSnapshot
        {
            X = -38.8f,
            Y = -0.04f,
            Z = 21.4f,
            RotationY = 3.14f,
            Valid = true,
        };
        data.Van = new VehicleSnapshot
        {
            X = -30f,
            Y = -0.05f,
            Z = 14.5f,
            RotationY = 0f,
            Valid = true,
        };
        return data;
    }

    /// <summary>
    /// A schema-3 save in the exact shape v1.2.1 wrote, with the handoff's
    /// 158-coin wallet and fully built world objects. This is a recovery test,
    /// not a screenshot-derived gameplay fixture.
    /// </summary>
    private static GameData BuildV121ReleaseSave()
    {
        var data = new GameData
        {
            SchemaVersion = 3,
            Coins = 158,
            CelebrationSeen = true,
            CharacterPreset = CharacterCustomisation.Girl,
            EquippedOutfit = "sunny",
        };
        foreach (string id in MissionRules.OriginalMissionIds)
            data.CompletedMissions.Add(id);
        data.OwnedOutfits.AddRange(new[] { "sunny", "berry" });
        data.UnlockedCosmetics.Add("red_cap");
        data.TutorialsSeen.AddRange(new[] { "walk", "drive", "build" });
        data.BridgePieces.AddRange(BuildRules.BridgeSockets);
        data.SwingPieces.AddRange(BuildRules.SwingSockets);
        data.ParkPlacements.Add(new PlacedItem("bench", "park0"));
        data.ParkPlacements.Add(new PlacedItem("flowerbed", "park1"));
        data.ParkPlacements.Add(new PlacedItem("lamp", "park2"));
        foreach (var pair in FurnitureRules.Required)
            data.FurnitureOwned[pair.Key] = pair.Value;
        data.FurniturePlacements.Add(new PlacedItem("table", "house0"));
        data.FurniturePlacements.Add(new PlacedItem("chair", "house1"));
        data.FurniturePlacements.Add(new PlacedItem("chair", "house2"));
        data.FurniturePlacements.Add(new PlacedItem("plant", "house3"));
        data.Player = new PlayerSnapshot
        {
            X = -38.8f,
            Y = -0.04f,
            Z = 21.4f,
            RotationY = 3.14f,
            Valid = true,
        };
        data.Van = new VehicleSnapshot
        {
            X = -30f,
            Y = -0.05f,
            Z = 14.5f,
            RotationY = 0f,
            Valid = true,
        };
        return data;
    }

    // ------------------------------------------- partial-save world restoration
    // Each phase instantiates the real v1.2.1-shaped save, relaunches the scene
    // and verifies that the authored static world plus the restored dynamic
    // construction/finale state is visible. Scripts are not faked in the save.
}
