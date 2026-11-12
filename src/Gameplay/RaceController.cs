using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace PatchworkIsland;

/// <summary>Ordered checkpoint/practice state and HUD, delegating rewards to the domain facade.</summary>
internal sealed class RaceController
{
    private readonly GameSession _session;
    private readonly WorldModel _world;
    private readonly VanController _van;
    private readonly Hud _hud;
    private readonly AudioManager _audio;
    private readonly IGameplayHost _host;
    private GameData _data => _session.Data;
    private MissionService _missions => _session.Village;
    internal RaceTimer Timer { get; set; } = new();
    internal bool PracticeActive { get; private set; }
    private float _raceCheckTimer;
    private float _raceHudTimer;
    private float _raceWrongHintCooldown;
    private int _practiceCheckpoint;
    private float _practiceElapsed;
    private float _practiceCheckTimer;
    private float _practiceHudTimer;
    internal RaceController(GameSession session, WorldModel world, VanController van,
        Hud hud, AudioManager audio, IGameplayHost host)
    {
        _session = session; _world = world; _van = van;
        _hud = hud; _audio = audio; _host = host;
    }
    internal void CancelPractice() => PracticeActive = false;
    internal void Reset()
    {
        Timer = new RaceTimer();
        PracticeActive = false;
        _raceCheckTimer = _raceHudTimer = _raceWrongHintCooldown = 0f;
        _practiceCheckpoint = 0;
        _practiceElapsed = _practiceCheckTimer = _practiceHudTimer = 0f;
    }
    internal void Tick(float dt)
    {
        _raceWrongHintCooldown = Mathf.Max(0f, _raceWrongHintCooldown - dt);
        TickRace(dt);
        TickPracticeRace(dt);
    }
    internal void StartPracticeRace()
    {
        if (!_data.IsCompleted("M05") && !_data.IsCompleted("M09"))
        {
            _hud.ShowHint("Finish Village Road Rally before practising the race.");
            return;
        }
        if (_missions.ActiveMission != null)
        {
            _hud.ShowHint("Finish or switch your current job before a practice race.");
            return;
        }

        _hud.CloseModal();
        PracticeActive = true;
        _practiceCheckpoint = 0;
        _practiceElapsed = 0f;
        _practiceCheckTimer = 0f;
        _practiceHudTimer = 0f;
        _audio?.PlayMusic("rally");
        _hud.ShowBigToast(
            "Practice race: get into the yellow van and drive through gates 1-2-3-4. No rewards, no time limit.",
            5f, Palette.Yellow);
        _host.RefreshMission();
    }

    private void TickPracticeRace(float dt)
    {
        if (!PracticeActive || !_host.Driving) return;

        if (_practiceCheckpoint > 0 && _practiceCheckpoint < 4)
            _practiceElapsed += dt;

        _practiceHudTimer -= dt;
        if (_practiceHudTimer <= 0f)
        {
            _practiceHudTimer = 0.8f;
            _host.RefreshMission();
        }

        _practiceCheckTimer -= dt;
        if (_practiceCheckTimer > 0f || _practiceCheckpoint >= 4) return;
        _practiceCheckTimer = 0.15f;

        Vector3 gate = _world.RaceGates[_practiceCheckpoint].Position;
        float distance = new Vector2(_van.GlobalPosition.X - gate.X, _van.GlobalPosition.Z - gate.Z).Length();
        if (distance > 7f) return;

        _practiceCheckpoint++;
        _audio?.PlaySfx("race_checkpoint");
        if (_practiceCheckpoint == 1)
        {
            _practiceElapsed = 0f;
            _hud.ShowBigToast("Gate 1! Practice timer started.", 2.4f, Palette.Yellow);
        }
        else if (_practiceCheckpoint < 4)
        {
            _hud.ShowBigToast($"Gate {_practiceCheckpoint}! Next: gate {_practiceCheckpoint + 1}.", 2.2f, Palette.Lavender);
        }
        else
        {
            float time = _practiceElapsed;
            PracticeActive = false;
            _practiceCheckpoint = 0;
            _practiceElapsed = 0f;
            _audio?.PlaySfx("race_finish");
            _host.RefreshMusic();
            _hud.ShowBigToast(
                time <= 75f
                    ? $"Practice lap complete in {FormatTime(time)} — a gold-lap pace!"
                    : $"Practice lap complete in {FormatTime(time)}. Nicely driven!",
                5f, Palette.Yellow);
        }
        _host.RefreshMission();
    }

    internal void RefreshPracticeMissionUi()
    {
        var rows = new List<(string Icon, string Text)>
        {
            ("flag", $"Gate {_practiceCheckpoint} / 4"),
            ("star", _practiceCheckpoint > 0 && _practiceCheckpoint < 4
                ? $"Practice time {FormatTime(_practiceElapsed)}"
                : "No rewards · any pace is fine"),
        };
        _hud.SetMission("Practice Race", "Drive the yellow van through gates 1-2-3-4. Just for fun!", "flag", rows);

        if (!_host.Driving)
        {
            _host.SetObjective(_van.GlobalPosition, "YELLOW VAN", "van", Palette.Yellow);
        }
        else if (_practiceCheckpoint == 0 && _van.GlobalPosition.X < 40f)
        {
            _host.SetObjective(_world.BridgeCenter, "BRIDGE TO MEADOW", "waypoint", Palette.Water);
        }
        else if (_practiceCheckpoint < 4)
        {
            var gate = _world.RaceGates[_practiceCheckpoint];
            _host.SetObjective(gate.Position, $"GATE {_practiceCheckpoint + 1}", "flag", Palette.Yellow);
        }
        else
        {
            _host.SetObjective(_world.RaceStartFlag.GlobalPosition, "FINISH", "flag", Palette.Yellow);
        }
    }

    private void TickRace(float dt)
    {
        var mission = _missions.ActiveMission;
        if (!_host.Driving || mission == null ||
            mission.Id is not ("M05" or "M09" or "M19"))
            return;
        int gateCount = mission.Id == "M19" ? FinaleRules.RallyGateCount : 4;
        var gates = mission.Id == "M19" ? _world.M19RaceGates : _world.RaceGates;
        int checkpoint = _data.GetMissionCounter(mission.Id, "checkpoint");
        if (checkpoint > 0 && checkpoint < gateCount)
        {
            Timer.Tick(dt);
            Timer.Store(_data, mission.Id);
        }

        _raceHudTimer -= dt;
        if (_raceHudTimer <= 0f)
        {
            _raceHudTimer = 0.8f;
            _host.RefreshMission();
        }

        _raceCheckTimer -= dt;
        if (_raceCheckTimer > 0f || checkpoint >= gateCount) return;
        _raceCheckTimer = 0.15f;

        Vector3 gate = gates[checkpoint].Position;
        float distance = new Vector2(_van.GlobalPosition.X - gate.X, _van.GlobalPosition.Z - gate.Z).Length();
        if (distance > 7.0f) return;

        var advance = _missions.AdvanceCheckpoint(checkpoint + 1);
        if (!advance.Success)
        {
            if (_raceWrongHintCooldown <= 0f)
            {
                _hud.ShowHint(advance.Message);
                _raceWrongHintCooldown = 2.5f;
            }
            return;
        }

        if (checkpoint == 0)
        {
            Timer.Restart();
            _data.SetMissionCounter(mission.Id, "started", 1);
        }
        Timer.Store(_data, mission.Id);
        _audio?.PlaySfx("race_checkpoint");
        _hud.ShowBigToast(advance.Message, 2.4f, Palette.Yellow);
        _host.RefreshMission();

        if (checkpoint + 1 >= gateCount)
        {
            double raceSeconds = Timer.ElapsedSeconds;
            int bonus = Timer.OptionalBonus(mission.Id);
            var result = _missions.CompleteActive(bonus);
            if (result.Success)
            {
                result.RaceSeconds = raceSeconds;
                _audio?.PlaySfx("race_finish");
                _host.CompleteVillageMission(result);
                Timer = new RaceTimer();
            }
        }
    }

    private static string FormatTime(double seconds) => RaceTimer.Format(seconds);

}
