using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
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
using MovieWatch.Web.Extensions;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<AuditSaveChangesInterceptor>();
builder.Services.AddDbContext<ApplicationDbContext>((provider, options) =>
{
    var configuration = provider.GetRequiredService<IConfiguration>();
    var connection = configuration.GetConnectionString("MovieWatch") ?? "Data Source=moviewatch.db";
    options.UseSqlite(connection).AddInterceptors(provider.GetRequiredService<AuditSaveChangesInterceptor>());
});
builder.Services.AddIdentityCore<IdentityUser>(options =>
{
    options.User.RequireUniqueEmail = true;
    options.Password.RequiredLength = 8;
}).AddRoles<IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>();
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<IIdentityRepository, IdentityRepository>();
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<ICatalogueService, CatalogueService>();
builder.Services.AddScoped<IPersonalDataService, PersonalDataService>();
builder.Services.AddScoped<IGroupService, GroupService>();
builder.Services.AddScoped<IRecommendationService, RecommendationService>();
builder.Services.AddScoped<IWorkbookExportService, WorkbookExportService>();
builder.Services.AddScoped<IImportJobRepository, ImportJobRepository>();
builder.Services.AddScoped<IImportJobService, ImportJobService>();
builder.Services.AddScoped<ICatalogueImportRepository, CatalogueImportRepository>();
builder.Services.AddScoped<IImportProcessor, ImportProcessor>();
builder.Services.Configure<TmdbSettings>(builder.Configuration.GetSection("Tmdb"));
builder.Services.AddHttpClient<ITmdbClient, TmdbClient>(client =>
{
    client.BaseAddress = new Uri("https://api.themoviedb.org/3/");
    client.Timeout = TimeSpan.FromSeconds(30);
});
if (builder.Configuration.GetValue("Import:WorkerEnabled", true))
    builder.Services.AddHostedService<ImportBackgroundService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<AccountMapper>();
builder.Services.AddScoped<CatalogueMapper>();
builder.Services.AddScoped<PersonalMapper>();
builder.Services.AddScoped<GroupMapper>();
builder.Services.AddScoped<RecommendationMapper>();
builder.Services.AddScoped<ImportJobMapper>();
builder.Services.AddOptions<JwtSettings>().Bind(builder.Configuration.GetSection("Jwt"))
    .Validate(jwt => !string.IsNullOrWhiteSpace(jwt.SigningKey) && Encoding.UTF8.GetByteCount(jwt.SigningKey) >= 32,
        "Configure Jwt:SigningKey with at least 32 bytes using user secrets or environment variables.")
    .Validate(jwt => !string.IsNullOrWhiteSpace(jwt.Issuer) && !string.IsNullOrWhiteSpace(jwt.Audience),
        "Jwt:Issuer and Jwt:Audience are required.")
    .Validate(jwt => jwt.LifetimeMinutes is >= 1 and <= 60, "JWT lifetime must be 1–60 minutes.");
builder.Services.Configure<AdministratorSettings>(builder.Configuration.GetSection("Administrator"));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
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
builder.Services.AddAuthorization();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddSwaggerGen(options =>
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
var app = builder.Build();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
await app.InitializeDatabaseAsync();
app.Run();

public partial class Program;
