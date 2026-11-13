using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace PatchworkIsland;

/// <summary>Village NPC/traffic composition and NPC task dialogue, separate from scene/session control.</summary>
internal sealed class VillagePopulationController
{
    private readonly GameSession _session;
    private readonly Node3D _root;
    private readonly WorldSceneAdapter _world;
    private readonly Hud _hud;
    private readonly PlayerController _player;
    private readonly VanController _van;
    private readonly List<NpcActor> _npcs;
    private readonly Dictionary<string, NpcActor> _npcById;
    private readonly List<TrafficCar> _trafficCars;
    private readonly AudioManager _audio;
    private readonly IGameplayHost _host;
    private NpcActor _talkingNpc;
    private GameData _data => _session.Data;
    private MissionService _missions => _session.Village;
    internal VillagePopulationController(GameSession session, Node3D root, WorldSceneAdapter world,
        Hud hud, PlayerController player, VanController van, List<NpcActor> npcs,
        Dictionary<string, NpcActor> npcById, List<TrafficCar> traffic, AudioManager audio, IGameplayHost host)
    {
        _session = session; _root = root; _world = world; _hud = hud; _player = player;
        _van = van; _npcs = npcs; _npcById = npcById; _trafficCars = traffic; _audio = audio; _host = host;
    }
    internal List<NpcOption> BuildNpcMissionOptions(NpcActor npc)
    {
        var options = new List<NpcOption>();
        if (_data.ActiveMission == "M11" && ChapterTwoRules.CardNpcIds.Contains(npc.NpcId))
        {
            foreach (string card in new[] { "card_ada", "card_ollie", "card_fern" })
            {
                string capturedCard = card;
                string cardNpc = ChapterTwoRules.NpcForCard(card);
                if (_data.GetMissionCounter("M11", $"card_{cardNpc}") <= 0) continue;
                options.Add(new NpcOption
                {
                    Text = $"Give {ChapterTwoRules.CardLabel(card)} card",
                    Name = $"GiveCard_{cardNpc}",
                    Border = Palette.Orange,
                    Close = false,
                    Action = () => CloseNpcDialogueThen(() =>
                    {
                        var result = _missions.HandWelcomeCard(npc.NpcId, capturedCard);
                        if (!result.Success)
                        {
                            AudioManager.PlayUi("ui_error");
                            _hud.ShowHint(result.Message);
                            return;
                        }
                        _audio?.PlaySfx("coin_collect", -7f);
                        if (result.Completion != null)
                            _host.CompleteVillageMission(result.Completion);
                        else
                        {
                            _hud.ShowBigToast(result.Message, 3.2f, Palette.Success);
                            _host.RefreshMission();
                        }
                    }),
                });
            }
        }

        if (_data.ActiveMission == "M12" && npc.NpcId == "ada")
        {
            options.Add(new NpcOption
            {
                Text = "Help with today's orders",
                Name = "BakeryHelp",
                Border = Palette.Orange,
                Close = false,
                Action = () => CloseNpcDialogueThen(() =>
                    BakeryUi.Show(_hud, _missions.Community,
                        result => _host.CompleteVillageMission(result), _host.RefreshMission)),
            });
        }

        if (_data.ActiveMission == "M13" && npc.NpcId == "ollie")
        {
            options.Add(new NpcOption
            {
                Text = "Match books to shelves",
                Name = "LibraryHelp",
                Border = Palette.Blue,
                Close = false,
                Action = () => CloseNpcDialogueThen(() =>
                    LibraryShelfUi.Show(_hud, _missions.Community,
                        result => _host.CompleteVillageMission(result), _host.RefreshMission)),
            });
        }

        if (_data.ActiveMission == "M15" && npc.NpcId == "fern")
        {
            options.Add(new NpcOption
            {
                Text = "Show me the garden pattern",
                Name = "GardenPatternHelp",
                Border = Palette.GrassDark,
                Close = false,
                Action = () => CloseNpcDialogueThen(() =>
                {
                    _hud.ShowHint("RED, BLUE, RED, BLUE, RED, BLUE. " +
                                 "Use the six garden pads east of the village green.");
                    AudioManager.PlayWorld("npc_greet_02", -8f);
                }),
            });
        }

        if (_data.ActiveMission == "M16" &&
            FinaleRules.DeliveryRecipients.Contains(npc.NpcId) &&
            _data.GetMissionCounter("M16", "loaded") > 0)
        {
            foreach (string recipient in FinaleRules.DeliveryRecipients)
            {
                if (_data.GetMissionCounter("M16", FinaleRules.CarryingKey(recipient)) <= 0)
                    continue;
                string captured = recipient;
                options.Add(new NpcOption
                {
                    Text = $"Deliver {FinaleRules.DeliveryName(recipient)} parcel",
                    Name = $"Deliver_{recipient}",
                    Border = Palette.Orange,
                    Close = false,
                    Action = () => CloseNpcDialogueThen(() => DeliverParcelFromVan(npc, captured)),
                });
            }
        }

        if (_data.ActiveMission == "M20" &&
            FinaleRules.FestivalInvitees.Contains(npc.NpcId) &&
            _data.GetMissionCounter("M20", FinaleRules.InvitedKey(npc.NpcId)) <= 0)
        {
            options.Add(new NpcOption
            {
                Text = "Give festival invitation",
                Name = "FestivalInvite",
                Border = Palette.Lavender,
                Close = false,
                Action = () => CloseNpcDialogueThen(() => InviteNeighbour(npc.NpcId)),
            });
        }

        return options;
    }

    internal void CloseNpcDialogueThen(Action action)
    {
        if (_hud.ModalOpen) _hud.CloseModal();
        action?.Invoke();
    }

    internal bool VanParkedNear(Vector3 position) => _van?.IsParkedNear(position) == true;

    internal void DeliverParcelFromVan(NpcActor npc, string recipientId)
    {
        if (!VanParkedNear(npc.GlobalPosition))
        {
            _hud.ShowHint("Park the van close to the neighbour and stop, then step out to deliver.");
            return;
        }
        var result = _missions.DeliverParcel(npc.NpcId, recipientId);
        if (!result.Success)
        {
            AudioManager.PlayUi("ui_error");
            _hud.ShowHint(result.Message);
            return;
        }
        AudioManager.PlayWorld("parcel_deliver", -5f);
        if (result.Completion != null)
        {
            _host.CompleteVillageMission(result.Completion);
            return;
        }
        _hud.ShowBigToast(result.Message, 3f, Palette.Orange);
        _host.RefreshMission();
    }

    internal void InviteNeighbour(string npcId)
    {
        var result = _missions.InviteForFestival(npcId);
        if (!result.Success)
        {
            AudioManager.PlayUi("ui_error");
            _hud.ShowHint(result.Message);
            return;
        }
        AudioManager.PlayWorld("festival_pop", -7f);
        _hud.ShowBigToast(result.Message, 2.8f, Palette.Yellow);
        _host.RefreshMission();
    }

    internal void SpawnVillagers()
    {
        if (!NpcCatalog.Validate(out string error))
            throw new InvalidDataException("Villager content is invalid: " + error);
        foreach (var definition in NpcCatalog.All)
        {
            var actor = new NpcActor { Name = $"Npc_{definition.Id}" };
            actor.Initialize(definition, _world.GetNpcPatrol(definition.Id));
            actor.TalkRequested += TalkToNpc;
            _root.AddChild(actor);
            _npcs.Add(actor);
            _npcById[definition.Id] = actor;
        }
    }

    internal void SpawnTraffic()
    {
        if (!TrafficCatalog.Validate(out string error))
            throw new InvalidDataException("Traffic content is invalid: " + error);
        foreach (var route in TrafficCatalog.Routes)
        {
            if (route.RallyOnly) continue;
            var car = new TrafficCar { Name = $"Traffic_{route.Id}" };
            car.Initialize(route, _world.GetTrafficPoints(route.Id),
                () => _player?.GlobalPosition ?? Vector3.Zero,
                () => _van?.GlobalPosition ?? Vector3.Zero,
                () => _host.Driving,
                () => _trafficCars);
            _root.AddChild(car);
            _trafficCars.Add(car);
        }
    }

    internal void TalkToNpc(NpcActor npc)
    {
        if (npc == null || _talkingNpc != null || !_host.Started) return;
        if (_host.Driving)
        {
            _hud.ShowBigToast("Park the van, then step out to chat.", 2.8f, Palette.Orange);
            return;
        }
        _talkingNpc = npc;
        npc.SetTalking(true);
        npc.FaceToward(_player.GlobalPosition);
        AudioManager.Instance?.PlayAt(npc.Definition.GreetingCue,
            npc.GlobalPosition + Vector3.Up * 1.4f, -10f);
        var options = new List<NpcOption>
        {
            new()
            {
                Text = "Tell me about the village",
                Close = false,
                Border = Palette.Blue,
                Action = () => _hud.SetModalStatus(npc.Definition.VillageFact, false),
            },
        };
        options.AddRange(BuildNpcMissionOptions(npc));
        NpcDialogueUi.Show(_hud, npc.Definition, npc.Definition.Intro, options, () =>
        {
            npc.SetTalking(false);
            _talkingNpc = null;
            _host.RefreshMission();
        });
    }

}
