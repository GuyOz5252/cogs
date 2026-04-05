# CoGS Service Initialization

## Overview

```mermaid
flowchart TB
    subgraph programCs ["Program.cs"]
        MAIN["WebApplication.CreateBuilder(args)"]
        MAIN --> ADDCOGS["services.AddCogs()"]
        ADDCOGS --> TRIGGER[".FromConfiguration(config)
        or .FromPipeline(...)"]
        TRIGGER --> BUILD["app.Build()"]
        BUILD --> RUN["app.RunAsync()"]
    end
```

## Phase 1a — `AddCogs()` (Server Infrastructure)

```mermaid
flowchart TB
    ADDCOGS["AddCogs()"] --> CREATE_DESC["Create empty PipelineDescriptor"]
    CREATE_DESC --> REG_DESC["Register PipelineDescriptor singleton"]
    REG_DESC --> REG_ROUTER["Register EventRouter singleton"]
    REG_ROUTER --> REG_HOST["Register CogsHostedService"]
    REG_HOST --> RETURN["Return CogsServerBuilder"]
```

## Phase 1b — Pipeline Definition (Trigger)

### FromConfiguration path

```mermaid
flowchart TB
    FROM_CFG["FromConfiguration(config)"] --> SCAN["Auto-scan loaded assemblies for
    ComponentBase subclasses →
    Dictionary of string to Type
    KafkaListener → typeof KafkaListener
    OrderProcessor → typeof OrderProcessor
    KafkaDispatcher → typeof KafkaDispatcher"]

    SCAN --> BINDER["PipelineConfigurationBinder reads CoGS:Components"]

    BINDER --> FOR_EACH{"For each component section"}
    FOR_EACH --> RESOLVE["Resolve CLR type via dictionary"]
    FOR_EACH --> PARSE_SUB["Parse SubscribesTo + Rules"]
    FOR_EACH --> DISCOVER_OPTS["Discover IOptionsMonitor T from constructor"]
    FOR_EACH --> STORE_PATH["Store ConfigSectionPath"]

    RESOLVE --> CR["ComponentRegistration"]
    PARSE_SUB --> CR
    DISCOVER_OPTS --> CR
    STORE_PATH --> CR
    CR --> DESC["Add to PipelineDescriptor"]

    DESC --> REG["RegisterComponents()"]
```

### FromPipeline path

```mermaid
flowchart TB
    FROM_PIPE["FromPipeline(lambda) or
    FromPipeline of TDefinition"] --> BUILDER["Run lambda / TDefinition.Define()
    on PipelineBuilder"]

    BUILDER --> DESC["PipelineDescriptor populated
    via AddComponent calls"]

    DESC --> REG["RegisterComponents()"]
```

## Phase 2 — DI Registration (`RegisterComponents`)

```mermaid
flowchart TB
    REG_START["RegisterComponents()"] --> FOR_EACH{"For each ComponentRegistration"}

    FOR_EACH --> HAS_OPTS{"Has OptionsType?"}
    HAS_OPTS -- Yes --> BIND_OPTS["OptionsRegistrar.RegisterOptions()"]
    HAS_OPTS -- No --> KEYED

    BIND_OPTS --> HAS_SECTION{"Has ConfigSectionPath?"}
    HAS_SECTION -- Yes --> CONFIGURE["services.Configure T
    (name, configSection)
    Binds Topic, ConsumerGroup, etc."]
    HAS_SECTION -- No --> HAS_ACTION

    CONFIGURE --> HAS_ACTION{"Has ConfigureOptionsAction?"}
    HAS_ACTION -- Yes --> POST["services.PostConfigure T
    (name, action)
    Code-based overrides"]
    HAS_ACTION -- No --> KEYED

    POST --> KEYED["services.AddKeyedSingleton IComponent
    (name, factory)
    Factory uses ActivatorUtilities"]

    KEYED --> NEXT["Next component"]
    NEXT --> FOR_EACH
```

## Phase 3 — Runtime Startup (`CogsHostedService.StartAsync`)

```mermaid
flowchart TB
    HOST_START["Host calls CogsHostedService.StartAsync()"] --> CTS["Create linked CancellationTokenSource"]
    CTS --> FOR_EACH{"For each ComponentRegistration"}

    FOR_EACH --> RESOLVE_DI["Resolve keyed singleton IComponent
    ActivatorUtilities injects:
    • IOptionsMonitor T
    • ILogger T
    • any other dependencies"]

    RESOLVE_DI --> CHANNEL["Create Channel IEvent
    (unbounded, single reader)"]

    CHANNEL --> WIRE_PUB["Set component.Publisher =
    ComponentPublisher(name, router)"]

    WIRE_PUB --> WIRE_SUBS{"For each Subscription"}
    WIRE_SUBS --> ADD_ROUTE["router.AddRoute(
    sourceComponent,
    channel.Writer,
    rule)"]
    ADD_ROUTE --> WIRE_SUBS

    WIRE_SUBS -- Done --> CREATE_HOST["Create ComponentHost(
    component, channel.Reader, logger)"]

    CREATE_HOST --> STORE["Store in _managedComponents"]
    STORE --> FOR_EACH

    FOR_EACH -- All done --> START_ALL{"Start all ComponentHosts"}
    START_ALL --> COMP_START["component.StartAsync()
    e.g. KafkaListener spawns producer loop"]
    COMP_START --> INBOX_LOOP["Spawn ProcessInboxAsync
    background task reading from channel"]
    INBOX_LOOP --> PIPELINE_READY["Pipeline running"]
```

## Phase 4 — Runtime Event Flow

```mermaid
flowchart LR
    subgraph kafkaListener ["KafkaListener"]
        L_PRODUCE["ProduceEventsAsync loop"] --> L_PUBLISH["PublishAsync(OrderReceived)"]
    end

    subgraph eventRouter1 ["EventRouter"]
        L_PUBLISH --> ROUTE["RouteAsync('listener', event)"]
        ROUTE --> RULE_CHECK{"Rule: EventTypeRule
        OrderReceived?"}
        RULE_CHECK -- Pass --> WRITE_P["channel.Writer.WriteAsync
        → processor inbox"]
        RULE_CHECK -- Fail --> DROP1["Event dropped"]
    end

    subgraph orderProcessor ["OrderProcessor"]
        WRITE_P --> P_INBOX["ProcessInboxAsync reads event"]
        P_INBOX --> P_HANDLE["HandleEventAsync
        creates OrderProcessed"]
        P_HANDLE --> P_PUBLISH["PublishAsync(OrderProcessed)"]
    end

    subgraph eventRouter2 ["EventRouter"]
        P_PUBLISH --> ROUTE2["RouteAsync('processor', event)"]
        ROUTE2 --> RULE2{"Rule: EventTypeRule
        OrderProcessed?"}
        RULE2 -- Pass --> WRITE_D["channel.Writer.WriteAsync
        → dispatcher inbox"]
        RULE2 -- Fail --> DROP2["Event dropped"]
    end

    subgraph kafkaDispatcher ["KafkaDispatcher"]
        WRITE_D --> D_INBOX["ProcessInboxAsync reads event"]
        D_INBOX --> D_HANDLE["HandleEventAsync
        reads Options.Topic via
        optionsMonitor.Get(Name)
        logs dispatch"]
    end
```

## Options Pattern Flow

Options type is declared explicitly via the `[CogsComponent]` attribute:

```csharp
[CogsComponent("KafkaListener", OptionsType = typeof(KafkaListenerOptions))]
```

Three modes for providing option values:

```mermaid
flowchart TB
    subgraph configOnly ["Config-only (FromConfiguration)"]
        CO_ATTR["Attribute declares OptionsType"] --> CO_SECTION["Bind config section
        CoGS:Components:listener"]
        CO_SECTION --> CO_CONFIGURE["services.Configure
        KafkaListenerOptions
        ('listener', section)"]
    end

    subgraph pureCode ["Pure fluent (FromPipeline)"]
        PC_CALL[".Configure of KafkaListenerOptions
        (opts => ...)"] --> PC_POST["services.PostConfigure
        KafkaListenerOptions
        ('listener', action)"]
    end

    subgraph hybrid ["Hybrid (FromPipeline with config)"]
        HY_SECTION["Bind config section
        CoGS:Components:listener"] --> HY_CONFIGURE["services.Configure
        (base values from config)"]
        HY_CONFIGURE --> HY_CALL[".Configure of KafkaListenerOptions
        (opts => ...)"]
        HY_CALL --> HY_POST["services.PostConfigure
        (code overrides applied on top)"]
    end
```

```mermaid
flowchart TB
    subgraph componentResolution ["Component Resolution (runtime)"]
        DI["ActivatorUtilities.CreateInstance
        KafkaListener(
          IOptionsMonitor KafkaListenerOptions,
          ILogger KafkaListener
        )"]
        DI --> MONITOR["IOptionsMonitor injected by DI"]
    end

    subgraph componentUsage ["Component Usage"]
        MONITOR --> GET["optionsMonitor.Get(Name)
        where Name = 'listener'"]
        GET --> OPTS["KafkaListenerOptions {
        Topic = 'orders-in',
        ConsumerGroup = 'order-service',
        ProduceIntervalMs = 2000
        }"]
    end
```

## Environment-Specific Topology

```mermaid
flowchart TB
    subgraph devEnv ["Development (appsettings.json)"]
        DEV["listener → processor → dispatcher
        3 components"]
    end

    subgraph prodEnv ["Production (+ appsettings.Production.json)"]
        PROD["listener → processor → dispatcher
        listener → processor → approval-dispatcher
        4 components, different topics"]
    end

    subgraph stagingEnv ["Staging (+ appsettings.Staging.json)"]
        STAGE["listener → dispatcher
        2 components, processor skipped"]
    end

    ENV["ASPNETCORE_ENVIRONMENT"] --> |Development| DEV
    ENV --> |Production| PROD
    ENV --> |Staging| STAGE
```
