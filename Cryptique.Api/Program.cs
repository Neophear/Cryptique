using Cryptique.Data.Extensions;
using Cryptique.Logic;
using Cryptique.Logic.Extensions;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDataLayer();
builder.Services.AddLogicLayer();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapPost("/message", async ([FromBody] string messageData, IMessageService messageService) =>
    {
        var result = await messageService.AddMessageAsync(messageData);

        return Results.Ok(result);
    })
    .WithName("AddMessage")
    .WithOpenApi();

app.MapGet("/message/{id}", async (string id, IMessageService messageService) =>
    {
        var message = await messageService.GetMessageAsync(id);
        
        return message is null ? Results.NotFound(new {message = "Message not found"}) : Results.Ok(message);
    })
    .WithName("GetMessage")
    .WithOpenApi();

app.MapPost("/message/{id}/decrypt", async (string id, [FromBody] string key, IMessageService messageService) =>
    {
        var result = await messageService.DecryptMessageAsync(id, key);
        
        return result is null ? Results.NotFound(new {message = "Message not found"}) : Results.Ok(result);
    })
    .WithName("DecryptMessage")
    .WithOpenApi();

app.Run();
