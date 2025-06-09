namespace Sefirah.Extensions;

public static class LinqExtensions
{
    public static TOut? Get<TOut, TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, TOut? defaultValue = default)
    {
        if (dictionary is null || key is null)
            return defaultValue;

        if (!dictionary.ContainsKey(key))
        {
            if (defaultValue is TValue value)
                dictionary.Add(key, value);

            return defaultValue;
        }

        if (dictionary[key] is TOut o)
            return o;

        return defaultValue;
    }

    /// <summary>
    /// Enumerates through <see cref="IEnumerable{T}"/> of elements and executes <paramref name="action"/>
    /// </summary>
    /// <typeparam name="T">Element of <paramref name="collection"/></typeparam>
    /// <param name="collection">The collection to enumerate through</param>
    /// <param name="action">The action to take every element</param>
    public static void ForEach<T>(this IEnumerable<T> collection, Action<T> action)
    {
        foreach (T value in collection)
            action(value);
    }
}
