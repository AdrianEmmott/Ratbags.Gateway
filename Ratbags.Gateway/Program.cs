using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using Ratbags.Shared.DTOs.Events.AppSettingsBase;

var builder = WebApplication.CreateBuilder(args);

// secrets
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>();
}


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

// add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// config ocelot
builder.Configuration.AddJsonFile("ocelot.json");

// setup identity with ocelot (this proj)
builder.Services.AddAuthentication()
    .AddJwtBearer("IdentityApiKey", options =>
    {
        options.Authority = "https://localhost:5000"; // Identity Server or Auth endpoint
        options.Audience = "api";
    });

builder.Services.AddOcelot(builder.Configuration);

var app = builder.Build();

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

app.UseAuthorization();

// "/" Hello World!
app.MapGet("/", async context =>
{
    await context.Response.WriteAsync("Hello World!");
});

// controllers
app.MapControllers();

// ocelot middleware
app.UseWebSockets();
app.UseOcelot().Wait();

app.Run();
