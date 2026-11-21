using TourFlow.API.Middleware;
using TourFlow.API.Authentication;
using TourFlow.API.Operations;
using TourFlow.API.Backend;
using TourFlow.Application;
using TourFlow.Infrastructure;
using TourFlow.Infrastructure.Authentication;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.AddTourFlowSecurity();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment.IsDevelopment());

var app = builder.Build();

app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (app.Environment.IsDevelopment() && app.Configuration.GetValue<bool>("AuthBootstrap:Run"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var email = app.Configuration["AuthBootstrap:Email"] ?? throw new InvalidOperationException("Configure AuthBootstrap:Email.");
    var password = app.Configuration["AuthBootstrap:Password"] ?? throw new InvalidOperationException("Configure AuthBootstrap:Password.");
    await scope.ServiceProvider.GetRequiredService<DevelopmentAdministrator>().CreateAsync(email.Trim(), password, CancellationToken.None);
    return;
}

app.MapTourFlowAccounts();
app.MapOperations();
app.MapBackend();
app.MapHealthChecks("/health").AllowAnonymous();
app.MapGet("/", () => Results.Ok(new { name = "TourFlow API", status = "running" }))
    .AllowAnonymous();

app.Run();

public partial class Program;
