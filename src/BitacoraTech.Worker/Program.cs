using BitacoraTech.Worker;
using BitacoraTech.Worker.Background;
using BitacoraTech.Infrastructure;
using BitacoraTech.Infrastructure.Configuration;

var builder = Host.CreateApplicationBuilder(args);
SecurityConfigurationValidator.Validate(builder.Configuration, builder.Environment);
builder.Services.AddBitacoraTechInfrastructure(builder.Configuration);
builder.Services.AddScoped<BackgroundWorkerRunner>();
builder.Services.AddScoped<IBackgroundWorkerStep, TrendWorkerStep>();
builder.Services.AddScoped<IBackgroundWorkerStep, ArticleWorkerStep>();
builder.Services.AddScoped<IBackgroundWorkerStep, ImageWorkerStep>();
builder.Services.AddScoped<IBackgroundWorkerStep, SeoWorkerStep>();
builder.Services.AddScoped<IBackgroundWorkerStep, PublishingQueueStep>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
