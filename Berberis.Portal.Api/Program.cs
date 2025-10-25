using Berberis.Messaging;
using Berberis.Portal.Api.Hubs;
using Berberis.Portal.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add controllers
builder.Services.AddControllers();

// Add Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Berberis.Portal API",
        Version = "v1",
        Description = "Admin Dashboard & Monitoring API for Berberis CrossBar"
    });
});

// Add SignalR
builder.Services.AddSignalR();

// Configure CORS for frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
            "https://localhost:5001",
            "http://localhost:5000",
            "https://localhost:7001",
            "http://localhost:7000"
        )
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials();
    });
});

// Add CrossBar options
var crossBarOptions = new CrossBarOptions
{
    EnableMessageTracing = builder.Configuration.GetValue<bool>("CrossBar:EnableMessageTracing", false),
    EnableLifecycleTracking = builder.Configuration.GetValue<bool>("CrossBar:EnableLifecycleTracking", true),
    EnablePublishLogging = builder.Configuration.GetValue<bool>("CrossBar:EnablePublishLogging", false),
    DefaultBufferCapacity = builder.Configuration.GetValue<int?>("CrossBar:DefaultBufferCapacity"),
    MaxChannels = builder.Configuration.GetValue<int?>("CrossBar:MaxChannels")
};
builder.Services.AddSingleton(crossBarOptions);

// Add CrossBar instance
builder.Services.AddSingleton<ICrossBar>(sp =>
{
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
    var options = sp.GetRequiredService<CrossBarOptions>();
    return new CrossBar(loggerFactory, options);
});

// Add Portal services
builder.Services.AddSingleton<IPortalService, PortalService>();
builder.Services.AddSingleton<ErrorTrackingService>();
builder.Services.AddHostedService<EventStreamingService>();

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
if (builder.Environment.IsDevelopment())
{
    builder.Logging.SetMinimumLevel(LogLevel.Debug);
}
else
{
    builder.Logging.SetMinimumLevel(LogLevel.Information);
}

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Berberis.Portal API v1");
    });
}

app.UseHttpsRedirection();

// Serve static files (HTML, CSS, JS) from wwwroot
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseCors("AllowFrontend");

app.UseAuthorization();

app.MapControllers();
app.MapHub<EventsHub>("/hubs/events");

app.Run();
