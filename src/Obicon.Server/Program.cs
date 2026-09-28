using Obicon.Server.BackgroundServices;
using Obicon.Server.Configuration;
using Obicon.Server.Middleware;
using Obicon.Server.Services;
using Obicon.Server.WebSockets;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .WriteTo.Console()
    .ReadFrom.Configuration(context.Configuration));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddControllers();
builder.Services.AddSingleton<INodeService, NodeService>();
builder.Services.AddSingleton<ITestService, TestService>();
builder.Services.AddSingleton<ITestQueueService, TestQueueService>();
builder.Services.AddSingleton<NodeConnectionManager>();
builder.Services.AddSingleton<IConfigRepository, JsonConfigRepository>();
builder.Services.AddHostedService<TestQueueProcessor>();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo { Title = "Obicon API", Version = "v1" });
});
var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Obicon API v1"));

app.UseWebSockets();
app.UseMiddleware<AuthMiddleware>();
app.UseMiddleware<WebSocketMiddleware>();

app.MapControllers();

app.Run();
