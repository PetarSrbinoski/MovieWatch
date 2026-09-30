using Microsoft.AspNetCore.Builder;
using MovieWatch.Web.Extensions;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddMovieWatch(builder.Configuration);
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
