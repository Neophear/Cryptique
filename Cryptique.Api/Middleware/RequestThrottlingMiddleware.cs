using System.Collections.Concurrent;

namespace Cryptique.Api.Middleware;

public class RequestThrottlingMiddleware(RequestDelegate next)
{
    private readonly ConcurrentDictionary<string, Queue<DateTimeOffset>> _requests = new();

    public async Task InvokeAsync(HttpContext context)
    {
        // Only throttle POST requests to /message
        if (context.Request.Method != "POST" || context.Request.Path != "/message")
        {
            await next(context);
            return;
        }
        
        var ip = context.Connection.RemoteIpAddress?.ToString();
        
        if (string.IsNullOrEmpty(ip))
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsync("Invalid IP address");
            return;
        }

        if (_requests.TryGetValue(ip, out var requests))
        {
            while (requests.Count > 0 && requests.Peek() < DateTimeOffset.UtcNow.AddMinutes(-10))
            {
                requests.Dequeue();
            }

            if (requests.Count > 10)
            {
                context.Response.StatusCode = 429;
                await context.Response.WriteAsync("Too many requests");
                return;
            }

            requests.Enqueue(DateTimeOffset.UtcNow);
        }
        else
        {
            _requests.TryAdd(ip, new Queue<DateTimeOffset>(new[] { DateTimeOffset.UtcNow }));
        }

        await next(context);
    }
}
