using atn062024.Models;
using atn062024.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<ISecretProvider, EnvSecretProvider>();

builder.Services.AddSingleton<IDataSourceProvider, PostgresDbDataSourceProvider>();

builder.Services.AddSingleton<ResiliencySettings>(ctx =>
    ctx.GetRequiredService<IConfiguration>().GetRequiredSection("ResiliencySettings").Get<ResiliencySettings>()
    ?? throw new InvalidOperationException("ResiliencySettings is null"));

builder.Services.AddSingleton<IDbService, PostgresDbService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

await using WebApplication app = builder.Build();

// Configure the HTTP request pipeline.
// enable swagger on prod for demo purposes
// if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

await app.RunAsync();
