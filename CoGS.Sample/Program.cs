using CoGS.Core;
using CoGS.Sample.Components;
using CoGS.Sample.Events;
using CoGS.Sample.Options;
using CoGS.Server;

var builder = WebApplication.CreateBuilder(args);

var features = builder.Configuration
    .GetSection("PipelineFeatures")
    .Get<PipelineFeatureOptions>() ?? new PipelineFeatureOptions();

builder.Services.AddCoGS(pipelines =>
{
    pipelines.AddPipeline("OrdersPipeline", pipeline =>
    {
        // Source: Kafka listener bound to the "Listeners:EU" config section.
        pipeline
            .AddComponent<SampleKafkaListener>("listener-eu", "Listeners:EU", ComponentRole.Source);

        // Processor: stateless pass-through that logs and re-emits.
        pipeline
            .AddComponent<OrderProcessor>("processor", role: ComponentRole.Processor)
            .SubscribeTo("listener-eu");

        // Sink: main dispatcher that receives all processed events.
        pipeline
            .AddComponent<SampleKafkaDispatcher>("dispatcher-main", "Dispatchers:Main", ComponentRole.Sink)
            .SubscribeTo("processor");

        // Conditional sink: only active when the feature flag is on.
        if (features.EnableHighValueDispatcher)
        {
            pipeline
                .AddComponent<SampleKafkaDispatcher>("dispatcher-high", "Dispatchers:HighValue", ComponentRole.Sink)
                .SubscribeTo("processor", rule => rule
                    .Where<OrderEvent>(e => e.Amount > 1000));
        }

        // Conditional sink: audit trail, controlled by config.
        if (features.EnableAuditDispatcher)
        {
            pipeline
                .AddComponent<AuditDispatcher>("dispatcher-audit", role: ComponentRole.Sink)
                .SubscribeTo("processor");
        }
    });
});

var app = builder.Build();

await app.RunAsync();
