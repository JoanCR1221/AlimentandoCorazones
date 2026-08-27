using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using SIGAC.Application.Interfaces;
using SIGAC.Application.Services;
using SIGAC.Infrastructure.Data;
using SIGAC.Infrastructure.Repositories;
using SIGAC.Components;
using SIGAC.Services;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddScoped<AparienciaService>();

builder.Services.AddMudServices();

// DbContext de EF Core contra SQL Server Express local.
// Factory en lugar de AddDbContext: en Blazor Server el scope dura toda la
// sesión (circuito), no cada clic, así que un DbContext inyectado directo
// queda compartido entre operaciones concurrentes y EF Core no tolera eso
// (DbContext no es seguro para usarse desde dos operaciones a la vez). Cada
// repositorio pide un contexto nuevo y de corta vida por operación.
builder.Services.AddDbContextFactory<SigacDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("SigacDb")));

// Servicios del módulo de Beneficiarios y Asistencia
builder.Services.AddScoped<IBeneficiariosService, BeneficiariosService>();
builder.Services.AddScoped<IAsistenciaService, AsistenciaService>();

// Servicio del módulo de Control de Inventario
builder.Services.AddScoped<IInventarioService, InventarioService>();


// Repositorio de Beneficiarios con EF Core (reemplaza la versión temporal en memoria)
builder.Services.AddScoped<IBeneficiariosRepository, BeneficiariosRepositoryEfCore>();

// Repositorio de Asistencia TEMPORAL en memoria (aún sin migrar a EF Core)
builder.Services.AddScoped<IAsistenciaRepository, AsistenciaRepositoryEfCore>();

// Repositorio de Inventario TEMPORAL en memoria (aún sin EF Core del lado del backend)
builder.Services.AddScoped<IInventarioRepository, InventarioRepositoryEnMemoria>();


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