using Cryptique.Api.HostedServices;
using Cryptique.Api.Middleware;
using Cryptique.Data.Extensions;
using Cryptique.DataTransferObjects.Exceptions;
using Cryptique.DataTransferObjects.Requests;
using Cryptique.Logic;
using Cryptique.Logic.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDataLayer();
builder.Services.AddLogicLayer();
builder.Services.AddCors();

// Add hosted services
builder.Services.AddHostedService<MessageCleanupService>();

// Add logging
builder.Services.AddApplicationInsightsTelemetry();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    // Add exception handling middleware so exceptions are not thrown to the client
    app.UseMiddleware<ExceptionHandlingMiddleware>();
    
    var corsString = app.Configuration["Cors"] ?? "https://cryptique.dev";
    
    var corsOrigins = corsString.Split(",");
    
    // Add cors for production
    app.UseCors(corsPolicyBuilder => corsPolicyBuilder
        .WithOrigins(corsOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod());
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    
    // Add cors for localhost
    app.UseCors(corsPolicyBuilder => corsPolicyBuilder
        .AllowAnyOrigin()
        .AllowAnyHeader()
        .AllowAnyMethod());
}

app.UseHttpsRedirection();

// Throttle requests from same IP
app.UseMiddleware<RequestThrottlingMiddleware>();

// Health check
app.MapGet("/health", () => Results.Ok(new {status = "Healthy"}));

app.MapPost("/message", async (CreateMessageRequest messageData, IMessageService messageService) =>
    {
        try
        {
            var result = await messageService.AddMessageAsync(messageData.Message, messageData.MaxAttempts,
                messageData.MaxDecrypts, messageData.Expiration);
        
            return Results.Ok(result);
        }
        catch (DataTooLongException e)
        {
            return Results.BadRequest(new {message = e.Message, data = new {e.AllowedSize, e.ActualSize}});
        }
    })
    .WithName("AddMessage")
    .WithOpenApi();

// Upload file
app.MapPost("/message/upload", async (IFormFile file, IMessageService messageService) =>
    {
        try
        {
            var dataStream = file.OpenReadStream();
            var result = await messageService.AddMessageAsync(dataStream, 0, 0, null);
        
            return Results.Ok(result);
        }
        catch (DataTooLongException e)
        {
            return Results.BadRequest(new {message = e.Message, data = new {e.AllowedSize, e.ActualSize}});
        }
    })
    .WithName("AddMessageFile")
    .WithOpenApi()
    .DisableAntiforgery();

app.MapPost("/message/{id}/decrypt", async (string id, DecryptMessageRequest request, IMessageService messageService) =>
    {
        // Verify if key is a valid base64 string
        byte[] key;
        try
        {
            key = Convert.FromBase64String(request.Key);
        }
        catch (Exception)
        {
            return Results.BadRequest(new {message = "Invalid key"});
        }
        
        var result = await messageService.DecryptMessageAsync(id, key);
        
        return result is null ? Results.NotFound(new {message = "Message not found"}) : Results.Ok(result);
    })
    .WithName("DecryptMessage")
    .WithOpenApi();

app.Run();
