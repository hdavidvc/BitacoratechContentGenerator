using BitacoraTech.Worker;
using BitacoraTech.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddBitacoraTechInfrastructure(builder.Configuration);
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
