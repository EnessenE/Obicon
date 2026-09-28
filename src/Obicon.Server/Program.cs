using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .WriteTo.Console()
    .ReadFrom.Configuration(context.Configuration));

builder.Services.AddOpenApi();

var app = builder.Build();

app.MapOpenApi("/openapi");

app.Run();
