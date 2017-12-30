#r "packages/build-deps/FAKE/tools/FakeLib.dll"
#r "packages/build-deps/FSharp.Configuration/lib/net45/FSharp.Configuration.dll"
#r "packages/build-deps/semver/lib/net452/Semver.dll"

open System
open Fake
open Fake.Core
open Fake.IO
open Fake.Core.TargetOperators
open FSharp.Configuration
open Semver;

let getVersion inputStr =
    let mutable value = null;
    if SemVersion.TryParse(inputStr, &value, true) then
        value
    else
        failwith "Value in version.yml must adhere to the SemanticVersion 2.0 Spec"

let runCommand execuatable command =
    let exitCode =
        Process.ExecProcess (fun info ->
        { info with
            FileName = execuatable
            Arguments = command
        }) (TimeSpan.FromSeconds(5.))

    if exitCode <> 0 then failwithf "Look at error for  command %s %s" execuatable command

let runMonoCommand command =
    runCommand "mono" command

let runPaketCommand paketCommand =
    sprintf ".paket/paket.exe %s" paketCommand |> runMonoCommand

type VersionConfig = YamlConfig<"version.yml">
let versionFile = VersionConfig()
let version  = getVersion versionFile.Version

let buildDir = "./build"


// *** Define Targets ***
Target.Create "Clean" (fun _ ->
    runCommand "dotnet" "clean -c \"Release\""
    Shell.CleanDir buildDir
)

Target.Create "Build" (fun _ ->
    DotNetCli.Build (fun p ->
        { p with
            Configuration = "Release"
        })
)

Target.Create "Test" (fun _ ->
    DotNetCli.Test (fun p ->
        { p with
            Project = "FluentAssertions.OneOf.Tests"
        })
)

Target.Create "Package" (fun _ ->
    Directory.ensure buildDir
    let finalVersion = version.ToString();
    sprintf "pack build --symbols --version %s --minimum-from-lock-file" finalVersion
        |> runPaketCommand
)

Target.Create "Publish" (fun _ ->
    sprintf "push build --url https://api.nuget.org --endpoint /v3/index.jsons"
        |> runPaketCommand
)

Target.Create "Test"

// *** Define Dependencies ***
"Clean"
    ==> "Build"
    ==> "Test" <=> "Package"
    ==> "Publish"

// *** Start Build ***
Target.RunOrDefault "Package"