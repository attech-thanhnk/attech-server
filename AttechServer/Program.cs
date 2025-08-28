using AttechServer.Applications.UserModules.Abstracts;
using AttechServer.Applications.UserModules.Implements;
using AttechServer.Infrastructures.Abstractions;
using AttechServer.Infrastructures.ContentProcessing;
using AttechServer.Infrastructures.Persistances;
using AttechServer.Shared.Configurations;
using AttechServer.Shared.Middlewares;
using AttechServer.Shared.Services;
using AttechServer.Shared.WebAPIBase;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.FileProviders;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();


// Configure TinyMCE options
builder.Services.Configure<TinyMceOptions>(
    builder.Configuration.GetSection(TinyMceOptions.SectionName));
//Config connect to sql server
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default"));
});

//Config JWT setting
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(x =>
{
    x.RequireHttpsMetadata = false;
    x.SaveToken = true;
    x.TokenValidationParameters = new TokenValidationParameters()
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration.GetSection("JWT")["Key"]!)),
        ValidateAudience = false,
        ValidateIssuer = false,
        ClockSkew = TimeSpan.Zero
    };
});

// Add services to the container.

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    });

// setting cors
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAllOrigins",
        policy =>
        {
            policy.WithOrigins(
                    "http://localhost:3000",
                    "https://localhost:3000",
                    "http://192.168.22.159:3000",
                    "https://192.168.22.159:3000",
                    "http://192.168.22.159:7276",
                    "https://192.168.22.159:7276",
                    "http://192.168.22.159:5232",
                    "https://attech.space",
                    "https://www.attech.space"
                )
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials();
        });
});

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    //Setting Xml for writing description API
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    c.IncludeXmlComments(xmlPath);
    c.OperationFilter<AddCommonParameterSwagger>();
    //Config swagger for using Bearer Token
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Version = "v1",
        Title = "Attech Web API"
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        new string[] {}
                    }
                });
});
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
//builder.Services.AddOpenApi();

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IRoleService, RoleService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IApiEndpointService, ApiEndpointService>();

builder.Services.AddScoped<IWysiwygFileProcessor, WysiwygFileProcessor>();
builder.Services.AddScoped<INewsService, NewsService>();
builder.Services.AddScoped<INewsCategoryService, NewsCategoryService>();

builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<INotificationCategoryService, NotificationCategoryService>();

builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IProductCategoryService, ProductCategoryService>();

builder.Services.AddScoped<IServiceService, ServiceService>();
builder.Services.AddScoped<IContactService, ContactService>();
builder.Services.AddScoped<IContactEmailService, ContactEmailService>();
builder.Services.AddScoped<IContactNotificationService, ContactNotificationService>();
builder.Services.AddScoped<AttechServer.Infrastructures.Mail.IEmailService, AttechServer.Infrastructures.Mail.EmailService>();
builder.Services.AddScoped<IAttachmentService, AttachmentService>();
builder.Services.AddScoped<ICacheService, CacheService>();
builder.Services.AddScoped<ICacheInvalidationService, CacheInvalidationService>();
builder.Services.AddScoped<IActivityLogService, ActivityLogService>();
builder.Services.AddScoped<ISystemMonitoringService, SystemMonitoringService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddHttpClient<ITranslationService, FreeTranslationService>();
builder.Services.AddScoped<IUrlService, UrlService>();
builder.Services.AddScoped<ILanguageContentService, LanguageContentService>();
builder.Services.AddScoped<ILanguageContentCategoryService, LanguageContentCategoryService>();

// Add filters
builder.Services.AddScoped<AttechServer.Shared.Filters.AntiSpamFilter>();

// Add SignalR
builder.Services.AddSignalR();

// Add background services
builder.Services.AddHostedService<AttechServer.Services.TempFileCleanupBackgroundService>();
builder.Services.AddHostedService<AttechServer.Services.SystemMonitoringBackgroundService>();

// Configure response caching
builder.Services.AddResponseCaching();
builder.Services.Configure<MemoryCacheOptions>(options =>
{
    // options.SizeLimit = 100; // Đã bỏ giới hạn cache size
    options.CompactionPercentage = 0.25; // Remove 25% when size limit is reached
});

var app = builder.Build();
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Application started!");

// Initialize database
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        await DbInitializer.InitializeAsync(context);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while seeding the database.");
    }
}

// Add request timing tracking
app.UseRequestTiming();

// Add global exception handling
app.UseGlobalExceptionHandling();

app.UseCors("AllowAllOrigins");

// Configure uploads directory (same as attachment service)
var uploadsPath = Path.Combine(app.Environment.ContentRootPath, "Uploads");
if (!Directory.Exists(uploadsPath))
{
    Directory.CreateDirectory(uploadsPath);
    logger.LogInformation($"Created uploads directory: {uploadsPath}");
}

// Configure static files for wwwroot (commented out - not needed)
// app.UseStaticFiles();

// Configure static files for uploads (GUID filenames make it secure)
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(Path.Combine(app.Environment.ContentRootPath, "Uploads")),
    RequestPath = "/uploads",
    ServeUnknownFileTypes = true,
    DefaultContentType = "application/octet-stream",
    OnPrepareResponse = ctx =>
    {
        // Add CORS headers
        ctx.Context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
        ctx.Context.Response.Headers.Add("Cache-Control", "public, max-age=3600");
    }
});

// Add response caching middleware
app.UseResponseCaching();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Only use HTTPS redirection in production
if (app.Environment.IsProduction())
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();

app.UseMiddleware<RoleMiddleWare>();

app.MapControllers();

// Map SignalR hubs
app.MapHub<AttechServer.Shared.Hubs.ContactHub>("/hubs/contact");

//var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
//var host = Environment.GetEnvironmentVariable("HOST") ?? "localhost";
//app.Run($"http://{host}:{port}");
app.Run();
