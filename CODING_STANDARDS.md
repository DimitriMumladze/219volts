# Coding Standards

These rules apply to all code in this repository. If a request conflicts with a rule, ask before deviating.

## 1. Principles

### 1.1 Build exactly what is asked
- Implement only the requested behavior. No extra layers, fallbacks, options or abstractions "for later".
- Add an abstraction (interface, service, helper) only when a second real use exists.
- If something beyond the request seems necessary (a timeout, error handling, a config value), propose it first; don't add it silently.
- Make the smallest change that works. Don't touch unrelated code.

### 1.2 Readability over cleverness
- Write code a new developer understands on first read.
- Use clear names instead of explanatory comments. Comments explain *why*, not *what*.
- Prefer plain, explicit code over clever one-liners, deep nesting or heavy generics.
- A function does one thing and stays short.

### 1.3 No secrets in git
- Never commit keys, tokens, passwords or connection strings.
- Local development: .NET User Secrets. Deployed: environment variables.
- Committed config files hold only empty values or placeholders for secrets.
- If a secret is ever committed, rotate it immediately. Removing it from history is not enough.

## 2. C# Conventions

### 2.1 Naming
| Element | Style | Example |
|---|---|---|
| Class, record, enum, method, property | PascalCase | `ShellyClient`, `TurnOn()`, `IsOn` |
| Interface | `I` + PascalCase | `ILampService` |
| Parameter, local variable | camelCase | `deviceIp`, `lamp` |
| Private field | `_camelCase` | `_deviceIp` |

```csharp
public class Lamp
{
    private int _switchCount;

    public bool IsOn { get; private set; }

    public void Toggle()
    {
        var wasOn = IsOn;
        IsOn = !wasOn;
        _switchCount++;
    }
}
```

### 2.2 Braces
Opening brace goes on its own line (Allman style). Always use braces, even when the body is one line.

```csharp
public void TurnOn()
{
    if (_isOn)
    {
        return;
    }

    _isOn = true;
}
```

### 2.3 Namespaces
Use file-scoped namespaces.

```csharp
namespace TwoNineteenVolts.Venue.Infrastructure.Lamps;

public class ShellyClient
{
}
```

### 2.4 `var`
Always use `var` for local variables.

```csharp
var lamp = new Lamp();
var isOn = lamp.IsOn;
```

### 2.5 Async methods
Methods that return `Task` or `Task<T>` end with `Async`.

```csharp
public async Task<bool> GetStatusAsync()
{
    ...
}

var isOn = await GetStatusAsync();
```

### 2.6 Constructors
Use primary constructors for dependency injection. Use the parameter directly; don't copy it into a field.

```csharp
public class ShellyClient(HttpClient http)
{
    public async Task<string> GetStatusJsonAsync(string deviceIp)
    {
        return await http.GetStringAsync($"http://{deviceIp}/rpc/Switch.GetStatus?id=0");
    }
}
```

### 2.7 One type per file
Each class, record, interface or enum lives in its own file, named after the type.

```
Endpoints/Lamps/LampEndpoints.cs       -> class LampEndpoints
Endpoints/Lamps/LampStatus.cs          -> record LampStatus
Infrastructure/Lamps/ShellyClient.cs   -> class ShellyClient
```

## 3. Project Layout

### 3.1 Repository
Application code goes in `src/`, tests in `tests/`. The solution file sits at the root. Each app is one project named `TwoNineteenVolts.<App>`, and its tests are `TwoNineteenVolts.<App>.Tests`. Standalone design demos and mock-ups go in `prototypes/<name>/`; they are not part of the app and nothing in `src/` uses them.

```
219Wats/
  src/
    TwoNineteenVolts.Venue/        venue app: runs on the venue PC as a Windows Service
  tests/
    TwoNineteenVolts.Venue.Tests/
  prototypes/
    admin-demo/                    clickable admin demo (index.html)
  TwoNineteenVolts.slnx
  CODING_STANDARDS.md
  CLAUDE.md
```

### 3.2 Project and namespace names
Projects are named `TwoNineteenVolts.<App>`. Namespaces follow the folder path.

```csharp
// src/TwoNineteenVolts.Venue/Endpoints/Users/UserEndpoints.cs
namespace TwoNineteenVolts.Venue.Endpoints.Users;
```

### 3.3 One project per app until a split is needed
Each app is a single project. Split out another project (for example `TwoNineteenVolts.Domain`) only when there is a concrete reason, such as a second app that needs the same code. Write the reason in the pull request. Until then the layers in 3.4 are folders, not projects.

### 3.4 Layers first, features inside
Inside a project, the top folders are the Clean Architecture layers. Inside each layer, code is grouped by feature (`Auth/`, `Users/`, `Audit/`, `Setup/`, later `Devices/`). **A feature has the same name in every layer it appears in**, so everything about users is in a `Users/` folder, whichever layer you open.

```
TwoNineteenVolts.Venue/
  Domain/
    Users/           User.cs, Role.cs
    Audit/           AuditEntry.cs, AuditSource.cs, AuditActions.cs
  Application/
    Auth/            LoginService.cs, LoginThrottle.cs, IPinHasher.cs, PinFormat.cs, ISessionRegistry.cs, ...
    Users/           UserService.cs, UserError.cs
    Audit/           IAuditLog.cs, ICurrentActor.cs, AuditActor.cs, AuditReader.cs, ...
    Setup/           SetupService.cs, ResetSupportService.cs, SetupError.cs, SecretGenerator.cs
    Data/            IVenueDbContext.cs
  Infrastructure/
    Auth/            AuthModule.cs, AuthOptions.cs, AuthClaims.cs, Policies.cs, PinHasher.cs, InMemoryTicketStore.cs, ...
    Users/           UsersModule.cs, UserConfiguration.cs
    Audit/           AuditModule.cs, AuditEntryConfiguration.cs, AuditLog.cs, AuditCleanupService.cs
    Setup/           SetupModule.cs
    Data/            DataModule.cs, VenueDbContext.cs, UtcDateTimeConverter.cs, Migrations/
  Endpoints/
    Auth/            AuthEndpoints.cs, LoginRequest.cs, CurrentUserResponse.cs, ...
    Users/           UserEndpoints.cs, CreateUserRequest.cs, UserResponse.cs, ...
    Audit/           AuditEndpoints.cs, AuditEntryResponse.cs, AuditPageResponse.cs
  Commands/
    Setup/           SetupCommand.cs, ResetSupportCommand.cs, IConsole.cs, SystemConsole.cs
  Common/            Result.cs
  Program.cs
```

`Endpoints` and `Commands` are both entry points: the first serves HTTP, the second the command line (`TwoNineteenVolts.Venue.exe setup`). Both stay thin and leave the rules to `Application`.

**Dependency rule**, enforced by `tests/TwoNineteenVolts.Venue.Tests/ArchitectureTests.cs` (the layers are folders, so the compiler cannot enforce it):

| Layer | May use |
|---|---|
| `Domain` | nothing else in the project, and no EF Core |
| `Application` | `Domain`, `Common`; never ASP.NET Core |
| `Infrastructure` | everything |
| `Endpoints`, `Commands` | everything |

- Application code reaches the database only through `Application/Data/IVenueDbContext`, which `VenueDbContext` implements. It never uses `VenueDbContext` itself.
- Application code never touches `HttpContext`. What it needs from the request comes through an interface implemented in Infrastructure (example: `ICurrentActor`).

Code shared by several features goes in `Common/`, and only once a second feature actually uses it (exception: `Result<T>`, see 6.2). The same holds inside a layer: `IConsole` lives in `Commands/Setup/` until a second feature has a command.

### 3.5 What goes in each layer
| Layer | Holds | Examples |
|---|---|---|
| `Domain/<Feature>/` | Entities, facts, enums and constants of the business (see 3.6) | `User`, `AuditEntry`, `Role`, `AuditActions` |
| `Application/<Feature>/` | Services with the business rules, the interfaces they need, and their result and query types | `UserService`, `SetupService`, `LoginThrottle`, `IPinHasher`, `UserError`, `AuditQuery` |
| `Infrastructure/<Feature>/` | Everything technical: EF configurations, implementations of Application interfaces, options, background services, and the `<Feature>Module` (see 4.4) | `UserConfiguration`, `PinHasher`, `AuthOptions`, `SessionExpiryService`, `AuthModule` |
| `Endpoints/<Feature>/` | The `<Feature>Endpoints` class (see 4.1) and its request and response records (see 4.3) | `UserEndpoints`, `CreateUserRequest`, `UserResponse` |
| `Commands/<Feature>/` | Command-line commands: prompts, printing and exit codes, and the console they talk to | `SetupCommand`, `ResetSupportCommand`, `IConsole` |

Where does a new piece go? Ask what it needs:

- Only business values and rules about one object → `Domain`.
- A business rule that needs other data, the clock or another service → `Application`.
- A library, the database schema, the file system or the OS → `Infrastructure`.
- HTTP (routes, status codes, JSON shapes) → `Endpoints`.
- The command line (questions, answers, exit codes) → `Commands`.

**When a feature folder gets crowded,** first check whether it holds more than one feature. `Infrastructure/Auth/` once had 14 files covering sign-in, users, setup and the console; split into `Auth`, `Users` and `Setup`, it has 8. Only a folder that is still large after that gets subfolders by concern.

### 3.6 Domain classes
Every domain class is one of two kinds. Decide by asking: **does it change after it is created?**

- **Yes → entity** (it has a life: created, renamed, deactivated). Example: `User`.
- **No → fact** (it records that something happened and is never edited). Example: `AuditEntry`.

**Entities**
- A primary constructor takes everything the object needs to be valid at creation, and nothing else.
- Every property is `{ get; private set; }`, set from a constructor parameter or given a default.
- Changes happen only through methods named with business verbs (`Deactivate()`, not `SetIsActive(false)`). A one-line method uses `=>`.
- The class guards only rules it can check alone (for example: name trimmed, not empty). A guard throws `ArgumentException`, because reaching it means a caller has a bug.
- Rules that need anything else (other records, the database, the current user, time, audit, sessions) live in the Application service and return `Result<T>` (see 6.1).

```csharp
public class User(string displayName, Role role, string secretHash, DateTime createdAt)
{
    public int Id { get; private set; }
    public string DisplayName { get; private set; } = CleanName(displayName);
    public bool IsActive { get; private set; } = true;
    ...

    public void Rename(string displayName) => DisplayName = CleanName(displayName);
    public void Deactivate() => IsActive = false;
}
```

**Facts**
- No constructor and no methods.
- Every property is `required` and `{ get; init; }`, including nullable ones, so the creator decides every value.
- No validation inside; the service that creates the fact is responsible for its values.
- Never updated. A fact is only read, or deleted by a retention job.

```csharp
public class AuditEntry
{
    public long Id { get; private set; }
    public required DateTime OccurredAt { get; init; }
    public required string Action { get; init; }
    public required string? Target { get; init; }
    ...
}
```

**Both kinds**
- Always `class`, never `record`: EF Core tracks objects by identity, while records compare by value.
- No public setters.
- `Id { get; private set; }` is assigned by the database (`long` for tables that grow fast).
- No persistence knowledge: no EF attributes and no `using Microsoft.EntityFrameworkCore`. Mapping lives in `Infrastructure/<Feature>/<Type>Configuration.cs`.
- No dependencies: nothing injected, no `TimeProvider`, no async. The caller passes values such as the current time.
- Times are UTC `DateTime`, named `…At` (`CreatedAt`, `OccurredAt`).
- A fixed set of values is an enum, stored as a string (configured in Infrastructure).

## 4. Backend (ASP.NET)

### 4.1 Minimal APIs
Write endpoints as Minimal APIs, not controllers. Each feature has one `<Feature>Endpoints` class that maps its routes under a `MapGroup`. Handlers are private static methods named after what they do.

```csharp
// Endpoints/Lamps/LampEndpoints.cs
public static class LampEndpoints
{
    public static void MapLampEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/lamps");

        group.MapGet("/{id}/status", GetStatusAsync);
    }

    private static async Task<IResult> GetStatusAsync(string id, ShellyClient shelly)
    {
        var status = await shelly.GetStatusAsync(id);
        return TypedResults.Ok(status);
    }
}

// Program.cs
app.MapLampEndpoints();
```

### 4.2 Errors are ProblemDetails
Return errors with `TypedResults.Problem(...)` so every error has the standard ProblemDetails shape (RFC 9457). Register `AddProblemDetails()` and call `app.UseExceptionHandler()` so unhandled exceptions use the same shape.

```csharp
return TypedResults.Problem(
    title: "Lamp unreachable",
    detail: "No response from 192.168.0.103",
    statusCode: StatusCodes.Status503ServiceUnavailable);
```

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.6.4",
  "title": "Lamp unreachable",
  "status": 503,
  "detail": "No response from 192.168.0.103"
}
```

### 4.3 DTOs are records
Request and response models are positional records.

```csharp
public record LampStatus(string Id, bool IsOn, double PowerW);
```

### 4.4 Service registration per feature
Each feature registers its own services in a `<Feature>Module` class. `Program.cs` only calls these methods.

```csharp
// Infrastructure/Lamps/LampsModule.cs
public static class LampsModule
{
    public static IServiceCollection AddLamps(this IServiceCollection services)
    {
        services.AddHttpClient<ShellyClient>();
        return services;
    }
}

// Program.cs
builder.Services.AddLamps();
```

## 5. Configuration and Secrets

### 5.1 Options pattern
Bind each settings section to a typed `<Name>Options` class in `Infrastructure/<Feature>/`. The class holds its section name as a constant. Feature code never reads `IConfiguration` by string key.

```csharp
// Infrastructure/Lamps/ShellyOptions.cs
public class ShellyOptions
{
    public const string SectionName = "Shelly";

    [Required]
    public string LocalIp { get; set; } = "";

    public string CloudAuthKey { get; set; } = "";
}
```

### 5.2 Validate at startup
Mark required settings with data annotations and register options with `ValidateOnStart()`, so the app refuses to start when a setting is missing or invalid.

```csharp
// Infrastructure/Lamps/LampsModule.cs
services.AddOptions<ShellyOptions>()
    .BindConfiguration(ShellyOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();
```

### 5.3 Settings are read once
Inject `IOptions<T>`. Changing a setting requires restarting the app. Don't use `IOptionsMonitor<T>` or `IOptionsSnapshot<T>`.

```csharp
public class ShellyClient(HttpClient http, IOptions<ShellyOptions> options)
{
    public async Task<string> GetStatusJsonAsync()
    {
        var ip = options.Value.LocalIp;
        return await http.GetStringAsync($"http://{ip}/rpc/Switch.GetStatus?id=0");
    }
}
```

### 5.4 Config only for what differs per environment
Put a value in config only if it differs between environments or installations (IPs, URLs, keys). Everything else is a named constant next to the code that uses it.

```csharp
public class ShellyClient(...)
{
    private const int SwitchChannel = 0;
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(2);
}
```

```json
{
  "Shelly": {
    "LocalIp": "192.168.0.103",
    "CloudAuthKey": ""
  }
}
```

### 5.5 Setting secrets
Secrets stay empty in `appsettings.json` (see 1.3) and are supplied like this:

**Local development: User Secrets.** In Visual Studio, right-click the project and choose *Manage User Secrets*, or run:

```
dotnet user-secrets set "Shelly:CloudAuthKey" "<key>" --project src/TwoNineteenVolts.Venue
```

**Deployed: environment variables.** Replace `:` with a double underscore:

```
Shelly__CloudAuthKey=<key>
```

## 6. Errors and Logging

### 6.1 Expected failures return `Result<T>`
A failure that can happen in normal operation (a device doesn't answer, an item isn't found, input is invalid) is returned as a `Result<T>`, not thrown. The endpoint turns a failed result into a ProblemDetails response (see 4.2).

Exceptions are only for bugs and truly unexpected situations. Those reach the global ProblemDetails handler.

```csharp
// ShellyClient
public async Task<Result<LampStatus>> GetStatusAsync(string ip)
{
    try
    {
        var status = await http.GetFromJsonAsync<LampStatus>($"http://{ip}/rpc/Switch.GetStatus?id=0");
        return Result<LampStatus>.Ok(status!);
    }
    catch (HttpRequestException ex)
    {
        logger.LogWarning(ex, "Lamp unreachable at {Ip}", ip);
        return Result<LampStatus>.Fail("Lamp unreachable");
    }
}

// Endpoint
var result = await shelly.GetStatusAsync(ip);
if (result.IsFailure)
{
    return TypedResults.Problem(title: result.Error, statusCode: StatusCodes.Status503ServiceUnavailable);
}
return TypedResults.Ok(result.Value);
```

### 6.2 `Result<T>` is our own type
`Result<T>` lives in `Common/Result.cs`. It is written by us, not taken from a library. It is the one type that goes into `Common/` before a second feature uses it (exception to 3.4).

```csharp
// Common/Result.cs
public record Result<T>(T? Value, string? Error)
{
    public bool IsFailure => Error is not null;

    public static Result<T> Ok(T value) => new(value, null);
    public static Result<T> Fail(string error) => new(default, error);
}
```

### 6.3 Built-in `ILogger`, console only
Log through the built-in `ILogger<T>`, injected where it is needed. Logs go to the console. Don't add logging packages (Serilog, OpenTelemetry) until there is a concrete need, such as keeping or searching log history.

```csharp
public class ShellyClient(HttpClient http, ILogger<ShellyClient> logger)
```

### 6.4 Message templates
Write log messages as templates with named placeholders, so values are stored as separate fields. Never use string interpolation (`$"..."`) in log calls.

```csharp
// Do
logger.LogWarning("Lamp {LampId} unreachable at {Ip}", lampId, ip);

// Don't
logger.LogWarning($"Lamp {lampId} unreachable at {ip}");
```

### 6.5 What to log
Log failures and state changes only. Don't log every request.

| Level | When | Example |
|---|---|---|
| `Information` | A state change | Lamp switched on or off |
| `Warning` | An expected failure | Device didn't answer |
| `Error` | An unexpected failure | Unhandled exception |

```csharp
logger.LogInformation("Lamp {LampId} switched {State}", lampId, isOn ? "on" : "off");
```

## 7. Git Workflow

### 7.1 Changes reach `main` through pull requests only
`main` is protected. Nobody pushes to it directly. Every change, however small, goes through a branch and a pull request.

```
git switch -c feature/lamp-status
git commit -m "feat: add lamp status endpoint"
git push -u origin feature/lamp-status
```
Then open a pull request on GitHub.

### 7.2 Branch names
Use `type/short-description`, lowercase, with words separated by hyphens. The type is the same as the commit type (see 7.3), except that `feat` becomes `feature`.

```
feature/lamp-status
fix/shelly-timeout
docs/coding-standards
refactor/feature-folders
```

### 7.3 Commit messages
Use Conventional Commits: `<type>: <summary>`. The summary is lowercase and imperative ("add", not "added").

| Type | Use for |
|---|---|
| `feat` | New behavior |
| `fix` | Bug fix |
| `docs` | Documentation only |
| `refactor` | Code change that doesn't change behavior |
| `test` | Adding or changing tests |
| `chore` | Build, tooling, dependencies |

```
feat: add lamp status endpoint
fix: handle Shelly timeout
docs: add coding standards
```

### 7.4 Squash merge
Pull requests are merged with *Squash and merge*, so each PR becomes one commit on `main`. The PR title becomes that commit's message, so the PR title must follow 7.3.

### 7.5 Reviews are optional
The author may merge their own pull request. Ask for a review when a second opinion would help.

## 8. Testing

### 8.1 What must be tested
- **Business rules get rule tests.** Every rule in a service or domain class has a test that fails when the rule is broken. Example: `Last_manager_cannot_be_deactivated_or_demoted`.
- **Each endpoint group gets at least one success test and one permission test.** Example: `Manager_can_create_list_and_deactivate` and `Staff_gets_403_on_users`.

### 8.2 Where tests live and how they are named
Tests use xUnit, in `tests/TwoNineteenVolts.<App>.Tests/`, in folders named after the feature (`Auth/`, `Users/`, `Audit/`). A test name says the behavior in words separated by underscores; it reads as a sentence, not as a method name.

```csharp
[Fact]
public async Task Deactivated_users_next_request_gets_401()
```

### 8.3 Endpoint and database tests
Endpoint tests run the whole app in memory through `VenueAppFactory` (a `WebApplicationFactory<Program>`). Each factory instance gets its own temporary data folder and SQLite database, deleted afterwards, so tests never share data. Test data is inserted directly (`TestUsers.AddAsync`) so a test does not depend on another feature's endpoints.

```csharp
using var factory = new VenueAppFactory();
var nino = await TestUsers.AddAsync(factory, "Nino", Role.Manager, "1234");
using var client = factory.CreateClient();
await client.LoginAsync(nino, "1234");
```

### 8.4 Time and background work
- Code reads the time only through an injected `TimeProvider`. Tests control it with `FakeTimeProvider` (`factory.Time.Advance(...)`), so waits and expiries are tested instantly and give the same result every run.
- A background service keeps its work in a public `RunOnceAsync()`. Tests call that directly; `VenueAppFactory` leaves the app's own background loops out so they cannot race the test.

### 8.5 Architecture tests
`ArchitectureTests.cs` checks the dependency rule of 3.4 on the compiled app (with NetArchTest). A change that makes `Application` use `Infrastructure`, or `Domain` use EF Core, fails the build's tests and names the offending type. When a new layer folder is added, add it to these tests.

## 9. Working with AI Assistants

AI assistants follow every rule in this document, plus these.

### 9.1 Plan before coding
For anything beyond a small change, first list the files to create or change and what goes in each, then wait for approval.

```
Plan:
1. Add Endpoints/Lamps/LampEndpoints.cs with GET /api/lamps/{id}/status
2. Add Infrastructure/Lamps/ShellyClient.cs (local call only)
3. Add Infrastructure/Lamps/LampsModule.cs and call AddLamps() in Program.cs
OK to proceed?
```

### 9.2 No commits or pushes
The assistant changes files only. A person reviews, commits and pushes.

### 9.3 Explain new concepts
When the assistant uses a concept for the first time in this repository (a pattern, an API, a package), it adds two or three sentences on what it is and why it is used here.

```
New: IHttpClientFactory. ASP.NET creates and reuses HttpClient instances
for you, which avoids running out of network connections. Registered via
services.AddHttpClient<ShellyClient>().
```
