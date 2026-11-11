namespace Cloris.Aion2Flow.ViewModels;

// One line of the details' Digital twin tab: a stat, item, skill, board or title, its value, and below it what it's made of.
public sealed record DigitalTwinRowViewModel(string Label, string Value, string? Detail = null)
{
    public bool HasDetail => !string.IsNullOrEmpty(Detail);
}
