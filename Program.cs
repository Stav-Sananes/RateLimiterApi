using RateLimiterAPI.Middleware;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseMiddleware<RateLimitingMiddleware>();
//builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
//    ConnectionMultiplexer.Connect("localhost"));

//app.UseMiddleware<RedisRateLimitingMiddleware>();

app.UseMiddleware<RoleBasedRateLimitingMiddleware>();
app.UseMiddleware<PerEndpointRateLimitingMiddleware>();
app.UseMiddleware<WildcardRateLimitingMiddleware>();

app.MapGet("/", () => "Hello World!");
// Per End Point Rate Limit
app.MapGet("/api/test", () => "Test endpoint");
app.MapGet("/api/critical", () => "Critical endpoint");
app.MapGet("/api/general", () => "General endpoint");
//  endpoints for Wild Card
app.MapGet("/api/users/view", () => "View Users");
app.MapGet("/api/users/create", () => "Create User");
app.MapGet("/api/products/view", () => "View Products");
app.MapGet("/api/products/add", () => "Add Product");
app.MapGet("/api/general/info", () => "General Info");

app.Run();


