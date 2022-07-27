using System.Net.Mime;
using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;

// Create hostbuilder
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Add exception filter
app.UseExceptionHandler(configure =>
    configure.Run(async context =>
    {
        var ex = context.Features.Get<IExceptionHandlerPathFeature>();

        if (string.IsNullOrWhiteSpace(ex?.Error.Message)) return;

        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        context.Response.ContentType = MediaTypeNames.Application.Json;

        await context.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            errors = new string[]
            {
                ex.Error.Message
            }
        }));
    })
);


// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();

app.Run();