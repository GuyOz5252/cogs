using CoGS.Sample.Components;
using CoGS.Server;

var builder = WebApplication.CreateBuilder(args);

builder.AddCogs();
builder.AddComponent<KafkaListenerComponent>("KafkaLister1")
    .Configure<KafkaListenerComponentOptions>();
builder.AddComponent<KafkaListenerComponent>("KafkaLister2");
    .Configure<KafkaListenerComponentOptions>();
builder.AddComponent<SomeComponent>("SomeComponent")
    .SubscribeTo("KafkaLister1")
    .SubscribeTo("KafkaLister2");
builder.AddComponent<KafkaDispatcherComponent>("KafkaDispatcher")
    .SubscribeTo("SomeComponent");

var app = builder.Build();

app.UseCogs();

await app.RunAsync();
