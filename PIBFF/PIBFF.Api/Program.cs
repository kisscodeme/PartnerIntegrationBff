using FluentValidation;
using PIBFF.Api.Middleware;
using PIBFF.Application.Interfaces;
using PIBFF.Application.Services;
using PIBFF.Application.Validators;
using PIBFF.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
// MVC / Controllers
builder.Services.AddControllers();

// Swagger / OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Partner Integration BFF",
        Version = "v1",
        Description = "Receives, validates, verifies and queues partner transactions for legacy processing."
    });
});

// FluentValidation - scans the Application assembly for all IValidator<T> implementations
builder.Services.AddValidatorsFromAssemblyContaining<TransactionRequestValidator>();
// Application services
builder.Services.AddScoped<ITransactionProcessingService, TransactionProcessingService>();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHealthChecks();
var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Partner Integration BFF v1");
    });
}

app.UseHttpsRedirection();
app.UseMiddleware<TimeoutExceptionMiddleware>();
app.MapControllers();
app.MapHealthChecks("/health");
app.Run();

// Exposed for WebApplicationFactory-based integration tests.
public partial class Program { }
