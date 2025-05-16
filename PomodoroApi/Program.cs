using Microsoft.EntityFrameworkCore;
using PomodoroApi.Data;

var builder = WebApplication.CreateBuilder(args);

// Kestrel'� a��k�a yap�land�r
//builder.WebHost.ConfigureKestrel(serverOptions =>
//{
//    serverOptions.ListenAnyIP(80);
//});

// Add services to the container.
builder.Services.AddControllers();

// CORS ayarlar�n� ekle
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy => policy
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader());
});

// Docker ortam� i�in ba�lant� dizesini kontrol et ve d�zelt
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var dockerConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");

if (!string.IsNullOrEmpty(dockerConnectionString))
{
    connectionString = dockerConnectionString;
}

// Add DbContext
builder.Services.AddDbContext<PomodoroDbContext>(options =>
    options.UseSqlServer(connectionString));

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp",
        builder => builder.WithOrigins("http://localhost:3000")
                          .AllowAnyHeader()
                          .AllowAnyMethod());
});

// Learn more about configuring Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}


// CORS middleware'ini ekle
app.UseCors("AllowAll");

app.UseAuthorization();

// MapControllers �a�r�s�ndan hemen sonra
app.MapControllers();

// Endpoint'leri logla
var endpointDataSource = app.Services.GetRequiredService<EndpointDataSource>();
foreach (var endpoint in endpointDataSource.Endpoints)
{
    if (endpoint is RouteEndpoint routeEndpoint)
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Endpoint {endpoint} is available at route pattern {routePattern}",
            routeEndpoint.DisplayName, routeEndpoint.RoutePattern.RawText);
    }
}

// Veritaban�n� otomatik olarak olu�tur ve migrationlar� uygula
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<PomodoroDbContext>();
        context.Database.Migrate(); // Bu, t�m bekleyen migrationlar� uygular

        // DefaultUser'�n var olup olmad���n� kontrol et
        if (!context.Users.Any(u => u.Id == "defaultUser"))
        {
            context.Users.Add(new PomodoroApi.Models.User
            {
                Id = "defaultUser",
                Username = "Default User",
                Email = "default@example.com"
            });
            context.SaveChanges();
        }
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Veritaban� migration veya seed i�lemi s�ras�nda bir hata olu�tu.");
    }
}

app.Run();