using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using CSCore;
using VoiceRecorder.Filters.Interfaces;

namespace VoiceRecorder.Filters;


/// <summary>
/// Chains multiple filters together
/// </summary>
[SuppressMessage("Design", "CA1515:Consider making public types internal",
    Justification = "Filter used across assemblies")]
public sealed class ChainFilter : IAudioFilter
{
    private readonly List<IAudioFilter> _filters = [];

    public string Name => "Chain";
    public string Description => $"Chain of {_filters.Count} filters";

    public IReadOnlyList<IAudioFilter> Filters => _filters.AsReadOnly();

    public ChainFilter()
    {
    }

    public ChainFilter(params IAudioFilter[] filters)
    {
        _filters.AddRange(filters);
    }

    public ChainFilter AddFilter(IAudioFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        _filters.Add(filter);
        return this;
    }

    public ChainFilter RemoveFilter(IAudioFilter filter)
    {
        _filters.Remove(filter);
        return this;
    }

    public ChainFilter ClearFilters()
    {
        _filters.Clear();
        return this;
    }

    public IWaveSource ApplyFilter(IWaveSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        IWaveSource result = source;

        foreach (var filter in _filters)
        {
            try
            {
                result = filter.ApplyFilter(result);
                Debug.WriteLine($"ChainFilter: Applied {filter.Name}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ChainFilter: Failed to apply {filter.Name}: {ex.Message}");
                // Continue with the current result
            }
        }

        return result;
    }
}
