using AeroSyncAPI.DataAccessLayer;
using AeroSyncAPI.Hubs;
using AeroSyncAPI.Security;
using AeroSyncAPI.Services;
using LLama;
using LLama.Common;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var modelPath = Path.Combine(AppContext.BaseDirectory, "AI", "Phi-3-mini-4k-instruct-q4.gguf"); // Update to your exact path
var modelParams = new ModelParams(modelPath)
{
    ContextSize = 2048,
    GpuLayerCount = 0 // Stays 0 since we are running on CPU
};

// 2. 🚀 LOAD THE WEIGHTS ONCE AT STARTUP (This will trigger the black console logs right when the server starts)
var weights = LLamaWeights.LoadFromFile(modelParams);

// 3. Register the weights and parameters as Singletons so they stay in RAM forever
builder.Services.AddSingleton(weights);
builder.Services.AddSingleton(modelParams);
builder.Services.AddSingleton<SmartIntentService>(sp => new SmartIntentService(weights, modelParams));
builder.Services.AddSingleton<AesEncryptionService>();
builder.Services.AddScoped<SmartItineraryService>();
builder.Services.AddScoped<SmartMatcherService>();

builder.Services.AddSingleton<AuthService>();
// Add services to the container.
builder.Services.AddAuthentication("Paseto")
    .AddScheme<AuthenticationSchemeOptions, PasetoAuthenticationHandler>("Paseto", null);
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("NcDeveloper")));
builder.Services.AddControllers();
builder.Services.AddSignalR();

var app = builder.Build();

// Configure the HTTP request pipeline.


app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<TerminalHub>("/terminalhub");
app.Run();
