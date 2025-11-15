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
    // Enable HTTPS metadata validation in production
    x.RequireHttpsMetadata = builder.Environment.IsProduction();
    x.SaveToken = true;
    x.TokenValidationParameters = new TokenValidationParameters()
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration.GetSection("JWT")["Key"]!)),
        ValidateAudience = true,
        ValidAudience = builder.Configuration.GetSection("JWT")["Audience"] ?? "AttechServer",
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration.GetSection("JWT")["Issuer"] ?? "AttechServer",
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromMinutes(5), // Allow 5 minutes clock skew
        RequireExpirationTime = true
    };

    // Security events
    x.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogWarning("JWT Authentication failed: {Message}", context.Exception?.Message);
            return Task.CompletedTask;
        },
        OnTokenValidated = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("JWT Token validated for user: {UserId}",
                context.Principal?.FindFirst("user_id")?.Value);
            return Task.CompletedTask;
        },
        OnChallenge = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogWarning("JWT Challenge: {Error}, {ErrorDescription}",
                context.Error, context.ErrorDescription);
            return Task.CompletedTask;
        }
    };
});

// Add services to the container.

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    });

// Add response compression
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.MimeTypes = new[]
    {
        "application/json",
        "application/javascript",
        "text/plain",
        "text/html",
        "text/css",
        "image/svg+xml"
    };
});

// Configure CORS - CHỈ enable khi Development (local dev)
// Production không dùng vì Nginx đã xử lý CORS
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("Development", policy =>
        {
            policy.WithOrigins(
                    "http://localhost:3000",
                    "https://localhost:3000",
                    "http://192.168.22.159:3000",
                    "https://192.168.22.159:3000",
                    "http://192.168.22.159:7276",
                    "https://192.168.22.159:7276",
                    "http://192.168.22.159:5232"
                )
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials()
                .SetPreflightMaxAge(TimeSpan.FromMinutes(10));
        });

        options.DefaultPolicyName = "Development";
    });
}

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
// builder.Services.AddScoped<IApiEndpointService, ApiEndpointService>(); // DISABLED - không sử dụng nữa

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
// builder.Services.AddScoped<ISystemMonitoringService, SystemMonitoringService>(); // DISABLED - không sử dụng nữa
// builder.Services.AddScoped<IDashboardService, DashboardService>(); // DISABLED - không sử dụng nữa
builder.Services.AddHttpClient<ITranslationService, FreeTranslationService>();
builder.Services.AddScoped<IUrlService, UrlService>();
builder.Services.AddScoped<ILanguageContentService, LanguageContentService>();
builder.Services.AddScoped<ILanguageContentCategoryService, LanguageContentCategoryService>();
builder.Services.AddScoped<IMenuService, MenuService>();
builder.Services.AddScoped<IInternalDocumentService, InternalDocumentService>();
builder.Services.AddScoped<IPhoneBookService, PhoneBookService>();

// Add filters
builder.Services.AddScoped<AttechServer.Shared.Filters.AntiSpamFilter>();

// Add SignalR
builder.Services.AddSignalR();

// Add background services
builder.Services.AddHostedService<AttechServer.Services.TempFileCleanupBackgroundService>();
// builder.Services.AddHostedService<AttechServer.Services.SystemMonitoringBackgroundService>(); // DISABLED - không sử dụng nữa

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

// Add security middleware (order is important!)
app.UseSecurityHeaders();

// Add response compression (early in pipeline)
app.UseResponseCompression();

// Add request timing tracking
app.UseRequestTiming();

// Add global exception handling
app.UseGlobalExceptionHandling();

// Apply CORS policy - chỉ khi Development
if (app.Environment.IsDevelopment())
{
    app.UseCors();
}

// Add XSS protection (before authentication)
app.UseXssProtection();

// Configure uploads directory (same as attachment service)
var uploadsPath = Path.Combine(app.Environment.ContentRootPath, "Uploads");
if (!Directory.Exists(uploadsPath))
{
    Directory.CreateDirectory(uploadsPath);
    logger.LogInformation($"Created uploads directory: {uploadsPath}");
}

// Configure static files for wwwroot (commented out - not needed)
// app.UseStaticFiles();

// Configure static files for uploads
// Development: ASP.NET Core serves files directly
// Production: Nginx serves files (no need for this middleware)
if (app.Environment.IsDevelopment())
{
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
}


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsProduction())
{
    //app.UseHttpsRedirection();
}


app.UseAuthentication();
app.UseAuthorization();

// Add CSRF protection (after authentication)
// app.UseCsrfProtection(); // Disabled for development

app.UseMiddleware<RoleMiddleWare>();

app.MapControllers();

// Map SignalR hubs
app.MapHub<AttechServer.Shared.Hubs.ContactHub>("/hubs/contact");

//var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
//var host = Environment.GetEnvironmentVariable("HOST") ?? "localhost";
//app.Run($"http://{host}:{port}");
app.Run();
