using ChildHDT.API.ApplicationServices;
using ChildHDT.Domain.DomainServices;
using ChildHDT.Infrastructure.InfrastructureServices;
using ChildHDT.Infrastructure.InfrastructureServices.Context;
using ChildHDT.Infrastructure.IntegrationServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using static Microsoft.AspNetCore.Http.StatusCodes;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new RoleJsonConverter());
    });

builder.Services.AddDbContext<ChildContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("PostgreSQL")), ServiceLifetime.Singleton);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<IUnitOfwork, UnitOfwork>();
builder.Services.AddSingleton<RepositoryChild>();
builder.Services.AddScoped<INotificationHandler, NotificationHandler>();
builder.Services.AddScoped<IStressService, PWAStressService>();

builder.Services.AddHostedService<StressMonitoringService>();

var app = builder.Build();

// Añadir esto ANTES de cualquier middleware:
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ChildContext>();
    db.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();
