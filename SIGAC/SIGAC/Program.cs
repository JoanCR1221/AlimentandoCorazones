using MudBlazor.Services;
using SIGAC.Application.Interfaces;
using SIGAC.Application.Services;
using SIGAC.Infrastructure.Repositories;
using SIGAC.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();

// Servicios del módulo de Beneficiarios y Asistencia
builder.Services.AddScoped<IBeneficiariosService, BeneficiariosService>();
builder.Services.AddScoped<IAsistenciaService, AsistenciaService>();

// Repositorios TEMPORALES en memoria (reemplazar por EF Core cuando conectes la BD real)
builder.Services.AddSingleton<IBeneficiariosRepository, BeneficiariosRepositoryEnMemoria>();
builder.Services.AddSingleton<IAsistenciaRepository, AsistenciaRepositoryEnMemoria>();

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