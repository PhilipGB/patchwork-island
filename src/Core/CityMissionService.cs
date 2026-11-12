using System;
using System.Collections.Generic;
using System.Linq;

namespace PatchworkIsland;

public enum CityMissionStatus
{
    Available,
    Active,
    Completed,
}

public sealed class CityMissionStartResult
{
    public bool Success;
    public string Message = "";
    public CityMissionDefinition Mission;
}

public sealed class CityProgressOutcome
{
    public bool Success;
    public bool Completed;
    public string Message = "";
    public CityMissionCompletionResult Completion;
}

public sealed class CityMissionCompletionResult
{
    public bool Success;
    public string Message = "";
    public CityMissionDefinition Mission;
    public int CoinsAwarded;
}

/// <summary>
/// Pure state machine for the independent city board. It uses its own
/// persisted fields rather than reusing town mission ids, so adding city jobs
/// cannot change M01–M21 prerequisites, celebrations or rewards.
/// </summary>
public sealed class CityMissionService
{
    public const string KeyLoaded = "loaded";
    public const string KeyAnswered = "answered";
    public const string KeyEngineer = "engineer";

    public GameData Data { get; }
    public CityMissionCatalog Catalog { get; }

    public CityMissionService(GameData data, CityMissionCatalog catalog)
    {
        Data = data ?? throw new ArgumentNullException(nameof(data));
        Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        Data.Normalise();
    }

    public CityMissionDefinition ActiveMission =>
        string.IsNullOrEmpty(Data.ActiveCityMission) ? null : Catalog.Get(Data.ActiveCityMission);

    public CityMissionStatus GetStatus(string id)
    {
        if (Data.CompletedCityMissions.Contains(id)) return CityMissionStatus.Completed;
        if (Data.ActiveCityMission == id) return CityMissionStatus.Active;
        return CityMissionStatus.Available;
    }

    public CityMissionStartResult StartMission(string id)
    {
        var mission = Catalog.Get(id);
        if (mission == null)
            return new CityMissionStartResult { Success = false, Message = "That city job is not on the board." };
        if (!Data.CityRoadOpen)
            return new CityMissionStartResult
            {
                Success = false,
                Message = "The city road is still closed. Finish Roadworks Rescue in the village first.",
                Mission = mission,
            };
        if (Data.CompletedCityMissions.Contains(id))
            return new CityMissionStartResult
            {
                Success = false,
                Message = "That city job is already finished.",
                Mission = mission,
            };
        if (mission.Prerequisites.Any(p => !Data.CompletedCityMissions.Contains(p)))
        {
            string needed = string.Join(" and ", mission.Prerequisites.Select(p => Catalog.Get(p)?.Title ?? p));
            return new CityMissionStartResult
            {
                Success = false,
                Message = $"Finish {needed} first.",
                Mission = mission,
            };
        }

        bool switching = !string.IsNullOrEmpty(Data.ActiveCityMission) &&
                         Data.ActiveCityMission != id;
        Data.ActiveCityMission = id;
        return new CityMissionStartResult
        {
            Success = true,
            Mission = mission,
            Message = $"{mission.Title} started." +
                      (switching ? " Your other city job keeps its progress." : ""),
        };
    }

    public int Progress(string id, string key) => Data.GetCityMissionCounter(id, key);

    public int C01Stops => new[]
    {
        Progress("C01", "stop_1"), Progress("C01", "stop_2"), Progress("C01", "stop_3"),
    }.Count(v => v > 0);

    public int C02Crates => Enumerable.Range(0, 6)
        .Count(i => Progress("C02", $"crate_{i}") > 0);

    public int C03Cabinets => new[]
    {
        Progress("C03", "cabinet_1"), Progress("C03", "cabinet_2"), Progress("C03", "cabinet_3"),
    }.Count(v => v > 0);

    /// <summary>Pick a stable authored hint without inventing UI-only progress.</summary>
    public int CityHintStep(string id) => id switch
    {
        "C01" => !HasCourierParcels ? 1 : C01Stops >= 3 ? 3 : 2,
        "C02" => C02Crates < 6 ? 1 : HasMarketAnswer ? 3 : 2,
        "C03" => C03Cabinets < 3 ? 1 : 2,
        _ => 0,
    };

    public bool HasCourierParcels => Progress("C01", KeyLoaded) > 0;

    public bool HasMarketAnswer => Progress("C02", KeyAnswered) > 0;

    public bool KeyEngineerDone => Progress("C03", KeyEngineer) > 0;

    public bool CanTurnIn => ActiveMission?.Id switch
    {
        "C01" => HasCourierParcels && C01Stops >= 3,
        "C02" => HasMarketAnswer,
        _ => false,
    };

    public CityProgressOutcome LoadCourierParcels()
    {
        var fail = ActiveCheck("C01", "Start City Courier at the CITY JOBS board first.");
        if (fail != null) return fail;
        if (HasCourierParcels)
            return Outcome(false, "The three numbered parcels are already loaded.");
        Data.SetCityMissionCounter("C01", KeyLoaded, 1);
        return Outcome(true, "Three parcels loaded. Deliver parcel 1 first, then 2, then 3.");
    }

    public CityProgressOutcome DeliverCourierStop(int stop)
    {
        var fail = ActiveCheck("C01", "Start City Courier at the CITY JOBS board first.");
        if (fail != null) return fail;
        if (!HasCourierParcels)
            return Outcome(false, "Collect the three numbered parcels from the depot first.");
        if (stop < 1 || stop > 3)
            return Outcome(false, "That is not one of the numbered stops.");
        if (Progress("C01", $"stop_{stop}") > 0)
            return Outcome(false, $"Parcel {stop} is already delivered.");

        int expected = C01Stops + 1;
        if (stop != expected)
            return Outcome(false, $"Read the numbers: deliver parcel {expected} next, not {stop}.");

        Data.SetCityMissionCounter("C01", $"stop_{stop}", 1);
        if (stop == 3)
            return Outcome(true, "All three parcels delivered in order. Return to CITY JOBS.");
        return Outcome(true, $"Parcel {stop} delivered. Next is parcel {stop + 1}.");
    }

    public CityProgressOutcome CollectMarketCrate(int index)
    {
        var fail = ActiveCheck("C02", "Start Market Stock-Up at the CITY JOBS board first.");
        if (fail != null) return fail;
        if (index < 0 || index >= 6)
            return Outcome(false, "That is not one of the six stock crates.");
        if (Progress("C02", $"crate_{index}") > 0)
            return Outcome(false, "That crate is already collected.");
        Data.SetCityMissionCounter("C02", $"crate_{index}", 1);
        return Outcome(true, $"Stock crate {C02Crates} of 6 collected.");
    }

    public CityProgressOutcome AnswerMarketStock(int total)
    {
        var fail = ActiveCheck("C02", "Start Market Stock-Up at the CITY JOBS board first.");
        if (fail != null) return fail;
        if (C02Crates < 6)
            return Outcome(false, $"Collect all six crates first. You have {C02Crates}.");
        if (!RoadworksRules.TotalIsCorrect(total))
            return Outcome(false, "Count again: 2 market stalls with 3 crates each. How many altogether?");
        if (HasMarketAnswer)
            return Outcome(false, "The stall answer is already checked.");
        Data.SetCityMissionCounter("C02", KeyAnswered, 1);
        return Outcome(true, "2 × 3 = 6. The market stalls are stocked. Return to CITY JOBS.");
    }

    /// <summary>Returns a small token payout for finishing. Exactly once.</summary>
    public CityMissionCompletionResult TurnInAtBoard()
    {
        var mission = ActiveMission;
        if (mission == null)
            return CompleteFail("No city job is active.");
        if (!CanTurnIn)
            return CompleteFail("This city job is not ready to finish yet.");
        return Complete(mission);
    }

    public CityProgressOutcome RepairCabinet(int cabinet, string colour)
    {
        var fail = ActiveCheck("C03", "Start Signals and Signs at the CITY JOBS board first.");
        if (fail != null) return fail;
        if (cabinet < 1 || cabinet > 3)
            return Outcome(false, "That is not one of the three signal cabinets.");
        if (Progress("C03", $"cabinet_{cabinet}") > 0)
            return Outcome(false, $"Cabinet {cabinet} is already repaired.");
        int expected = C03Cabinets + 1;
        if (cabinet != expected)
            return Outcome(false, $"Follow the numbers: repair cabinet {expected} next.");
        string expectedColour = expected switch
        {
            1 => "red",
            2 => "amber",
            _ => "green",
        };
        if (!string.Equals(colour, expectedColour, StringComparison.OrdinalIgnoreCase))
            return Outcome(false, $"Cabinet {cabinet} needs {expectedColour.ToUpperInvariant()}. Try again.");
        Data.SetCityMissionCounter("C03", $"cabinet_{cabinet}", 1);
        if (cabinet == 3)
            return Outcome(true, "Red → amber → green is correct. Talk to the city engineer.");
        return Outcome(true, $"Cabinet {cabinet} is fixed. Next is cabinet {cabinet + 1}.");
    }

    /// <summary>
    /// Fail-safe used only after restoring a save written between a completed
    /// objective and its reward. C01/C02 normally turn in at the board; if the
    /// process died after the last stop/answer, this prevents a stuck job.
    /// </summary>
    public CityMissionCompletionResult ReconcileActiveObjective()
    {
        var mission = ActiveMission;
        if (mission == null) return CompleteFail("No city job is active.");
        if (!ObjectiveComplete(Data, mission.Id))
            return CompleteFail("This city job is not finished yet.");
        return Complete(mission);
    }

    public CityProgressOutcome TalkToEngineer()    {
        var fail = ActiveCheck("C03", "Start Signals and Signs at the CITY JOBS board first.");
        if (fail != null) return fail;
        if (C03Cabinets < 3)
            return Outcome(false, $"Repair all three signal cabinets first. You have {C03Cabinets}.");
        if (KeyEngineerDone)
            return Outcome(false, "The engineer has already signed off this job.");
        Data.SetCityMissionCounter("C03", KeyEngineer, 1);
        var completion = Complete(Catalog.Get("C03"));
        return new CityProgressOutcome
        {
            Success = completion.Success,
            Completed = completion.Success,
            Message = completion.Message,
            Completion = completion,
        };
    }

    public static bool ObjectiveComplete(GameData data, string missionId) => missionId switch
    {
        "C01" => data.GetCityMissionCounter("C01", KeyLoaded) > 0 &&
                 data.GetCityMissionCounter("C01", "stop_1") > 0 &&
                 data.GetCityMissionCounter("C01", "stop_2") > 0 &&
                 data.GetCityMissionCounter("C01", "stop_3") > 0,
        "C02" => data.GetCityMissionCounter("C02", KeyAnswered) > 0,
        "C03" => data.GetCityMissionCounter("C03", "cabinet_1") > 0 &&
                 data.GetCityMissionCounter("C03", "cabinet_2") > 0 &&
                 data.GetCityMissionCounter("C03", "cabinet_3") > 0 &&
                 data.GetCityMissionCounter("C03", KeyEngineer) > 0,
        _ => false,
    };

    private CityMissionCompletionResult Complete(CityMissionDefinition mission)
    {
        if (mission == null)
            return CompleteFail("That city job is missing from the catalogue.");
        if (!ObjectiveComplete(Data, mission.Id))
            return CompleteFail("This city job is not finished yet.");
        if (Data.CompletedCityMissions.Contains(mission.Id))
            return CompleteFail("That city job is already finished.");

        Data.CompletedCityMissions.Add(mission.Id);
        Data.ActiveCityMission = "";
        Data.Coins += mission.RewardCoins;
        return new CityMissionCompletionResult
        {
            Success = true,
            Mission = mission,
            CoinsAwarded = mission.RewardCoins,
            Message = $"{mission.Title} complete! +{mission.RewardCoins} coins.",
        };
    }

    private CityProgressOutcome ActiveCheck(string missionId, string message)
    {
        if (Data.CompletedCityMissions.Contains(missionId))
            return Outcome(false, "That city job is already complete.");
        if (Data.ActiveCityMission != missionId)
            return Outcome(false, message);
        return null;
    }

    private static CityProgressOutcome Outcome(bool success, string message) =>
        new() { Success = success, Message = message };

    private static CityMissionCompletionResult CompleteFail(string message) =>
        new() { Success = false, Message = message };
}
