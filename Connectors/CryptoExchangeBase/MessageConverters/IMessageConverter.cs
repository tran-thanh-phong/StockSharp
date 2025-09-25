namespace StockSharp.CryptoExchange.MessageConverters;

/// <summary>
/// Interface for converting between CryptoExchange.Net objects and StockSharp messages.
/// </summary>
/// <typeparam name="TSource">Source type from CryptoExchange.Net</typeparam>
/// <typeparam name="TTarget">Target StockSharp message type</typeparam>
public interface IMessageConverter<in TSource, out TTarget> : IMessageConverter
    where TTarget : Message
{
    /// <summary>
    /// Convert CryptoExchange.Net object to StockSharp message.
    /// </summary>
    /// <param name="source">Source object from CryptoExchange.Net</param>
    /// <param name="securityId">Security identifier for the message</param>
    /// <returns>Converted StockSharp message</returns>
    TTarget Convert(TSource source, SecurityId securityId);
}

/// <summary>
/// Non-generic base interface for message converters.
/// </summary>
public interface IMessageConverter
{
    /// <summary>
    /// Check if converter can handle the specified source and target types.
    /// </summary>
    /// <param name="sourceType">Source object type</param>
    /// <param name="targetType">Target message type</param>
    /// <returns>True if conversion is supported</returns>
    bool CanConvert(Type sourceType, Type targetType);

    /// <summary>
    /// Convert object to StockSharp message (non-generic version).
    /// </summary>
    /// <param name="source">Source object</param>
    /// <param name="securityId">Security identifier</param>
    /// <returns>Converted message</returns>
    Message Convert(object source, SecurityId securityId);
}