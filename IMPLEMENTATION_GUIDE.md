# MyLib.Core — Pipeline Framework: Implementation Guide

> **Status:** In active design. Decisions marked 🟢 are final. Decisions marked 🟡 are open questions with a preferred direction. Decisions marked 🔴 are known problems without a solution yet.

---

## Table of Contents

1. [Context & Goals](#1-context--goals)
2. [What We Are Replacing](#2-what-we-are-replacing)
3. [Architecture Overview](#3-architecture-overview)
4. [Core Concepts](#4-core-concepts)
5. [API Design](#5-api-design)
6. [Implementation Guide](#6-implementation-guide)
7. [Per-Instance Configuration](#7-per-instance-configuration)
8. [Open Questions](#8-open-questions)
9. [Out of Scope](#9-out-of-scope)

---

## 1. Context & Goals

### Background

`MyLib.Core` is an internal .NET library owned by the platform team. It provides the building blocks for data pipeline microservices: Kafka listeners, event processors, rule filters, state management, and Kafka dispatchers. All components communicate via an in-memory event bus built on `System.Threading.Channels`.

Today, a pipeline service is bootstrapped by a JSON configuration file parsed at startup. Component types are resolved via Newtonsoft.Json type-name-handling, dependencies are wired by Castle.Windsor, and per-instance configuration is carried by factory classes declared in the JSON.

This works, but it has significant friction:

- Every new service requires a large JSON config file with verbose type-name-handling syntax
- Castle.Windsor installers are not familiar to developers used to modern .NET
- Factories are a library-specific concept that developers have to learn
- Configuration and code are separated in a way that makes the pipeline topology hard to read
- There is no IntelliSense, compile-time validation, or refactoring support for the JSON

### Goals of the Modernization

- 🟢 Replace JSON config bootstrapping with a fluent C# API in `Program.cs`
- 🟢 Replace Castle.Windsor with `Microsoft.Extensions.DependencyInjection`
- 🟢 Eliminate factory classes entirely — components are referenced by type directly
- 🟢 Per-instance config (e.g. Kafka topic) is bound from `appsettings.json` sections, declared alongside the component in the fluent API
- 🟢 Integrate with the standard .NET Generic Host (`IHostedService`, `IConfiguration`, `ILogger`)
- 🟢 Preserve all existing topologies: linear, fan-out, fan-in, conditional routing
- 🟢 Validate the pipeline graph eagerly at startup, not at first event

### Non-Goals

- This is not a rewrite of the component runtime (channels, event bus, state management)
- This is not a public-facing API — internal library only
- Source generation is a future concern, not part of this phase

---

## 2. What We Are Replacing

Understanding the old system is essential for making correct design decisions in the new one.

### Old JSON Configuration Structure

```json
{
  "ExpressionResolverFactory": {
    // Resolves expressions like ${Consul:my-key} or ${Env:MY_VAR}
    // in the rest of the config at parse time
  },
  "ServerConfiguration": {
    "Components": {
      "$type": "System.Collections.Generic.List`1[[MyLib.Core.ComponentInitializationInfo, MyLib.Core]]",
      "$values": [
        {
          "$type": "MyLib.Core.ComponentInitializationInfo, MyLib.Core",
          "ComponentFactory": {
            "$type": "MyService.Factories.KafkaListenerFactory, MyService",
            "ComponentName": "listener-eu",
            "Topic": "orders-eu",
            "GroupId": "order-service"
          },
          "Subscriptions": { "$values": [] }
        },
        {
          "$type": "MyLib.Core.ComponentInitializationInfo, MyLib.Core",
          "ComponentFactory": {
            "$type": "MyService.Factories.OrderProcessorFactory, MyService",
            "ComponentName": "processor"
          },
          "Subscriptions": {
            "$values": [
              {
                "$type": "MyLib.Core.ConditionalSubscription, MyLib.Core",
                "ComponentName": "listener-eu",
                "Rule": { /* rule factory reference */ }
              }
            ]
          }
        }
      ]
    }
  },
  "CommonCastleConfiguration": {
    "Installers": {
      // Windsor installer declarations
    }
  }
}
```

### Key Observations About the Old Design

**Factories carried per-instance config.** `KafkaListenerFactory` had properties like `Topic` and `GroupId`. Two `KafkaListener` instances in the same pipeline were differentiated by having two separate factory objects in the JSON, each with different property values. When factories are removed, this role must be taken over by the fluent API + `IConfiguration`.

**Subscriptions were declared on the receiver.** `processor` declares that it subscribes to `listener-eu`, not the other way around. This pull model is preserved in the new design.

**Rules lived on the subscription, not the component.** A `ConditionalSubscription` had a `Rule` that filtered which events passed through. This maps directly to the `.SubscribeTo("name", rule => ...)` pattern in the new API.

**Factories received `IWindsorContainer` in `Create()`** and used it as a service locator. This is the pattern being broken. The replacement is constructor injection via `ActivatorUtilities.CreateInstance`.

### Old Factory Contract (Being Removed)

```csharp
// OLD — every downstream service implemented this
public interface IComponentFactory
{
    string ComponentName { get; }
    ComponentBase Create(IWindsorContainer container); // ← this signature is gone
}

// OLD — a typical factory
public class KafkaListenerFactory : IComponentFactory
{
    public string ComponentName { get; set; }
    public string Topic { get; set; }       // ← per-instance config lived here
    public string GroupId { get; set; }

    public ComponentBase Create(IWindsorContainer container)
    {
        var listener = container.Resolve<KafkaListenerComponent>();
        listener.Configure(Topic, GroupId);  // ← manual wiring after creation
        return listener;
    }
}
```

### Windsor Installers (Being Replaced)

```csharp
// OLD — CommonCastleConfiguration installer
public class MyServiceInstaller : IWindsorInstaller
{
    public void Install(IWindsorContainer container, IConfigurationStore store)
    {
        container.Register(
            Component.For<IKafkaClient>()
                     .ImplementedBy<KafkaClient>()
                     .LifestyleSingleton(),
            Component.For<IOrderRepository>()
                     .ImplementedBy<OrderRepository>()
                     .LifestyleSingleton()
        );
    }
}

// NEW — standard IServiceCollection in Program.cs
services.AddSingleton<IKafkaClient, KafkaClient>();
services.AddSingleton<IOrderRepository, OrderRepository>();
// or grouped:
services.AddMyServiceDependencies();
```

---

## 3. Architecture Overview

### Component Model

The runtime model is unchanged. A pipeline is a directed graph of components connected by `System.Threading.Channels`. Every object flowing through the pipeline implements `IEvent`. Every component inherits `ComponentBase`.

```
[KafkaListener] ──channel──► [RuleFilter] ──channel──► [OrderProcessor] ──channel──► [KafkaDispatcher]
                                  ↑
                            (subscription rule
                             filters events here)
```

What changes is only how the graph is **declared** and **bootstrapped** — not how it runs.

### Layers

```
┌─────────────────────────────────────────────────────┐
│                    Program.cs                        │
│         services.AddPipeline(pipeline => ...)        │  ← Developer writes this
└─────────────────────┬───────────────────────────────┘
                      │
┌─────────────────────▼───────────────────────────────┐
│              Fluent API Layer                         │
│   PipelineBuilder, ComponentBuilder, RuleBuilder      │  ← Library (new)
│   Produces: PipelineGraph                             │
└─────────────────────┬───────────────────────────────┘
                      │
┌─────────────────────▼───────────────────────────────┐
│              Bootstrap Layer                          │
│           PipelineHostedService                       │  ← Library (new)
│   Reads PipelineGraph, activates components,          │
│   wires channels, starts/stops lifecycle              │
└─────────────────────┬───────────────────────────────┘
                      │
┌─────────────────────▼───────────────────────────────┐
│              Component Runtime                        │
│   ComponentBase, IEvent, Channels, State              │  ← Library (existing, unchanged)
└─────────────────────────────────────────────────────┘
```

### Project Structure

```
MyLib.Core/
├── IEvent.cs                          # Marker interface for all events
├── ComponentBase.cs                   # Base class for all components
├── Pipeline/
│   ├── PipelineServiceCollectionExtensions.cs   # Entry point: services.AddPipeline()
│   ├── PipelineBuilder.cs             # Root fluent builder
│   ├── ComponentBuilder.cs            # Per-component fluent handle
│   ├── RuleBuilder.cs                 # Subscription rule DSL
│   ├── ComponentRegistration.cs       # Graph node (type + config section + subscriptions)
│   ├── SubscriptionDescriptor.cs      # Graph edge (source name + optional rule)
│   ├── PipelineGraph.cs               # Immutable validated graph
│   └── PipelineHostedService.cs       # IHostedService bootstrap
```

---

## 4. Core Concepts

### IEvent

Every object that flows through the pipeline must implement `IEvent`. This is a marker interface — no members required.

```csharp
public interface IEvent { }

// Example event
public record OrderEvent(string OrderId, decimal Amount, string Region) : IEvent;
```

### ComponentBase

Every pipeline component inherits `ComponentBase`. The base class manages:
- The inbound `System.Threading.Channel`
- Subscription registration and filtering
- Lifecycle: `Start()` and `StopAsync()`

Components receive their dependencies through the **constructor only**. There is no property injection, no post-creation configuration method, and no factory.

```csharp
public class KafkaListener : ComponentBase
{
    // Dependencies injected by DI, options injected via IOptions<T>
    public KafkaListener(IOptions<KafkaListenerOptions> options, IKafkaClient kafka, ILogger<KafkaListener> logger)
    {
        // store and use
    }
}
```

### ComponentRegistration

The internal model for one node in the pipeline graph. Replaces `ComponentInitializationInfo` from the old JSON config. Holds:
- `Name` — unique within the pipeline, used for subscription wiring
- `ComponentType` — the concrete `Type` to activate
- `ConfigSection` — optional `IConfiguration` path for per-instance options
- `OptionsType` — inferred from the constructor; the `T` in `IOptions<T>` if present
- `Subscriptions` — list of `SubscriptionDescriptor`

### SubscriptionDescriptor

The internal model for one edge in the pipeline graph. Replaces `SubscriptionBase` / `ConditionalSubscription`. Holds:
- `SourceComponentName` — the component this subscription receives from
- `Rule` — `Func<IEvent, bool>?` — null means unconditional

### PipelineGraph

The immutable, validated result of the builder. Produced once at startup by `PipelineBuilder.Build()`, stored as a singleton, consumed by `PipelineHostedService`.

---

## 5. API Design

### Entry Point

```csharp
// Program.cs
services.AddPipeline(pipeline => pipeline
    .AddComponent<KafkaListener>("listener", "Listeners:Main")
    .AddComponent<OrderProcessor>()
    .AddComponent<KafkaDispatcher>("dispatcher", "Dispatchers:Main")
);
```

`AddPipeline` builds and validates the graph eagerly. Any misconfiguration (dangling subscription, duplicate name) throws at startup before the host is running.

### AddComponent Overloads

```csharp
// No name, no config — auto-named, no per-instance options
pipeline.AddComponent<OrderProcessor>()

// Config section only — auto-named, options bound from section
pipeline.AddComponent<KafkaListener>("Listeners:EU")

// Name + config section — explicit name, options bound from section
pipeline.AddComponent<KafkaListener>("listener-eu", "Listeners:EU")

// Name only — explicit name, no per-instance options
pipeline.AddComponent<OrderProcessor>("processor")
```

### Subscription Wiring

**Implicit (linear chains):** When `AddComponent` is called by chaining off another `ComponentBuilder`, the new component automatically subscribes to the previous one. No `.SubscribeTo()` needed.

```csharp
pipeline
    .AddComponent<KafkaListener>("listener", "Listeners:Main")
    .AddComponent<OrderProcessor>()          // implicitly subscribes to "listener"
    .AddComponent<KafkaDispatcher>("dispatcher", "Dispatchers:Main")  // implicitly subscribes to processor
```

**Explicit:** Call `.SubscribeTo()` to declare subscriptions manually. The moment you call `.SubscribeTo()` on a component, implicit wiring is disabled for that component — you own all its subscriptions.

```csharp
.AddComponent<OrderProcessor>("processor")
    .SubscribeTo("listener-eu")
    .SubscribeTo("listener-us")
```

**Conditional:** Pass a rule builder lambda to filter events on the subscription.

```csharp
.AddComponent<KafkaDispatcher>("dispatcher-high", "Dispatchers:HighValue")
    .SubscribeTo("processor", rule => rule
        .Where<OrderEvent>(e => e.Amount > 1000))
```

**`.And()`** steps back to the `PipelineBuilder` level, allowing a new branch to be started without an implicit subscription to the current component.

```csharp
pipeline
    .AddComponent<KafkaListener>("listener", "Listeners:Main")
    .AddComponent<OrderProcessor>("processor")
        .SubscribeTo("listener")
    .And()   // ← back to pipeline level, no implicit sub from processor
    .AddComponent<KafkaDispatcher>("dispatcher-a", "Dispatchers:A")
        .SubscribeTo("processor")
    .And()
    .AddComponent<KafkaDispatcher>("dispatcher-b", "Dispatchers:B")
        .SubscribeTo("processor")
```

### RuleBuilder DSL

```csharp
// Filter by type and predicate (AND logic across multiple Where calls)
rule.Where<OrderEvent>(e => e.Amount > 1000)
    .Where<OrderEvent>(e => e.Region == "EU")

// Filter by type only
rule.OfType<OrderEvent>()

// OR logic
rule.Where<OrderEvent>(e => e.Amount > 1000)
    .Or<OrderEvent>(e => e.CustomerTier == "VIP")
```

### Topology Examples

#### Linear

```csharp
services.AddPipeline(pipeline => pipeline
    .AddComponent<KafkaListener>("listener", "Listeners:Main")
    .AddComponent<OrderProcessor>()
    .AddComponent<KafkaDispatcher>("dispatcher", "Dispatchers:Main")
);
```

#### Fan-In

```csharp
services.AddPipeline(pipeline => pipeline
    .AddComponent<KafkaListener>("listener-eu", "Listeners:EU")
    .And()
    .AddComponent<KafkaListener>("listener-us", "Listeners:US")
    .And()
    .AddComponent<OrderProcessor>("processor")
        .SubscribeTo("listener-eu")
        .SubscribeTo("listener-us")
    .And()
    .AddComponent<KafkaDispatcher>("dispatcher", "Dispatchers:Main")
        .SubscribeTo("processor")
);
```

#### Fan-Out with Conditional Routing

```csharp
services.AddPipeline(pipeline => pipeline
    .AddComponent<KafkaListener>("listener", "Listeners:Main")
    .AddComponent<OrderProcessor>("processor")
        .SubscribeTo("listener")
    .And()
    .AddComponent<KafkaDispatcher>("dispatcher-high", "Dispatchers:HighValue")
        .SubscribeTo("processor", rule => rule
            .Where<OrderEvent>(e => e.Amount > 1000))
    .And()
    .AddComponent<KafkaDispatcher>("dispatcher-standard", "Dispatchers:Standard")
        .SubscribeTo("processor", rule => rule
            .Where<OrderEvent>(e => e.Amount <= 1000))
    .And()
    .AddComponent<KafkaDispatcher>("dispatcher-audit", "Dispatchers:Audit")
        .SubscribeTo("processor") // unconditional — audit receives everything
);
```

---

## 6. Implementation Guide

Implement in this order. Each step is independently testable.

### Step 1 — Core Abstractions

**Files:** `IEvent.cs`, `ComponentBase.cs`

No changes to `IEvent`. `ComponentBase` needs one new thing: a `Name` property set by the hosted service after activation, so components know their own name for logging and diagnostics.

```csharp
public abstract class ComponentBase
{
    public string Name { get; internal set; } = string.Empty;

    public void Subscribe(ComponentBase source, Func<IEvent, bool>? rule = null)
    {
        // wire the channel from source to this component
        // rule == null means accept all events
    }

    public abstract void Start();
    public abstract Task StopAsync(CancellationToken cancellationToken);
}
```

**Verify:** Existing components compile unchanged.

---

### Step 2 — Graph Model

**Files:** `SubscriptionDescriptor.cs`, `ComponentRegistration.cs`, `PipelineGraph.cs`

These are pure data classes with no logic. Implement them exactly as specified in section 4.

```csharp
public sealed class SubscriptionDescriptor
{
    public string SourceComponentName { get; }
    public Func<IEvent, bool>? Rule { get; }

    public SubscriptionDescriptor(string sourceComponentName, Func<IEvent, bool>? rule = null)
    {
        SourceComponentName = sourceComponentName;
        Rule = rule;
    }
}

public sealed class ComponentRegistration
{
    public string Name { get; }
    public Type ComponentType { get; }
    public string? ConfigSection { get; }
    public Type? OptionsType { get; }
    public IReadOnlyList<SubscriptionDescriptor> Subscriptions => _subscriptions;

    private readonly List<SubscriptionDescriptor> _subscriptions = new();

    internal ComponentRegistration(string name, Type componentType, string? configSection, Type? optionsType)
    {
        Name = name;
        ComponentType = componentType;
        ConfigSection = configSection;
        OptionsType = optionsType;
    }

    internal void AddSubscription(SubscriptionDescriptor sub) => _subscriptions.Add(sub);
}

public sealed class PipelineGraph
{
    public IReadOnlyList<ComponentRegistration> Components { get; }

    internal PipelineGraph(IReadOnlyList<ComponentRegistration> components)
        => Components = components;

    public ComponentRegistration? FindByName(string name)
        => Components.FirstOrDefault(c => c.Name == name);
}
```

**Verify:** Unit test that you can construct a graph manually and read it back correctly.

---

### Step 3 — RuleBuilder

**File:** `RuleBuilder.cs`

```csharp
public sealed class RuleBuilder
{
    private Func<IEvent, bool>? _compiled;

    public RuleBuilder Where<TEvent>(Func<TEvent, bool> predicate) where TEvent : IEvent
    {
        Func<IEvent, bool> typed = e => e is TEvent te && predicate(te);
        _compiled = _compiled is null ? typed : And(_compiled, typed);
        return this;
    }

    public RuleBuilder OfType<TEvent>() where TEvent : IEvent
        => Where<TEvent>(_ => true);

    public RuleBuilder Or<TEvent>(Func<TEvent, bool> predicate) where TEvent : IEvent
    {
        Func<IEvent, bool> typed = e => e is TEvent te && predicate(te);
        _compiled = _compiled is null ? typed : (e => _compiled(e) || typed(e));
        return this;
    }

    internal Func<IEvent, bool>? Build() => _compiled;

    private static Func<IEvent, bool> And(Func<IEvent, bool> a, Func<IEvent, bool> b)
        => e => a(e) && b(e);
}
```

**Verify:** Unit test all combinations — `Where`, `OfType`, `Or`, chained `Where` (AND semantics), mixed types.

---

### Step 4 — PipelineBuilder and ComponentBuilder

**Files:** `PipelineBuilder.cs`, `ComponentBuilder.cs`

This is the most complex piece. The builder has two responsibilities:

1. **Collecting registrations** — name resolution, duplicate detection, implicit subscription wiring
2. **Options type discovery** — reflecting on the component constructor to find `IOptions<T>`

#### Options Type Discovery

When a `ComponentRegistration` is created, the builder inspects the component's constructor to find if it takes an `IOptions<T>` parameter. This tells the hosted service which options type to bind from the config section.

```csharp
private static Type? DiscoverOptionsType(Type componentType)
{
    var ctor = componentType.GetConstructors()
        .OrderByDescending(c => c.GetParameters().Length)
        .FirstOrDefault();

    if (ctor is null) return null;

    foreach (var param in ctor.GetParameters())
    {
        var t = param.ParameterType;
        if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IOptions<>))
            return t.GetGenericArguments()[0];
    }

    return null;
}
```

#### Implicit vs Explicit Subscription Logic

The builder's `AddComponent` internal method accepts an `implicitSourceName`. If set, a subscription is added automatically. If the developer later calls `.SubscribeTo()` explicitly, the implicit subscription has already been added — **the developer takes responsibility for removing it if they don't want it** by not using the implicit chain (i.e., using `.And()` first and then explicit `.SubscribeTo()` calls).

> 🟡 **Open question:** Should calling `.SubscribeTo()` explicitly on a component that already has an implicit subscription *replace* the implicit one, or *add to* it? Current behavior is additive. This may be surprising if a developer calls `.And()` then forgets that their previous chain already wired an implicit sub.
>
> **Preferred direction:** Make implicit wiring opt-in per-component only when no `.And()` is called. If `.And()` is called before adding the next component, no implicit sub is added. Document this clearly.

#### Name Resolution

```csharp
// Priority order:
// 1. Explicit name passed to AddComponent<T>("my-name", ...)
// 2. Auto-generated: "{TypeName}_{counter}"
//
// The factory's ComponentName property is GONE.
// Names are now always controlled by the developer or auto-generated.
```

**Verify:** Test implicit wiring (chain), explicit wiring (`.SubscribeTo`), `.And()` breaks the chain, duplicate names throw, dangling subscriptions are caught by `Validate()`.

---

### Step 5 — PipelineServiceCollectionExtensions

**File:** `PipelineServiceCollectionExtensions.cs`

Simple — calls the builder, registers the graph and the hosted service.

```csharp
public static IServiceCollection AddPipeline(
    this IServiceCollection services,
    Action<PipelineBuilder> configure)
{
    var builder = new PipelineBuilder();
    configure(builder);

    var graph = builder.Build(); // validates here — throws on misconfiguration

    services.AddSingleton(graph);
    services.AddHostedService<PipelineHostedService>();

    return services;
}
```

**Verify:** Call `AddPipeline` in a test host and confirm `PipelineGraph` is resolvable as a singleton.

---

### Step 6 — PipelineHostedService

**File:** `PipelineHostedService.cs`

This is the bootstrap runtime. It runs when the host starts and:

1. Activates all components via `ActivatorUtilities.CreateInstance`
2. Wires subscriptions
3. Starts all components in declaration order
4. Stops them in reverse order on shutdown

#### Activation

For components **without** a config section: activate directly from the root `IServiceProvider`.

```csharp
var component = (ComponentBase)ActivatorUtilities.CreateInstance(_services, reg.ComponentType);
```

For components **with** a config section: see [Section 7 — Per-Instance Configuration](#7-per-instance-configuration) for the full problem statement and current approach.

#### Subscription Wiring

```csharp
foreach (var reg in _graph.Components)
{
    var subscriber = byName[reg.Name];
    foreach (var sub in reg.Subscriptions)
    {
        var source = byName[sub.SourceComponentName];
        subscriber.Subscribe(source, sub.Rule);
    }
}
```

#### Start/Stop Order

Components start in **declaration order** (sources before sinks). Components stop in **reverse declaration order** (sinks before sources) so the pipeline drains cleanly before sources stop producing.

**Verify:** Integration test with stub components that log when started/stopped/wired. Confirm correct order.

---

## 7. Per-Instance Configuration

This is the hardest problem in the design and is **not fully solved**. Read this section carefully.

### The Problem

Two `KafkaListener` components in the same pipeline must receive different `KafkaListenerOptions`. They are the same type, registered once in DI, but need different option values — `orders-eu` vs `orders-us`.

Standard `IOptions<T>` is a singleton — all resolutions of `IOptions<KafkaListenerOptions>` return the same value. This doesn't work.

### Current Approach 🟡

At activation time, for each component that has a config section, build a **child `IServiceCollection`** with `IOptions<TOptions>` bound specifically from that component's config section, then build a child `IServiceProvider` and activate from it.

```csharp
private ComponentBase Activate(ComponentRegistration reg)
{
    if (reg.ConfigSection is not null && reg.OptionsType is not null)
    {
        var child = BuildChildProvider(reg);
        return (ComponentBase)ActivatorUtilities.CreateInstance(child, reg.ComponentType);
    }

    return (ComponentBase)ActivatorUtilities.CreateInstance(_services, reg.ComponentType);
}

private IServiceProvider BuildChildProvider(ComponentRegistration reg)
{
    var child = new ServiceCollection();

    // Bind IOptions<TOptions> from the specific config section
    // using reflection to call Configure<TOptions>(section)
    BindOptionsFromSection(child, reg.OptionsType!, reg.ConfigSection!);

    // ⚠️ Known gap: parent registrations (IKafkaClient, etc.) are NOT
    // automatically available in the child container. See open question below.

    return child.BuildServiceProvider();
}
```

The options binding uses reflection to call `Configure<TOptions>(IConfigurationSection)`:

```csharp
private void BindOptionsFromSection(IServiceCollection services, Type optionsType, string section)
{
    var configSection = _configuration.GetSection(section);

    var method = typeof(OptionsConfigurationServiceCollectionExtensions)
        .GetMethods()
        .First(m => m.Name == "Configure"
                    && m.IsGenericMethod
                    && m.GetParameters().Length == 2
                    && m.GetParameters()[1].ParameterType == typeof(IConfiguration))
        .MakeGenericMethod(optionsType);

    method.Invoke(null, new object[] { services, configSection });
}
```

### Known Gap 🔴

**The child container does not inherit parent registrations.** A component that needs both `IOptions<KafkaListenerOptions>` (per-instance) and `IKafkaClient` (shared singleton from the root container) will fail to resolve `IKafkaClient` from the child container.

This is the primary unsolved problem.

### Candidate Solutions

**Option A — Keyed Services (.NET 8)**

Pre-register each component's options as a keyed singleton at `AddPipeline` time, keyed by component name. The component constructor receives `[FromKeyedServices("listener-eu")] IOptions<KafkaListenerOptions>` or similar. No child container needed — everything resolves from root.

Problem: requires components to use `[FromKeyedServices]` attributes, which is framework coupling in the component class.

**Option B — Named Options (`IOptionsSnapshot` / `IOptionsMonitor`)**

Use `services.Configure<KafkaListenerOptions>("listener-eu", section)` to register named options. Pass the component name into the component so it can resolve `IOptionsSnapshot<KafkaListenerOptions>.Get("listener-eu")`.

Problem: component must know its own name at construction time, which creates a bootstrapping dependency.

**Option C — Options factory delegate**

Register a factory delegate per component: `Func<KafkaListenerOptions>`. The component takes `Func<KafkaListenerOptions>` in its constructor. The hosted service registers the delegate keyed per component before activation.

Problem: changes the component contract in a non-standard way.

**Option D — Child scope with parent forwarding**

Build the child `ServiceCollection` by forwarding all parent singleton and transient registrations as factory delegates that resolve from the root `IServiceProvider`. Add the per-instance options on top.

Problem: requires enumerating root service descriptors, which is fragile and can miss registrations added late.

**Option E — `IServiceProviderFactory` / third-party child containers**

Use a container that supports proper child scopes with parent inheritance (Autofac, DryIoc). Parent registrations are visible from child scopes natively.

Problem: introduces a third-party container dependency, which is exactly what we're trying to move away from.

> 🔴 **No preferred direction yet.** Option B (named options) is the most idiomatic but requires the component to participate in the naming convention. Option D is the most transparent to component authors but is fragile. This needs a decision before `PipelineHostedService` can be considered production-ready.

### appsettings.json Convention

Regardless of which solution is chosen, config sections follow this convention:

```json
{
  "Listeners": {
    "EU": {
      "Topic": "orders-eu",
      "GroupId": "order-service-eu",
      "BootstrapServers": "kafka-eu:9092"
    },
    "US": {
      "Topic": "orders-us",
      "GroupId": "order-service-us",
      "BootstrapServers": "kafka-us:9092"
    }
  },
  "Dispatchers": {
    "HighValue": {
      "Topic": "orders-high-value",
      "BootstrapServers": "kafka-eu:9092"
    }
  }
}
```

And referenced in the fluent API by path:

```csharp
.AddComponent<KafkaListener>("listener-eu", "Listeners:EU")
.AddComponent<KafkaListener>("listener-us", "Listeners:US")
```

---

## 8. Open Questions

| # | Question | Status | Preferred Direction |
|---|----------|--------|---------------------|
| 1 | How does per-instance config reach components when multiple instances of the same type exist? | 🔴 Unsolved | See Section 7 |
| 2 | Should calling `.SubscribeTo()` explicitly remove an already-added implicit subscription? | 🟡 Leaning yes | Explicit `.SubscribeTo()` replaces any implicit sub on that component |
| 3 | Should `AddComponent` with only a config section string (no name) auto-generate the name? | 🟢 Decided | Yes — `"{TypeName}_{counter}"` |
| 4 | Should `ExpressionResolver` (Consul KV, env var interpolation) be replaced by `IConfiguration` providers? | 🟡 Leaning yes | Use community Consul `IConfiguration` provider; env vars are native |
| 5 | Should the fluent API support multiple named pipelines in one service? | 🟡 Not yet | Defer — no known use case today |
| 6 | Should rules support negation (`.Not<T>()`)? | 🟡 Low priority | Add if requested by teams |

---

## 9. Out of Scope

The following are explicitly not part of this modernization effort. They may be revisited later.

- **Component runtime internals** — `System.Threading.Channels` wiring, backpressure, bounded/unbounded channel configuration. The runtime is not changing.
- **State management** — stateful processors and their storage backends are unchanged.
- **Source generation** — attribute-based component discovery and boilerplate elimination. Planned for a future phase once the fluent API is stable.
- **Schema validation** — validating that a `KafkaListener` is not connected directly to another `KafkaListener`, or that a `KafkaDispatcher` has no downstream components. Type-level topology validation is a future concern.
- **Multiple pipeline support** — running more than one named pipeline in a single service host.
- **Hot reload** — changing pipeline topology without restarting the service.
- **Migration tooling** — automated conversion of existing JSON configs to the new fluent API. Teams will migrate manually.
