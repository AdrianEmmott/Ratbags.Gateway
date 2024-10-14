using Microsoft.IdentityModel.Tokens;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using Ratbags.Core.Settings;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
var environment = builder.Environment;

// secrets
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>();
}

builder.Services.Configure<AppSettingsBase>(builder.Configuration);
var appSettings = builder.Configuration.Get<AppSettingsBase>() ?? throw new Exception("Appsettings missing");

var certificatePath = string.Empty;
var certificateKeyPath = string.Empty;

// are we in docker?
var isDocker = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true";

certificatePath = Path.Combine(appSettings.Certificate.Path, appSettings.Certificate.Name);

Console.WriteLine($"HTTP Port: {appSettings.Ports.Http}");
Console.WriteLine($"HTTPS Port: {appSettings.Ports.Https}");

builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.ListenAnyIP(Convert.ToInt32(appSettings.Ports.Http));
    serverOptions.ListenAnyIP(Convert.ToInt32(appSettings.Ports.Https), listenOptions =>
    {
        listenOptions.UseHttps(
            certificatePath,
            appSettings.Certificate.Password);
    });
});

// config ocelot for local / docker
builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("ocelot.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"ocelot.{environment.EnvironmentName}.json", optional: true, reloadOnChange: true);

// add services
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigins",
        builder => builder
            .WithOrigins("https://localhost:4200", "http://localhost:5173") // angular and react
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// setup identity with ocelot (this proj)
builder.Services.AddAuthentication()
    .AddJwtBearer("IdentityApiKey", options =>
    {
        options.Authority = "https://localhost:7158"; // account api
        options.Audience = "builder.Configuration[\"JWT:Audience\"]"; 
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = appSettings.JWT.Issuer,
            ValidAudience = appSettings.JWT.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(appSettings.JWT.Secret))
        };
    });

builder.Services.AddOcelot(builder.Configuration);
var app = builder.Build();

var ocelotConfig = builder.Configuration.GetSection("Routes").GetChildren();
foreach (var route in ocelotConfig)
{
    app.Logger.LogInformation($"Downstream Host: {route["DownstreamHostAndPorts:0:Host"]}");
    app.Logger.LogInformation($"Downstream Port: {route["DownstreamHostAndPorts:0:Port"]}");
}

// use cors
app.UseCors("AllowSpecificOrigins");

// setup dev
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.UseWebSockets();
app.UseOcelot().Wait();

app.Run();