using Ocelot.DependencyInjection;
using Ocelot.Middleware;

var builder = WebApplication.CreateBuilder(args);

// secrets
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>();
}

// Configure Kestrel to use HTTPS on port 5001
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.ListenAnyIP(5000); // HTTP on port 5000
    serverOptions.ListenAnyIP(5001, listenOptions =>
    {
        listenOptions.UseHttps("aspnetapp.pfx", "dog1Open!"); // Ensure the correct path and password
    });
});

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOriginAngular",
        builder => builder
            .WithOrigins("https://localhost:4200") // Angular
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());

    options.AddPolicy("AllowSpecificOriginReact",
        builder => builder
            .WithOrigins("http://localhost:5173") // React
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Load Ocelot configuration from ocelot.json
builder.Configuration.AddJsonFile("ocelot.json");

builder.Services.AddOcelot(builder.Configuration); // Add Ocelot services

var app = builder.Build();

// Enable CORS
app.UseCors("AllowSpecificOriginAngular");
app.UseCors("AllowSpecificOriginReact");

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthorization();

// Register the endpoint to return "Hello World!"
app.MapGet("/", async context =>
{
    await context.Response.WriteAsync("Hello World!");
});

// Register controllers
app.MapControllers();

// Use Ocelot middleware
app.UseWebSockets();
app.UseOcelot().Wait();

app.Run();
