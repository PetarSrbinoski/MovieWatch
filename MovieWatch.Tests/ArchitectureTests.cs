using System.Reflection;
using System.Xml.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MovieWatch.Domain.Models;
using MovieWatch.Repository;
using MovieWatch.Service.Implementation;
using MovieWatch.Web.Controllers;

namespace MovieWatch.Tests;

public class ArchitectureTests
{
    [Theory]
    [InlineData("Domain", new string[] { })]
    [InlineData("Repository", new[] { "Domain" })]
    [InlineData("Service", new[] { "Domain", "Repository" })]
    [InlineData("Web", new[] { "Domain", "Repository", "Service" })]
    public void Project_references_match_the_course_graph(string project, string[] dependencies)
    {
        var document = XDocument.Load(Path.Combine(SolutionRoot(), $"MovieWatch.{project}", $"MovieWatch.{project}.csproj"));
        var actual = document.Descendants("ProjectReference").Select(e =>
            Path.GetFileNameWithoutExtension(e.Attribute("Include")!.Value).Replace("MovieWatch.", "")).Order().ToArray();
        Assert.Equal(dependencies.Order(), actual);
    }

    [Fact]
    public void Domain_has_no_external_packages_or_project_dependencies()
    {
        var document = XDocument.Load(Path.Combine(SolutionRoot(), "MovieWatch.Domain", "MovieWatch.Domain.csproj"));
        Assert.Empty(document.Descendants("PackageReference"));
        Assert.Empty(document.Descendants("FrameworkReference"));
        Assert.All(typeof(Viewer).Assembly.GetReferencedAssemblies(), assembly => Assert.StartsWith("System.", assembly.Name));
    }

    [Fact]
    public void Controllers_do_not_depend_on_repositories_or_database_contexts()
    {
        var controllers = typeof(AuthController).Assembly.GetTypes().Where(type => typeof(ControllerBase).IsAssignableFrom(type));
        Assert.NotEmpty(controllers);
        foreach (var type in controllers)
            Assert.All(DependencyTypes(type), dependency =>
            {
                Assert.False(typeof(DbContext).IsAssignableFrom(dependency), $"{type.Name} depends on {dependency.Name}");
                Assert.NotEqual(typeof(ApplicationDbContext).Assembly, dependency.Assembly);
            });
    }

    [Fact]
    public void Services_do_not_depend_on_database_contexts()
    {
        var services = typeof(AccountService).Assembly.GetTypes();
        Assert.NotEmpty(services);
        foreach (var type in services)
            Assert.All(DependencyTypes(type), dependency =>
                Assert.False(typeof(DbContext).IsAssignableFrom(dependency), $"{type.Name} depends on {dependency.Name}"));
    }

    private static IEnumerable<Type> DependencyTypes(Type type)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        var members = type.GetConstructors(flags).SelectMany(c => c.GetParameters()).Select(p => p.ParameterType)
            .Concat(type.GetFields(flags).Select(f => f.FieldType))
            .Concat(type.GetProperties(flags).Select(p => p.PropertyType));
        return members.SelectMany(ExpandType);
    }

    private static IEnumerable<Type> ExpandType(Type type)
    {
        return new[] { type }.Concat(type.GetGenericArguments().SelectMany(ExpandType));
    }

    private static string SolutionRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MovieWatch.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Could not locate the MovieWatch solution.");
    }
}
