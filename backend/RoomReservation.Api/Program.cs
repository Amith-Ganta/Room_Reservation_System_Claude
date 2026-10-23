using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using RoomReservation.Api.Common;
using RoomReservation.Api.Configuration;
using RoomReservation.Api.Data;
using RoomReservation.Api.Mapping;
using RoomReservation.Api.Repositories;
using RoomReservation.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddOptions<BookingRulesOptions>()
    .Bind(builder.Configuration.GetSection(BookingRulesOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<UsageReportOptions>()
    .Bind(builder.Configuration.GetSection(UsageReportOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton(TimeProvider.System);

var databaseName = builder.Configuration["Database:Name"];
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseInMemoryDatabase(string.IsNullOrWhiteSpace(databaseName) ? "RoomReservation" : databaseName));
builder.Services.AddScoped<IRoomRepository, RoomRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IReservationRepository, ReservationRepository>();

builder.Services.AddAutoMapper(typeof(MappingProfile).Assembly);
builder.Services.AddScoped<IRoomService, RoomService>();
builder.Services.AddScoped<IReservationValidator, ReservationValidator>();
builder.Services.AddScoped<ReservationLifecycle>();

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
        // Malformed JSON must not echo System.Text.Json internals back to the client.
        options.AllowInputFormatterExceptionMessages = false;
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = InvalidModelStateResponse.Create;
        // Keep a single error shape: no ProblemDetails for 404/405/415.
        options.SuppressMapClientErrors = true;
    });

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Configuration.GetValue("Seed:Enabled", false))
{
    using var scope = app.Services.CreateScope();
    await DbSeeder.SeedAsync(
        scope.ServiceProvider.GetRequiredService<AppDbContext>(),
        scope.ServiceProvider.GetRequiredService<TimeProvider>());
}

// Unhandled exceptions are logged by the middleware; the client only gets the standard error body.
app.UseExceptionHandler(errorApp => errorApp.Run(context =>
{
    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    return context.Response.WriteAsJsonAsync(new ApiErrorResponse(
        [new ApiError(ErrorCodes.InternalError, "An unexpected error occurred.")]));
}));

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.MapControllers();

app.Run();

// Exposed so WebApplicationFactory<Program> can host the app in tests.
public partial class Program;
