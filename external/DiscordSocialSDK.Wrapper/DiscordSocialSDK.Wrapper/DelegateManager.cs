using System.Collections.Concurrent;

namespace DiscordSocialSDK.Wrapper;

public static class DelegateManager
{
    private static readonly ConcurrentDictionary<string, Delegate> _delegates = new();

    public static T GetOrAdd<T>(string key, Func<T> creator) where T : Delegate
    {
        if (_delegates.TryGetValue(key, out var existing))
        {
            return (T)existing;
        }

        var newDelegate = creator();
        _delegates[key] = newDelegate;
        return newDelegate;
    }
}