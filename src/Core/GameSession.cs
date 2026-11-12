using System;

namespace PatchworkIsland;

/// <summary>
/// Session facade. Adopts one save and binds every domain service to that same
/// instance, so a load/reset cannot leave city or chapter activities on stale data.
/// Presenters retain this stable facade and read its current services on demand.
/// </summary>
public sealed class GameSession
{
    private readonly MissionCatalog _villageCatalog;
    private readonly CityMissionCatalog _cityCatalog;
    public GameData Data { get; private set; }
    public MissionService Village { get; private set; }
    public CityMissionService City { get; private set; }

    public GameSession(MissionCatalog villageCatalog, CityMissionCatalog cityCatalog, GameData data)
    {
        _villageCatalog = villageCatalog ?? throw new ArgumentNullException(nameof(villageCatalog));
        _cityCatalog = cityCatalog ?? throw new ArgumentNullException(nameof(cityCatalog));
        ReplaceData(data);
    }

    public void ReplaceData(GameData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        // Construct first: a validation/construction failure must leave the old
        // session graph intact rather than publishing a partially rebound save.
        var village = new MissionService(data, _villageCatalog);
        var city = new CityMissionService(data, _cityCatalog);
        Data = data;
        Village = village;
        City = city;
    }
}
