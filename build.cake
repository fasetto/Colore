#addin nuget:?package=Cake.DocFx&version=0.13.1
#addin nuget:?package=Cake.Coveralls&version=0.10.1
#addin nuget:?package=Cake.Codecov&version=0.8.0
#tool "nuget:?package=GitVersion.CommandLine&version=5.3.6"
#tool "nuget:?package=OpenCover&version=4.7.922"
#tool "nuget:?package=ReportGenerator&version=4.6.1"
#tool nuget:?package=coveralls.io&version=1.4.2
#tool nuget:?package=Codecov&version=1.11.1

///////////////////////////////////////////////////////////////////////////////
// ARGUMENTS
///////////////////////////////////////////////////////////////////////////////

var target = Argument("Target", "Default");
var tag = Argument("Tag", "cake");

var configuration = HasArgument("Configuration")
    ? Argument<string>("Configuration")
    : EnvironmentVariable("CONFIGURATION") != null
        ? EnvironmentVariable("CONFIGURATION")
        : "Release";

var buildNumber = HasArgument("BuildNumber")
    ? Argument<int>("BuildNumber")
    : AppVeyor.IsRunningOnAppVeyor
        ? AppVeyor.Environment.Build.Number
        : TravisCI.IsRunningOnTravisCI
            ? TravisCI.Environment.Build.BuildNumber
            : EnvironmentVariable("BUILD_NUMBER") != null
                ? int.Parse(EnvironmentVariable("BUILD_NUMBER"))
                : 0;

var coverallsRepoToken = HasArgument("CoverallsToken")
    ? Argument<string>("CoverallsToken")
    : EnvironmentVariable("COVERALLS_REPO_TOKEN");

///////////////////////////////////////////////////////////////////////////////
// GLOBALS
///////////////////////////////////////////////////////////////////////////////

var isAppVeyor = AppVeyor.IsRunningOnAppVeyor;
var isTravis = TravisCI.IsRunningOnTravisCI;
var isCi = isAppVeyor || isTravis;
var isWindows = IsRunningOnWindows();
var cover = isAppVeyor || HasArgument("Cover");

IEnumerable<string> ReadCoverageFilters(string path)
{
    return System.IO.File.ReadLines(path).Where(l => !string.IsNullOrWhiteSpace(l) && !l.StartsWith("#"));
}

if (isTravis)
{
    Information("OpenCover does not work on Travis CI, disabling coverage generation");
    cover = false;
}

///////////////////////////////////////////////////////////////////////////////
// SETUP / TEARDOWN
///////////////////////////////////////////////////////////////////////////////

var mainProject = "./src/Colore/Colore.csproj";
var testProject = "./src/Colore.Tests/Colore.Tests.csproj";
var frameworks = new List<string>();

GitVersion version = null;

Setup(ctx =>
{
    Information("PATH is {0}", EnvironmentVariable("PATH"));
    var docFxBranch = EnvironmentVariable("DOCFX_SOURCE_BRANCH_NAME");
    if (docFxBranch != null)
        Information("DocFx branch is {0}", docFxBranch);
    if (isTravis)
        Information("Travis branch is {0}", EnvironmentVariable("TRAVIS_BRANCH"));

    Information("Reading framework settings");

    var xmlValue = XmlPeek(mainProject, "/Project/PropertyGroup/TargetFrameworks");
    if (string.IsNullOrEmpty(xmlValue))
    {
        xmlValue = XmlPeek(mainProject, "/Project/PropertyGroup/TargetFramework");
    }

    if (isWindows)
        frameworks.AddRange(xmlValue.Split(';'));
    else
        frameworks.AddRange(xmlValue.Split(';').Where(v => !v.StartsWith("net4")));

    Information("Frameworks: {0}", string.Join(", ", frameworks));

    if (isCi)
    {
        GitVersion(new GitVersionSettings
        {
            RepositoryPath = ".",
            OutputType = GitVersionOutput.BuildServer
        });
    }

    version = GitVersion(new GitVersionSettings { RepositoryPath = "." });

    Information("Version: {0} on {1}", version.FullSemVer, version.CommitDate);
    Information("Commit hash: {0}", version.Sha);
});

Teardown(ctx =>
{
	// Executed AFTER the last task.
	Information("Finished running tasks.");
});

///////////////////////////////////////////////////////////////////////////////
// HELPERS
///////////////////////////////////////////////////////////////////////////////

void FixNUnitLoggerPaths()
{
    Information("Fixing NUnitXML.TestLogger paths");
    var files = GetFiles("/home/travis/.nuget/packages/nunitxml.testlogger/*/build/_common/*.dll");

    foreach (var file in files)
    {
        if (file.GetFilenameWithoutExtension().ToString().Contains("Nunit"))
        {
            var full = file.ToString();
            var fix = full.Replace("Nunit", "NUnit");
            Information("{0} --> {1}", full, fix);
            MoveFile(full, fix);
        }
    }
}

void Build(string project, string framework = null)
{
    var settings = new DotNetCoreBuildSettings
    {
        Configuration = configuration,
        NoRestore = true,
        ArgumentCustomization = args => args
            .Append($"/p:AssemblyVersion={version.AssemblySemVer}")
            .Append($"/p:NuGetVersion={version.NuGetVersionV2}")
    };

    if (!string.IsNullOrEmpty(framework))
        settings.Framework = framework;

    DotNetCoreBuild(project, settings);
}

///////////////////////////////////////////////////////////////////////////////
// TASKS
///////////////////////////////////////////////////////////////////////////////

Task("Clean")
    .Does(() =>
    {
        Information("Cleaning output directories");
        CleanDirectory("./artifacts");
        CleanDirectory("./publish");
        DotNetCoreClean("src/");
        CreateDirectory("./artifacts/nuget");
    });

Task("Restore")
    .IsDependentOn("Clean")
    .Does(() =>
    {
        DotNetCoreRestore("src/");
    });

Task("Build")
    .IsDependentOn("Restore")
    .Does(() =>
    {
        if (isTravis)
            FixNUnitLoggerPaths();

        if (isWindows)
        {
            Build(mainProject);
        }
        else
        {
            foreach (var framework in frameworks)
            {
                Build(mainProject, framework);
            }
        }

        Build(testProject);
    });

void Test(ICakeContext context = null)
{
    var settings = new DotNetCoreTestSettings
    {
        Configuration = configuration,
        NoBuild = true
    };

    if (AppVeyor.IsRunningOnAppVeyor)
    {
        settings.ArgumentCustomization = args => args
            .Append("--logger:AppVeyor");
    }
    else
    {
        settings.ArgumentCustomization = args => args
            .Append("--logger:nunit");
    }

    if (context == null)
        DotNetCoreTest(testProject, settings);
    else
        context.DotNetCoreTest(testProject, settings);

    if (AppVeyor.IsRunningOnAppVeyor)
    {
        return;
    }

    var testResults = GetFiles("src/Colore.Tests/TestResults/*.xml");
    CopyFiles(testResults, "./artifacts");
}

Task("Test")
    .IsDependentOn("Build")
    .Does(() =>
    {
        if (cover)
        {
            Information("Running tests with coverage, using OpenCover");

            var filters = ReadCoverageFilters("./src/coverage-filters.txt");

            var settings = filters.Aggregate(new OpenCoverSettings
            {
                OldStyle = true,
                MergeOutput = true
            }, (a, e) => a.WithFilter(e));

            if (AppVeyor.IsRunningOnAppVeyor)
            {
                settings.Register = "Path32";
            }

            OpenCover(c => Test(c), new FilePath("./artifacts/opencover-results.xml"), settings);

            ReportGenerator("./artifacts/opencover-results.xml", "./artifacts/coverage-report");
        }
        else
        {
            Information("Running tests without coverage");
            Test();
        }
    });

Task("Dist")
    .IsDependentOn("Test")
    .Does(() =>
    {
        foreach (var framework in frameworks)
        {
            var dir = $"./src/Colore/bin/{configuration}/{framework}/";
            var target = $"./artifacts/colore_{version.SemVer}_{framework}_anycpu.zip";
            Information("Zipping {0} to {1}", dir, target);
            Zip(dir, target, $"{dir}**/*.*");
        }
    });

Task("Publish")
    .IsDependentOn("Test")
    .Does(() =>
    {
        foreach (var framework in frameworks)
        {
            var settings = new DotNetCorePublishSettings
            {
                Framework = framework,
                Configuration = configuration,
                OutputDirectory = $"./publish/{framework}/",
                ArgumentCustomization = args => args
                    .Append($"/p:AssemblyVersion={version.AssemblySemVer}")
                    .Append($"/p:NuGetVersion={version.NuGetVersionV2}")
            };

            DotNetCorePublish("src/Colore", settings);

            var dir = $"./publish/{framework}/";
            var target = $"./artifacts/colore_{version.SemVer}_{framework}_full.zip";
            Information("Zipping {0} to {1}", dir, target);
            Zip(dir, target, $"{dir}**/*.*");
        }
    });

Task("Pack")
    .IsDependentOn("Test")
    .Does(() =>
    {
        MoveFiles(GetFiles("./src/**/*.nupkg"), "./artifacts/nuget");
        MoveFiles(GetFiles("./src/**/*.snupkg"), "./artifacts/nuget");
    });

Task("Docs")
    .IsDependentOn("Build")
    .Does(() =>
    {
        DocFxMetadata("./docs/docfx.json");
        if (isTravis)
        {
            DocFxBuild("./docs/docfx.json", new DocFxBuildSettings
            {
                ToolPath = "./docfx/docfx.exe"
            });
        }
        else
        {
            DocFxBuild("./docs/docfx.json");
        }
        Zip("./docs/_site", $"./artifacts/colore_{version.SemVer}_docs.zip");
    });

Task("Coveralls")
    .WithCriteria(cover)
    .WithCriteria(isAppVeyor)
    .WithCriteria(coverallsRepoToken != null)
    .IsDependentOn("Test")
    .Does(() =>
    {
        Information("Running Coveralls tool on OpenCover result");

        CoverallsIo("./artifacts/opencover-results.xml", new CoverallsIoSettings
        {
            FullSources = true,
            RepoToken = coverallsRepoToken
        });
    });

Task("Codecov")
    .WithCriteria(cover)
    .WithCriteria(isAppVeyor)
    .IsDependentOn("Test")
    .Does(() =>
    {
        var ccVersion = $"{version.FullSemVer}.build.{BuildSystem.AppVeyor.Environment.Build.Number}";
        var codecovPath = Context.Tools.Resolve("codecov.exe");

        Information("Running Codecov tool with version {0} on OpenCover result", ccVersion);

        Codecov(new CodecovSettings
        {
            Files = new[] { "./artifacts/opencover-results.xml" },
            Required = true,
            ////Branch = version.BranchName,
            EnvironmentVariables = new Dictionary<string, string>
            {
                ["APPVEYOR_BUILD_VERSION"] = ccVersion
            },
            Verbose = true,
            ToolPath = codecovPath
        });
    });

Task("CI")
    .IsDependentOn("Dist")
    .IsDependentOn("Publish")
    .IsDependentOn("Pack")
    .IsDependentOn("Docs")
    .IsDependentOn("Coveralls")
    .IsDependentOn("Codecov");

Task("Travis").IsDependentOn("Test");
Task("AppVeyor").IsDependentOn("CI");
Task("GitHub").IsDependentOn("CI");
Task("Default").IsDependentOn("Test");

RunTarget(target);
