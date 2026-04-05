using CoGS.Sample.Components;
using CoGS.Sample.Events;
using CoGS.Server.Extensions;

var builder = WebApplication.CreateBuilder(args);

// builder.Services.AddCogs()
//     .FromConfiguration(builder.Configuration);
//
// var app = builder.Build();
//
// await app.RunAsync();

// -------------------------------------------------------------------
// Alternative: fully code-based pipeline with rules (no appsettings)
// -------------------------------------------------------------------

builder.Services.AddCogs(builder.Configuration)
    .FromPipeline(pipeline =>
    {
        pipeline.AddComponent<KafkaListener>("listener")
            .Configure<KafkaListenerOptions>(opts =>
            {
                opts.Topic = "orders-in";
                opts.ConsumerGroup = "order-service";
            })
            .SubscribesTo("listener", r => r.EventType<OrderReceived>());

        pipeline.AddComponent<OrderProcessor>("processor")
            .SubscribesTo("listener", r => r.EventType<OrderReceived>());

        pipeline.AddComponent<KafkaDispatcher>("dispatcher")
            .Configure<KafkaDispatcherOptions>(opts =>
            {
                opts.Topic = "orders-out";
            })
            .SubscribesTo("processor");
    });

// -------------------------------------------------------------------
// Alternative: hybrid — topology from code, options base from config
// -------------------------------------------------------------------
//
// builder.Services.AddCogs()
//     .FromPipeline(builder.Configuration, pipeline =>
//     {
//         pipeline.AddComponent<KafkaListener>("listener")
//             .Configure<KafkaListenerOptions>(opts =>
//             {
//                 opts.ProduceIntervalMs = 5000; // override config value
//             })
//             .SubscribesTo("listener", r => r.EventType<OrderReceived>());
//
//         pipeline.AddComponent<OrderProcessor>("processor")
//             .SubscribesTo("listener", r => r.EventType<OrderReceived>());
//
//         pipeline.AddComponent<KafkaDispatcher>("dispatcher")
//             .SubscribesTo("processor", r => r.EventType<OrderProcessed>());
//     });
