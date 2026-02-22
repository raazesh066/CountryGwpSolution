using CountryGwp.Core.Abstractions;
using CountryGwp.Core.Services;
using CountryGwp.Infrastructure.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseKestrel().UseUrls("http://localhost:9091");

builder.Services.AddControllers();
builder.Services.AddMemoryCache();
builder.Services.AddOpenApi();
builder.Services.AddCountryGwpInfrastructure(builder.Configuration);
builder.Services.AddScoped<ICountryGwpService, CountryGwpService>();

var app = builder.Build();

app.UseExceptionHandler(exceptionHandlerApp =>
{
    exceptionHandlerApp.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new
        {
            error = "An unexpected error occurred while processing the request."
        });
    });
});

app.MapOpenApi();
app.MapGet("/", () => Results.Ok(new { status = "CountryGwp API is running" }));
app.MapControllers();

app.Run();
