using NetArchTest.Rules;

namespace TwoNineteenVolts.Venue.Tests;

// The dependency rule of CODING_STANDARDS.md §3.4. The layers are folders in one project,
// so the compiler cannot enforce it; these tests do.
public class ArchitectureTests
{
    private const string Root = "TwoNineteenVolts.Venue";
    private const string Domain = Root + ".Domain";
    private const string Application = Root + ".Application";
    private const string Infrastructure = Root + ".Infrastructure";
    private const string Endpoints = Root + ".Endpoints";
    private const string Commands = Root + ".Commands";
    private const string Common = Root + ".Common";

    [Fact]
    public void Domain_uses_nothing_else_in_the_project()
    {
        AssertNoDependency(Domain, Application, Infrastructure, Endpoints, Commands, Common);
    }

    [Fact]
    public void Domain_knows_nothing_about_persistence()
    {
        AssertNoDependency(Domain, "Microsoft.EntityFrameworkCore");
    }

    [Fact]
    public void Application_uses_only_domain_and_common()
    {
        AssertNoDependency(Application, Infrastructure, Endpoints, Commands);
    }

    [Fact]
    public void Application_never_touches_the_http_request()
    {
        AssertNoDependency(Application, "Microsoft.AspNetCore");
    }

    private static void AssertNoDependency(string layer, params string[] forbidden)
    {
        var result = Types.InAssembly(typeof(Program).Assembly)
            .That().ResideInNamespace(layer)
            .ShouldNot().HaveDependencyOnAny(forbidden)
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            $"{layer} must not use {string.Join(", ", forbidden)}. Offending types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }
}
