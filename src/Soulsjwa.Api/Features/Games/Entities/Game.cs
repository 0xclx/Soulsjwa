using Soulsjwa.Api.Features.Events.Entities;

namespace Soulsjwa.Api.Features.Games.Entities;

public class Game
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool ConnectorSupported { get; set; }
    public string? RequiredConnectorVersion { get; set; }
    public ICollection<Objective> PredefinedObjectives { get; set; } = [];
}
