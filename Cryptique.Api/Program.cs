using Cryptique.Data.Extensions;
using Cryptique.DataTransferObjects.Requests;
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

app.MapPost("/message", async (CreateMessageRequest messageData, IMessageService messageService) =>
    {
        if (messageData.Message.Length > 1000)
            return Results.BadRequest(new {message = "Message is too long"});
        
        var result = await messageService.AddMessageAsync(messageData.Message);

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

app.MapPost("/message/{id}/decrypt", async (string id, DecryptMessageRequest request, IMessageService messageService) =>
    {
        // Verify if key is a valid base64 string
        byte[] key;
        try
        {
            key = Convert.FromBase64String(request.Key);
        }
        catch (Exception e)
        {
            return Results.BadRequest(new {message = "Invalid key"});
        }
        
        var result = await messageService.DecryptMessageAsync(id, key);
        
        return result is null ? Results.NotFound(new {message = "Message not found"}) : Results.Ok(result);
    })
    .WithName("DecryptMessage")
    .WithOpenApi();

app.Run();
