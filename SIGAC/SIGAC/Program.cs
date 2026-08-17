using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using SIGAC.Application.Interfaces;
using SIGAC.Application.Services;
using SIGAC.Infrastructure.Data;
using SIGAC.Infrastructure.Repositories;
using SIGAC.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();

// DbContext de EF Core contra SQL Server Express local
builder.Services.AddDbContext<SigacDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("SigacDb")));

// Servicios del módulo de Beneficiarios y Asistencia
builder.Services.AddScoped<IBeneficiariosService, BeneficiariosService>();
builder.Services.AddScoped<IAsistenciaService, AsistenciaService>();
builder.Services.AddScoped<IInventarioService, InventarioService>();

// Repositorio de Beneficiarios con EF Core (reemplaza la versión temporal en memoria)
builder.Services.AddScoped<IBeneficiariosRepository, BeneficiariosRepositoryEfCore>();

// Repositorio de Asistencia TEMPORAL en memoria (aún sin migrar a EF Core)
builder.Services.AddSingleton<IAsistenciaRepository, AsistenciaRepositoryEnMemoria>();

// Repositorio de Inventario con EF Core
//builder.Services.AddScoped<IInventarioRepository, InventarioRepository>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();