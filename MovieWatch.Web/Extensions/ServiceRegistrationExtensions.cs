using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using MovieWatch.Domain.Common;
using MovieWatch.Domain.Config;
using MovieWatch.Service.Jobs;
using MovieWatch.Repository;
using MovieWatch.Repository.Implementation;
using MovieWatch.Repository.Interface;
using MovieWatch.Service.Implementation;
using MovieWatch.Service.Interface;
using MovieWatch.Web.Errors;
using MovieWatch.Web.Mapper;
using MovieWatch.Web.Interceptor;

namespace MovieWatch.Web.Extensions;

public static class ServiceRegistrationExtensions
{
    public static IServiceCollection AddMovieWatch(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<AuditSaveChangesInterceptor>();
        services.AddDbContext<ApplicationDbContext>((provider, options) => options.UseSqlite(
            configuration.GetConnectionString("MovieWatch") ?? "Data Source=moviewatch.db")
            .AddInterceptors(provider.GetRequiredService<AuditSaveChangesInterceptor>()));
        services.AddIdentityCore<IdentityUser>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = 8;
        }).AddRoles<IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>();
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IIdentityRepository, IdentityRepository>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<ICatalogueService, CatalogueService>();
        services.AddScoped<IPersonalDataService, PersonalDataService>();
        services.AddScoped<IGroupService, GroupService>();
        services.AddScoped<IRecommendationService, RecommendationService>();
        services.AddScoped<IWorkbookExportService, WorkbookExportService>();
        services.AddScoped<IImportJobRepository, ImportJobRepository>();
        services.AddScoped<IImportJobService, ImportJobService>();
        services.AddScoped<ICatalogueImportRepository, CatalogueImportRepository>();
        services.AddScoped<IImportProcessor, ImportProcessor>();
        services.Configure<TmdbSettings>(configuration.GetSection("Tmdb"));
        services.AddHttpClient<ITmdbClient, TmdbClient>(client =>
        {
            client.BaseAddress = new Uri("https://api.themoviedb.org/3/");
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        if (configuration.GetValue("Import:WorkerEnabled", true))
            services.AddHostedService<ImportBackgroundService>();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<AccountMapper>();
        services.AddScoped<CatalogueMapper>();
        services.AddScoped<PersonalMapper>();
        services.AddScoped<GroupMapper>();
        services.AddScoped<RecommendationMapper>();
        services.AddScoped<ImportJobMapper>();
        services.AddOptions<JwtSettings>().Bind(configuration.GetSection("Jwt"))
            .Validate(jwt => !string.IsNullOrWhiteSpace(jwt.SigningKey) && Encoding.UTF8.GetByteCount(jwt.SigningKey) >= 32,
                "Configure Jwt:SigningKey with at least 32 bytes using user secrets or environment variables.")
            .Validate(jwt => !string.IsNullOrWhiteSpace(jwt.Issuer) && !string.IsNullOrWhiteSpace(jwt.Audience),
                "Jwt:Issuer and Jwt:Audience are required.")
            .Validate(jwt => jwt.LifetimeMinutes is >= 1 and <= 60, "JWT lifetime must be 1–60 minutes.");
        services.Configure<AdministratorSettings>(configuration.GetSection("Administrator"));
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtSettings>>((options, settings) =>
            {
                var jwt = settings.Value;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true, ValidIssuer = jwt.Issuer,
                    ValidateAudience = true, ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                    ValidateLifetime = true, ClockSkew = TimeSpan.Zero
                };
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var id = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                        var accounts = context.HttpContext.RequestServices.GetRequiredService<IAccountService>();
                        try
                        {
                            await accounts.GetOwnProfileAsync(id ?? "", context.HttpContext.RequestAborted);
                        }
                        catch (OperationException)
                        {
                            context.Fail("The account is no longer available.");
                        }
                    }
                };
            });
        services.AddAuthorization();
        services.AddProblemDetails();
        services.AddExceptionHandler<ApiExceptionHandler>();
        services.AddControllers().AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo { Title = "MovieWatch API", Version = "v1" });
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT"
            });
            options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", document)] = []
            });
        });
        return services;
    }
}
