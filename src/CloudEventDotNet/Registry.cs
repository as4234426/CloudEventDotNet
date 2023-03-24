﻿using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace CloudEventDotNet;

internal delegate Task HandleCloudEventDelegate(IServiceProvider serviceProvider, CloudEvent @event, CancellationToken token);

/// <summary>
/// A registry of CloudEvent metadata and handlers
/// </summary>
public sealed class Registry
{
    internal readonly Dictionary<Type, CloudEventMetadata> _metadata = new();
    internal readonly Dictionary<CloudEventMetadata, HandleCloudEventDelegate> _handlerDelegates = new();
    internal readonly Dictionary<CloudEventMetadata, CloudEventHandler> _handlers = new();
    private readonly string _defaultSource;

    public string DefaultPubSubName { get; }

    public string DefaultTopic { get; }

    public string DefaultSource => _defaultSource;

    /// <summary>
    /// Constructor of Registry
    /// </summary>
    /// <param name="defaultPubSubName">The default PubSub name</param>
    /// <param name="defaultTopic">The default topic</param>
    /// <param name="defaultSource">The default source</param>
    public Registry(string defaultPubSubName, string defaultTopic, string defaultSource)
    {
        DefaultPubSubName = defaultPubSubName;
        DefaultTopic = defaultTopic;
        _defaultSource = defaultSource;
    }

    internal Registry Build(IServiceProvider services)
    {
        foreach (var (metadata, handlerDelegate) in _handlerDelegates)
        {
            var handler = ActivatorUtilities.CreateInstance<CloudEventHandler>(services, metadata, handlerDelegate);
            _handlers.TryAdd(metadata, handler);
        }
        return this;
    }

    internal void RegisterMetadata(Type eventDataType, CloudEventAttribute attribute)
    {
        var metadata = new CloudEventMetadata(
            PubSubName: attribute.PubSubName ?? DefaultPubSubName,
            Topic: attribute.Topic ?? DefaultTopic,
            Type: attribute.Type ?? eventDataType.Name,
            Source: attribute.Source ?? DefaultSource
        );
        _metadata.TryAdd(eventDataType, metadata);
    }

    internal CloudEventMetadata GetMetadata(Type eventDataType) => _metadata[eventDataType];

    internal bool TryGetHandler(CloudEventMetadata metadata, [NotNullWhen(true)] out CloudEventHandler? handler) => _handlers.TryGetValue(metadata, out handler);

    internal void RegisterHandler<TData>(CloudEventMetadata metadata)
    {
        _handlerDelegates.TryAdd(metadata, Handle);

        static Task Handle(IServiceProvider serviceProvider, CloudEvent @event, CancellationToken token)
        {
            var typedEvent = new CloudEvent<TData>(
                Id: @event.Id,
                Source: @event.Source,
                Type: @event.Type,
                Time: @event.Time,
                Data: @event.Data.Deserialize<TData>(JSON.DefaultJsonSerializerOptions)!,
                DataSchema: @event.DataSchema,
                Subject: @event.Subject
            )
            {
                Extensions = @event.Extensions
            };

            return serviceProvider.GetRequiredService<ICloudEventHandler<TData>>().HandleAsync(typedEvent, token);
        }
    }

    /// <summary>
    /// Get topics subscribed by specified pubsub
    /// </summary>
    /// <param name="pubSubName">The pubsub name</param>
    /// <returns></returns>
    public IEnumerable<string> GetSubscribedTopics(string pubSubName)
    {
        return _handlers.Keys
            .Where(m => m.PubSubName == pubSubName)
            .Select(m => m.Topic)
            .Distinct();
    }

    /// <summary>
    /// Show registered metadata and handlers
    /// </summary>
    /// <returns>Registered metadata and handlers</returns>
    public string Debug()
    {
        var sb = new StringBuilder();

        sb.AppendLine("Metadata:");
        foreach (var (key, value) in _metadata)
        {
            sb.AppendLine($"{key}: {value}");
        }

        sb.AppendLine("Handlers:");
        foreach (var (key, value) in _handlers)
        {
            sb.AppendLine($"{key}: {value}");
        }

        return sb.ToString();
    }

}
