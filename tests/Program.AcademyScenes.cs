using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using PatchworkIsland;

namespace PatchworkIsland.Tests;

internal static partial class Program
{
    private static void AcademySceneContracts(string root)
    {
        Run("academy scene exposes every authored marker exactly once", () =>
        {
            string coursePath = Path.Combine(root, "scenes", "academy", "DrivingAcademyCourse.tscn");
            string course = File.ReadAllText(coursePath);
            var markers = Regex.Matches(course,
                    "\\[node name=\"(?<name>[^\"]+)\" type=\"Marker3D\" parent=\"GameplayMarkers\"\\]")
                .Select(match => match.Groups["name"].Value).ToList();
            Equal(markers.Count, markers.Distinct(StringComparer.Ordinal).Count(),
                "academy marker names are unique");

            string json = File.ReadAllText(Path.Combine(root, "data", "driving_academy_lessons.json"));
            var catalog = DrivingAcademyCatalog.FromJson(json);
            foreach (string required in catalog.RequiredMarkers)
                Check(markers.Contains(required, StringComparer.Ordinal),
                    $"required marker {required} exists");
            foreach (string spawn in new[]
                     {
                         "Lesson03_Start", "Lesson04_Start", "Lesson05_Start", "Lesson06_Start",
                     })
                Check(markers.Contains(spawn, StringComparer.Ordinal),
                    $"stage spawn {spawn} exists");

            Check(course.Contains("PracticeVehicleSafeReset"),
                "safe reset marker is authored");
            Check(course.Contains("cast_shadow = 0"),
                "road markings disable detached shadows");
            Check(course.Contains("TrafficLightCentre"),
                "traffic light prop is authored");
            foreach (string id in new[] { "Lesson04_Crossing", "Final_Crossing" })
            {
                var block = Regex.Match(course, $"\\[node name=\"{id}\"[^\\[]*").Value;
                Check(block.Contains("metadata/crossing_half_width = 6.0") &&
                      block.Contains("metadata/crossing_half_length = 3.0"),
                    $"{id} has bounded authored lane dimensions");
            }
            Check(!Regex.IsMatch(course, "StopSign[^\"]*\" type=\"StaticBody3D\""),
                "roadside signs are visual only");
            foreach (string id in new[] { "Lesson04_TrafficLight", "Final_Stop" })
            {
                var block = Regex.Match(course, $"\\[node name=\"{id}\"[^\\[]*").Value;
                Check(block.Contains("metadata/stop_line_half_width = 5.5") &&
                      block.Contains("metadata/stop_line_approach_length = 16.0") &&
                      block.Contains("rotation_degrees = Vector3(0, 180, 0)"),
                    $"{id} has an authored finite directed stop-line segment");
            }
        });

        Run("academy school is placed clear of town buildings and roads", () =>
        {
            string town = File.ReadAllText(Path.Combine(root, "scenes", "world", "Town.tscn"));
            Check(town.Contains("res://scenes/academy/DrivingSchoolOffice.tscn"),
                "school office is instanced in the authored town");
            Check(town.Contains("res://scenes/academy/DrivingSchoolYard.tscn"),
                "cone yard is instanced in the authored town");
            const float schoolX = 32f;
            const float schoolZ = 48f;
            foreach (var building in WorldAnchors.Buildings)
            {
                bool overlaps = MathF.Abs(schoolX - building.X) < building.HalfX + 7.9f &&
                                MathF.Abs(schoolZ - building.Z) < building.HalfZ + 5.6f;
                Check(!overlaps, "school footprint clears a town building");
            }
            Check(schoolZ > 40f, "school is on the open south lawn");
            Check(schoolX > 20f && schoolX < 40f, "school sits on the authored west island");
            Check(schoolX + 7.9f < 41f && schoolZ + 5.6f < 55f,
                "school footprint stays inside the west island");
            Check(WorldAnchors.DistanceToNearestStation(schoolX, schoolZ) > 10f,
                "school approach clears every critical station");
        });

        Run("academy office and yard keep their authored interaction contract", () =>
        {
            string office = File.ReadAllText(Path.Combine(root, "scenes", "academy", "DrivingSchoolOffice.tscn"));
            Check(office.Contains("EnrollmentInteraction"), "office has the enrolment marker");
            Check(office.Contains("EntranceApproach"), "office has a safe approach marker");
            Check(office.Contains("OfficeCollision"), "office walls are solid");
            Check(office.Contains("WallsShape"), "office collision shape exists");

            string yard = File.ReadAllText(Path.Combine(root, "scenes", "academy", "DrivingSchoolYard.tscn"));
            for (int i = 1; i <= 3; i++)
            {
                Check(yard.Contains($"HelperCone_{i}"), $"yard cone marker {i} exists");
                Check(yard.Contains($"\"{i}\""), $"yard cone label {i} exists");
            }

            foreach (string icon in new[]
                     {
                         "driving_licence.svg", "parking.svg", "steering_wheel.svg", "traffic_light.svg",
                     })
                Check(File.Exists(Path.Combine(root, "assets", "icons", icon)),
                    $"academy icon {icon} is available to the HUD");
        });
    }
}
