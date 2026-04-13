using MeetingMinutes.Api.Services;
using OllamaSharp;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddScoped<TranscriptionService>();
builder.Services.AddOpenApi();

builder.Services.AddSingleton<OllamaApiClient>(_ =>
{
    var url = builder.Configuration["OllamaBaseUrl"] ?? "http://localhost:11434";
    return new OllamaApiClient(new Uri(url));
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapControllers();
app.Run();
