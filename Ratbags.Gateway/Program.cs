using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using Ratbags.Shared.DTOs.Events.AppSettingsBase;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
var environment = builder.Environment;

// secrets
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>();
}

builder.Services.Configure<AppSettingsBase>(builder.Configuration);
// hmm
//builder.Configuration
//    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)  // Base settings
//    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true);  // Environment-specific

builder.Services.Configure<AppSettingsBase>(builder.Configuration);
var appSettings = builder.Configuration.Get<AppSettingsBase>() ?? throw new Exception("Appsettings missing");

// config kestrel for https on 5001
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.ListenAnyIP(5000); // HTTP on port 5000
    serverOptions.ListenAnyIP(5001, listenOptions =>
    {
        listenOptions.UseHttps(appSettings.Certificate.Name, appSettings.Certificate.Password);
    });
});

// config cors
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOriginAngular",
        builder => builder
            .WithOrigins("https://localhost:4200") // angular
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());

    options.AddPolicy("AllowSpecificOriginReact",
        builder => builder
            .WithOrigins("http://localhost:5173") // react
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

// config ocelot for local / docker
//builder.Configuration.AddJsonFile("ocelot.json");
builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("ocelot.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"ocelot.{environment.EnvironmentName}.json", optional: true, reloadOnChange: true);
    

// add services
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
            ValidIssuer = builder.Configuration["JWT:Issuer"],
            ValidAudience = builder.Configuration["JWT:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["JWT:Secret"]))
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

// cors
app.UseCors("AllowSpecificOriginAngular");
app.UseCors("AllowSpecificOriginReact");

// config http request pipeline
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

// controllers
app.MapControllers();

// ocelot middleware
app.UseWebSockets();
app.UseOcelot().Wait();

app.Run();
