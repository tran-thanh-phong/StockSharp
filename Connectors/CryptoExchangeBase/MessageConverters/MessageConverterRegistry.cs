namespace StockSharp.CryptoExchange.MessageConverters;

/// <summary>
/// Registry for managing message converters between CryptoExchange.Net and StockSharp.
/// </summary>
public class MessageConverterRegistry
{
    private readonly ConcurrentDictionary<(Type SourceType, Type TargetType), IMessageConverter> _converters
        = new();

    /// <summary>
    /// Register a message converter for specific source and target types.
    /// </summary>
    /// <typeparam name="TSource">Source type from CryptoExchange.Net</typeparam>
    /// <typeparam name="TTarget">Target StockSharp message type</typeparam>
    /// <param name="converter">The converter instance</param>
    public void RegisterConverter<TSource, TTarget>(IMessageConverter<TSource, TTarget> converter)
        where TTarget : Message
    {
        var key = (typeof(TSource), typeof(TTarget));
        _converters.TryAdd(key, converter);
    }

    /// <summary>
    /// Convert object using registered converter.
    /// </summary>
    /// <typeparam name="TSource">Source type</typeparam>
    /// <typeparam name="TTarget">Target message type</typeparam>
    /// <param name="source">Source object</param>
    /// <param name="securityId">Security identifier</param>
    /// <returns>Converted message</returns>
    /// <exception cref="InvalidOperationException">When no converter is registered for the types</exception>
    public TTarget Convert<TSource, TTarget>(TSource source, SecurityId securityId)
        where TTarget : Message
    {
        var key = (typeof(TSource), typeof(TTarget));

        if (!_converters.TryGetValue(key, out var converter))
        {
            throw new InvalidOperationException(
                $"No converter registered for {typeof(TSource).Name} -> {typeof(TTarget).Name}");
        }

        if (converter is IMessageConverter<TSource, TTarget> typedConverter)
        {
            return typedConverter.Convert(source, securityId);
        }

        throw new InvalidOperationException(
            $"Converter type mismatch for {typeof(TSource).Name} -> {typeof(TTarget).Name}");
    }

    /// <summary>
    /// Convert object to StockSharp message (auto-detect target type).
    /// </summary>
    /// <param name="source">Source object from CryptoExchange.Net</param>
    /// <param name="securityId">Security identifier</param>
    /// <returns>Converted StockSharp message</returns>
    /// <exception cref="InvalidOperationException">When no suitable converter found</exception>
    public Message ConvertToStockSharp(object source, SecurityId securityId)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));

        var sourceType = source.GetType();

        // Find first compatible converter
        var converterEntry = _converters.FirstOrDefault(kvp =>
            kvp.Value.CanConvert(sourceType, kvp.Key.TargetType));

        if (converterEntry.Key == default)
        {
            throw new InvalidOperationException(
                $"No converter found for source type {sourceType.Name}");
        }

        return converterEntry.Value.Convert(source, securityId);
    }

    /// <summary>
    /// Check if converter exists for the specified types.
    /// </summary>
    /// <typeparam name="TSource">Source type</typeparam>
    /// <typeparam name="TTarget">Target message type</typeparam>
    /// <returns>True if converter is registered</returns>
    public bool HasConverter<TSource, TTarget>() where TTarget : Message
    {
        var key = (typeof(TSource), typeof(TTarget));
        return _converters.ContainsKey(key);
    }

    /// <summary>
    /// Get all registered converter types.
    /// </summary>
    /// <returns>Collection of (source, target) type pairs</returns>
    public IEnumerable<(Type SourceType, Type TargetType)> GetRegisteredTypes()
    {
        return _converters.Keys.ToArray();
    }

    /// <summary>
    /// Clear all registered converters.
    /// </summary>
    public void Clear()
    {
        _converters.Clear();
    }
}