using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using RoomReservation.Api.Common;
using RoomReservation.Api.Configuration;
using RoomReservation.Api.Data;
using RoomReservation.Api.Mapping;
using RoomReservation.Api.Repositories;
using RoomReservation.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Every request body is a small JSON document; the 30 MB Kestrel default would let one call tie up memory.
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 64 * 1024);

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

builder.Services.AddAutoMapper(configuration => configuration.AddMaps(typeof(MappingProfile).Assembly));
builder.Services.AddScoped<IRoomService, RoomService>();
builder.Services.AddScoped<IReservationService, ReservationService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IUsageReportService, UsageReportService>();
builder.Services.AddScoped<IReservationValidator, ReservationValidator>();
builder.Services.AddScoped<ReservationLifecycle>();

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
        options.JsonSerializerOptions.Converters.Add(new OffsetDateTimeConverter());
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

// The API only serves JSON: stop browsers from sniffing it as something else, framing it, or caching live data.
app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            headers["Cache-Control"] = "no-store";
        }
        return Task.CompletedTask;
    });
    await next();
});

// Unhandled exceptions are logged by the middleware; the client only gets the standard error body.
app.UseExceptionHandler(errorApp => errorApp.Run(context =>
{
    // Kestrel raises this when the body passes the size limit above; that is a client mistake, not a server fault.
    if (context.Features.Get<IExceptionHandlerFeature>()?.Error is BadHttpRequestException { StatusCode: StatusCodes.Status413PayloadTooLarge })
    {
        context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
        return context.Response.WriteAsJsonAsync(new ApiErrorResponse(
            [new ApiError(ErrorCodes.PayloadTooLarge, "The request body is too large.")]));
    }

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
