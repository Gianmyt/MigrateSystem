using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Configurazione Ocelot
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

// JWT validation (token generati da Auth.Api)
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer("Bearer", options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "Auth.Api", // deve combaciare con Issuer di Auth.Api

            ValidateAudience = true,
            ValidAudience = "MigrationClients", // deve combaciare con Audience di Auth.Api

            ValidateLifetime = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                System.Text.Encoding.UTF8.GetBytes("o9tKlf7xJGUP86YQzPKDRqMIJOyWGblquqJ10oMRikw=!ABCDEF")), // stessa chiave di Auth.Api
            ValidateIssuerSigningKey = true
        };
    });

builder.Services.AddOcelot();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

await app.UseOcelot();

app.Run();
