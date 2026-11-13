using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace PatchworkIsland;

/// <summary>One authored academy stage from data/driving_academy_lessons.json.</summary>
public sealed class DrivingAcademyLesson
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public List<string> Markers { get; init; } = new();
    public List<string> Hints { get; init; } = new();
    public bool IsFinal { get; init; }

    public string FirstHint => Hints.Count > 0 ? Hints[0] : "Follow the markers.";
}

/// <summary>
/// Strongly typed view of the authored lesson contract. The JSON file is the
/// content source; this type performs structural validation before gameplay.
/// </summary>
public sealed class DrivingAcademyCatalog
{
    public const string Format = "PatchworkIsland.DrivingAcademy.Lessons";
    public int EnrolmentCoins { get; init; } = DrivingAcademyRules.EnrolmentCoins;
    public int HelperCoins { get; init; } = DrivingAcademyRules.HelperCoins;
    public int GraduationCoins { get; init; } = DrivingAcademyRules.GraduationCoins;
    public string LicenceId { get; init; } = "patchwork_standard_driving_licence";
    public List<DrivingAcademyLesson> Lessons { get; init; } = new();
    public DrivingAcademyLesson FinalAssessment { get; init; } = new();
    public List<string> RequiredMarkers { get; init; } = new();

    public IEnumerable<DrivingAcademyLesson> AllStages =>
        Lessons.Concat(new[] { FinalAssessment });

    public DrivingAcademyLesson Find(string id) =>
        AllStages.FirstOrDefault(stage => string.Equals(stage.Id, id, StringComparison.Ordinal));

    public bool IsLesson(string id) =>
        Lessons.Any(lesson => string.Equals(lesson.Id, id, StringComparison.Ordinal));

    public string PreviousLesson(string id)
    {
        for (int i = 0; i < Lessons.Count; i++)
            if (string.Equals(Lessons[i].Id, id, StringComparison.Ordinal))
                return i == 0 ? "" : Lessons[i - 1].Id;
        return "";
    }

    public static DrivingAcademyCatalog FromJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        string format = root.GetProperty("format").GetString() ?? "";
        if (!string.Equals(format, Format, StringComparison.Ordinal))
            throw new InvalidOperationException($"Unexpected academy lesson format '{format}'.");

        var school = root.GetProperty("school");
        var licence = root.GetProperty("licence");
        var lessons = new List<DrivingAcademyLesson>();
        foreach (var element in root.GetProperty("lessons").EnumerateArray())
            lessons.Add(ReadStage(element, false));
        var final = ReadStage(root.GetProperty("finalAssessment"), true);

        var required = new List<string>();
        foreach (var element in root.GetProperty("requiredSceneMarkers").EnumerateArray())
            required.Add(element.GetString() ?? "");

        var catalog = new DrivingAcademyCatalog
        {
            EnrolmentCoins = school.GetProperty("enrolmentCoins").GetInt32(),
            HelperCoins = school.GetProperty("repeatableHelperJobCoins").GetInt32(),
            GraduationCoins = school.GetProperty("graduationRewardCoins").GetInt32(),
            LicenceId = licence.GetProperty("id").GetString() ?? "",
            Lessons = lessons,
            FinalAssessment = final,
            RequiredMarkers = required,
        };
        if (!catalog.Validate(out string error))
            throw new InvalidOperationException(error);
        return catalog;
    }

    public bool Validate(out string error)
    {
        error = "";
        if (EnrolmentCoins <= 0 || EnrolmentCoins > 1000)
            { error = "Academy enrolment cost is invalid."; return false; }
        if (HelperCoins <= 0 || HelperCoins > 100 || GraduationCoins < 0 || GraduationCoins > 100)
            { error = "Academy helper/graduation reward is invalid."; return false; }
        if (Lessons.Count != 6)
            { error = "The academy must define exactly six lessons."; return false; }
        if (string.IsNullOrWhiteSpace(FinalAssessment.Id) || !FinalAssessment.IsFinal)
            { error = "The academy final assessment is missing."; return false; }
        if (RequiredMarkers.Count != RequiredMarkers.Distinct(StringComparer.Ordinal).Count())
            { error = "Academy required markers contain duplicates."; return false; }
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var stage in AllStages)
        {
            if (string.IsNullOrWhiteSpace(stage.Id) || !ids.Add(stage.Id))
                { error = $"Academy stage id '{stage.Id}' is empty or duplicated."; return false; }
            if (stage.Markers.Count == 0 || stage.Markers.Any(string.IsNullOrWhiteSpace))
                { error = $"Academy stage '{stage.Id}' has no markers."; return false; }
            foreach (string marker in stage.Markers)
                if (!RequiredMarkers.Contains(marker, StringComparer.Ordinal))
                    { error = $"Academy stage '{stage.Id}' uses undeclared marker '{marker}'."; return false; }
        }
        if (!string.Equals(Lessons[^1].Id, "L06", StringComparison.Ordinal))
            { error = "The last lesson must be L06."; return false; }
        if (!string.IsNullOrWhiteSpace(PreviousLesson(Lessons[0].Id)))
            { error = "The first lesson cannot have a predecessor."; return false; }
        return true;
    }

    private static DrivingAcademyLesson ReadStage(JsonElement element, bool final) => new()
    {
        Id = element.GetProperty("id").GetString() ?? "",
        Title = element.GetProperty("title").GetString() ?? "",
        IsFinal = final,
        Markers = element.GetProperty("markers").EnumerateArray()
            .Select(value => value.GetString() ?? "").ToList(),
        Hints = element.TryGetProperty("hints", out var hints)
            ? hints.EnumerateArray().Select(value => value.GetString() ?? "").ToList()
            : new List<string>(),
    };
}
