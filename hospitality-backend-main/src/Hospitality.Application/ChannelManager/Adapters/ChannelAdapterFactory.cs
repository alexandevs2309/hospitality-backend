using Hospitality.Application.ChannelManager.Adapters;

namespace Hospitality.Application.ChannelManager.Adapters;

public class ChannelAdapterFactory
{
    private readonly Dictionary<string, IChannelAdapter> _adapters;

    public ChannelAdapterFactory(IEnumerable<IChannelAdapter> adapters)
    {
        _adapters = adapters.ToDictionary(a => a.ChannelType + "|" + a.DisplayName, a => a);
    }

    public IChannelAdapter? GetAdapter(string channelType, string displayName)
    {
        var key = channelType + "|" + displayName;
        return _adapters.TryGetValue(key, out var adapter) ? adapter : null;
    }

    public IChannelAdapter? GetAdapterByType(string channelType)
    {
        return _adapters.Values.FirstOrDefault(a => a.ChannelType.Equals(channelType, StringComparison.OrdinalIgnoreCase));
    }

    public IEnumerable<(string ChannelType, string DisplayName)> GetAvailableAdapters()
    {
        return _adapters.Keys.Select(k =>
        {
            var parts = k.Split('|');
            return (parts[0], parts[1]);
        });
    }
}