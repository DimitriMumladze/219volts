using NetArchTest.Rules;

namespace TwoNineteenVolts.Venue.Tests;

public class ArchitectureTests
{
    private const string Root = "TwoNineteenVolts.Venue";
    private const string Domain = Root + ".Domain";
    private const string Application = Root + ".Application";
    private const string Infrastructure = Root + ".Infrastructure";
    private const string Endpoints = Root + ".Endpoints";
    private const string Commands = Root + ".Commands";
    private const string Common = Root + ".Common";
    private const string Resources = Root + ".Resources";

    private static readonly string[] DeviceInternals =
    [
        Application + ".Devices.DeviceGateway",
        Application + ".Devices.DeviceLocks",
        Application + ".Devices.OutletControl",
        Application + ".Devices.Contracts.DeviceStatus",
        Application + ".Devices.DeviceStatuses",
        Application + ".Devices.RelayDrivers",
        Application + ".Devices.Contracts.IRelayDriver",
        Application + ".Devices.RelayCall"
    ];

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

    [Fact]
    public void Only_endpoints_and_commands_use_resources()
    {
        AssertNoDependency(Domain, Resources);
        AssertNoDependency(Application, Resources);
        AssertNoDependency(Infrastructure, Resources);
    }

    [Fact]
    public void Only_devices_use_device_internals()
    {
        var result = Types.InAssembly(typeof(Program).Assembly)
            .That().DoNotResideInNamespaceMatching(@"\.Devices(\.|$)")
            .ShouldNot().HaveDependencyOnAny(DeviceInternals)
            .GetResult();

        var offenders = string.Join(", ", result.FailingTypeNames ?? []);
        Assert.True(result.IsSuccessful, $"Only the Devices feature may use device internals. Offending types: {offenders}");
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
