namespace Mayordomo.Tooling

open System
open System.Diagnostics
open System.Globalization
open System.IO
open System.IO.Compression
open System.Net.Http
open System.Security
open System.Text
open System.Text.Json
open System.Text.RegularExpressions
open System.Xml.Linq

module Internal =
    let repoRoot () = Directory.GetCurrentDirectory()

    let fullPath relativePath =
        Path.GetFullPath(Path.Combine(repoRoot (), relativePath))

    let exists relativePath =
        File.Exists(fullPath relativePath) || Directory.Exists(fullPath relativePath)

    let readText relativePath =
        File.ReadAllText(fullPath relativePath)

    let printErrors (header: string) (errors: string list) =
        Console.Error.WriteLine(header)
        errors |> List.iter (fun error -> Console.Error.WriteLine($"- {error}"))

    let bytesContain (haystack: byte array) (needle: byte array) =
        if needle.Length = 0 then
            true
        elif haystack.Length < needle.Length then
            false
        else
            seq { 0 .. haystack.Length - needle.Length }
            |> Seq.exists (fun offset ->
                let mutable equal = true
                let mutable index = 0

                while equal && index < needle.Length do
                    if haystack[offset + index] <> needle[index] then
                        equal <- false

                    index <- index + 1

                equal)

    let jsonOptions =
        JsonSerializerOptions(
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase)

module ProcessRunner =
    let run workingDirectory executable (arguments: string list) =
        let startInfo = ProcessStartInfo()
        startInfo.FileName <- executable
        startInfo.WorkingDirectory <- workingDirectory
        startInfo.UseShellExecute <- false
        arguments |> List.iter (fun argument -> startInfo.ArgumentList.Add(argument))

        use childProcess = new Process()
        childProcess.StartInfo <- startInfo

        if not (childProcess.Start()) then
            invalidOp $"Unable to start process '{executable}'."

        childProcess.WaitForExit()
        childProcess.ExitCode

module BranchPolicy =
    let private allowed =
        Regex(
            "^(features|bugs|releases|hotfixes|tags)/[a-z0-9][a-z0-9._-]*$",
            RegexOptions.CultureInvariant)

    let isValid (branch: string) =
        not (String.IsNullOrWhiteSpace(branch)) && allowed.IsMatch(branch)

    let run (args: string array) =
        if args.Length <> 1 then
            Console.Error.WriteLine("Usage: Mayordomo.Tooling branch-name <branch>")
            2
        elif isValid args[0] then
            Console.WriteLine($"Working branch accepted: {args[0]}")
            0
        else
            Console.Error.WriteLine(
                $"Invalid working branch \"{args[0]}\". Expected features/*, bugs/*, releases/*, hotfixes/*, or tags/*.")
            1

module ReleaseVersion =
    type ReleaseInfo =
        {
            PackageVersion: string
            AssemblyVersion: string
            FileVersion: string
            InformationalVersion: string
        }

    type SelectedRelease =
        {
            PackageVersion: string
            AssemblyVersion: string
            FileVersion: string
            InformationalVersion: string
            LatestPublishedBuild: int
            SelectedBuild: int
            ArtifactName: string
            SourceSha: string
        }

    let private validateInteger name minimum maximum value =
        if value < minimum || value > maximum then
            Error($"{name} must be an integer between {minimum} and {maximum}.")
        else
            Ok value

    let private parseDate (value: string) =
        let mutable parsed = DateTime.MinValue

        if not (
            DateTime.TryParseExact(
                value,
                "yyyyMMdd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                &parsed)
        ) then
            Error "Release date must be a valid calendar date using YYYYMMDD."
        else
            let dayOfYear = parsed.DayOfYear
            let fileDate = (parsed.Year % 100) * 1000 + dayOfYear

            if fileDate > 65535 then
                Error "Encoded file-version date exceeds 65535."
            else
                Ok fileDate

    let compute major minor (date: string) build (sha: string) =
        match
            validateInteger "major" 0 65535 major,
            validateInteger "minor" 0 65535 minor,
            validateInteger "build" 1 65535 build
        with
        | Error error, _, _
        | _, Error error, _
        | _, _, Error error ->
            Error error
        | Ok _, Ok _, Ok _ ->
            let sourceSha =
                if isNull sha then
                    ""
                else
                    sha.Trim().ToLowerInvariant()

            if not (
                Regex.IsMatch(
                    sourceSha,
                    "^[0-9a-f]{6,40}$",
                    RegexOptions.CultureInvariant)
            ) then
                Error "A hexadecimal source SHA (6-40 characters) is required."
            else
                match parseDate date with
                | Error error -> Error error
                | Ok fileDate ->
                    let packageVersion = $"{major}.{minor}.{date}.{build}"

                    Ok
                        {
                            PackageVersion = packageVersion
                            AssemblyVersion = $"{major}.{minor}.0.0"
                            FileVersion = $"{major}.{minor}.{fileDate}.{build}"
                            InformationalVersion = $"{packageVersion}+{sourceSha}"
                        }

    let latestPublishedBuild releaseDate (versions: string seq) =
        let prefix = $"1.0.{releaseDate}."

        versions
        |> Seq.choose (fun version ->
            if version.StartsWith(prefix, StringComparison.Ordinal) then
                match Int32.TryParse(version.Substring(prefix.Length)) with
                | true, build when build > 0 -> Some build
                | _ -> None
            else
                None)
        |> Seq.fold max 0

    let selectBuild runNumber latestBuild =
        max runNumber (latestBuild + 1)

    let private fetchPublishedVersions () =
        let url =
            "https://api.nuget.org/v3-flatcontainer/mayordomo.core/index.json"

        try
            use client = new HttpClient()
            let body = client.GetStringAsync(url).GetAwaiter().GetResult()
            use document = JsonDocument.Parse(body)
            let mutable versionsElement = Unchecked.defaultof<JsonElement>

            if
                document.RootElement.TryGetProperty(
                    "versions",
                    &versionsElement
                )
                && versionsElement.ValueKind = JsonValueKind.Array
            then
                versionsElement.EnumerateArray()
                |> Seq.choose (fun item ->
                    if item.ValueKind = JsonValueKind.String then
                        item.GetString() |> Option.ofObj
                    else
                        None)
                |> Seq.toArray
            else
                Array.empty
        with _ ->
            Array.empty

    let run (args: string array) =
        if args.Length <> 5 then
            Console.Error.WriteLine(
                "Usage: Mayordomo.Tooling release-version <major> <minor> <YYYYMMDD> <workflow-run-number> <source-sha>")
            2
        else
            match
                Int32.TryParse(args[0]),
                Int32.TryParse(args[1]),
                Int32.TryParse(args[3])
            with
            | (true, major), (true, minor), (true, runNumber)
                when runNumber > 0 ->
                let versions = fetchPublishedVersions ()
                let latest = latestPublishedBuild args[2] versions
                let selected = selectBuild runNumber latest

                match compute major minor args[2] selected args[4] with
                | Error error ->
                    Console.Error.WriteLine(error)
                    1
                | Ok version ->
                    let result =
                        {
                            PackageVersion = version.PackageVersion
                            AssemblyVersion = version.AssemblyVersion
                            FileVersion = version.FileVersion
                            InformationalVersion = version.InformationalVersion
                            LatestPublishedBuild = latest
                            SelectedBuild = selected
                            ArtifactName =
                                $"Mayordomo.Core-{version.PackageVersion}"
                            SourceSha =
                                args[4].Trim().ToLowerInvariant()
                        }

                    Console.WriteLine(
                        JsonSerializer.Serialize(
                            result,
                            Internal.jsonOptions
                        )
                    )

                    0
            | _ ->
                Console.Error.WriteLine(
                    "major, minor, and workflow-run-number must be positive integers.")
                2

module BenchmarkBudget =
    type Budget =
        {
            Name: string
            MaxMicrosecondsPerOperation: double
            MaxBytesPerOperation: double
        }

    let defaultBudgets =
        [
            {
                Name = "canonical-state-hash"
                MaxMicrosecondsPerOperation = 250.0
                MaxBytesPerOperation = 64.0 * 1024.0
            }
            {
                Name = "invariant-validation"
                MaxMicrosecondsPerOperation = 250.0
                MaxBytesPerOperation = 64.0 * 1024.0
            }
            {
                Name = "event-replay"
                MaxMicrosecondsPerOperation = 10000.0
                MaxBytesPerOperation = 2.0 * 1024.0 * 1024.0
            }
        ]

    let evaluate (json: string) (budgets: Budget list) =
        try
            use document = JsonDocument.Parse(json)
            let root = document.RootElement
            let mutable schemaElement = Unchecked.defaultof<JsonElement>

            let schemaFound =
                root.TryGetProperty(
                    "schemaVersion",
                    &schemaElement
                )

            if
                not schemaFound
                || schemaElement.ValueKind <> JsonValueKind.Number
                || schemaElement.GetInt32() <> 1
            then
                let schemaText =
                    if schemaFound then
                        schemaElement.ToString()
                    else
                        ""

                [
                    $"Unsupported benchmark report schema version '{schemaText}'."
                ]
            else
                let mutable resultsElement = Unchecked.defaultof<JsonElement>

                if
                    not (
                        root.TryGetProperty(
                            "results",
                            &resultsElement
                        )
                    )
                    || resultsElement.ValueKind <> JsonValueKind.Array
                then
                    [ "Benchmark report results must be an array." ]
                else
                    let results =
                        resultsElement.EnumerateArray()
                        |> Seq.choose (fun item ->
                            let mutable nameElement =
                                Unchecked.defaultof<JsonElement>

                            if
                                item.TryGetProperty(
                                    "name",
                                    &nameElement
                                )
                                && nameElement.ValueKind =
                                    JsonValueKind.String
                            then
                                nameElement.GetString()
                                |> Option.ofObj
                                |> Option.map (fun name ->
                                    name,
                                    item.Clone())
                            else
                                None)
                        |> Map.ofSeq

                    [
                        for budget in budgets do
                            match Map.tryFind budget.Name results with
                            | None ->
                                yield
                                    $"Benchmark '{budget.Name}' is missing from the report."
                            | Some result ->
                                let mutable latencyElement =
                                    Unchecked.defaultof<JsonElement>

                                let mutable allocationElement =
                                    Unchecked.defaultof<JsonElement>

                                let latency =
                                    if
                                        result.TryGetProperty(
                                            "microsecondsPerOperation",
                                            &latencyElement
                                        )
                                        && latencyElement.ValueKind =
                                            JsonValueKind.Number
                                    then
                                        latencyElement.GetDouble()
                                    else
                                        Double.NaN

                                let allocation =
                                    if
                                        result.TryGetProperty(
                                            "bytesPerOperation",
                                            &allocationElement
                                        )
                                        && allocationElement.ValueKind =
                                            JsonValueKind.Number
                                    then
                                        allocationElement.GetDouble()
                                    else
                                        Double.NaN

                                if
                                    not (Double.IsFinite(latency))
                                    || latency < 0.0
                                then
                                    yield
                                        $"Benchmark '{budget.Name}' has invalid latency '{latency}'."
                                elif
                                    latency
                                    > budget.MaxMicrosecondsPerOperation
                                then
                                    yield
                                        $"Benchmark '{budget.Name}' latency {latency} us/op exceeds hard ceiling {budget.MaxMicrosecondsPerOperation} us/op."

                                if
                                    not (Double.IsFinite(allocation))
                                    || allocation < 0.0
                                then
                                    yield
                                        $"Benchmark '{budget.Name}' has invalid allocation '{allocation}'."
                                elif
                                    allocation
                                    > budget.MaxBytesPerOperation
                                then
                                    yield
                                        $"Benchmark '{budget.Name}' allocation {allocation} B/op exceeds hard ceiling {budget.MaxBytesPerOperation} B/op."
                    ]
        with error ->
            [ $"Unable to parse benchmark report: {error.Message}" ]

    let run (args: string array) =
        if args.Length <> 1 then
            Console.Error.WriteLine(
                "Usage: Mayordomo.Tooling benchmark-budget <benchmark-report.json>")
            2
        elif not (File.Exists(args[0])) then
            Console.Error.WriteLine(
                $"Unable to read benchmark report '{args[0]}'.")
            2
        else
            let violations =
                evaluate
                    (File.ReadAllText(args[0]))
                    defaultBudgets

            if violations.IsEmpty then
                Console.WriteLine(
                    "Benchmark hard ceilings satisfied.")
                0
            else
                Internal.printErrors
                    "Benchmark hard ceilings failed:"
                    violations

                1

module EngineBudget =
    let private designTargetBytes = 256L * 1024L
    let private hardCeilingBytes = 512L * 1024L

    let run (args: string array) =
        let root = Internal.repoRoot ()

        let dllPath =
            Path.Combine(
                root,
                "src",
                "Mayordomo.Core",
                "bin",
                "Release",
                "net10.0",
                "Mayordomo.Core.dll"
            )

        let projectPath =
            Path.Combine(
                root,
                "src",
                "Mayordomo.Core",
                "Mayordomo.Core.csproj"
            )

        if not (File.Exists(dllPath)) then
            Console.Error.WriteLine(
                $"ENGINE BUDGET CHECK FAILED: missing Release DLL at {Path.GetRelativePath(root, dllPath)}")
            1
        elif not (File.Exists(projectPath)) then
            Console.Error.WriteLine(
                "ENGINE BUDGET CHECK FAILED: core project file is missing.")
            1
        else
            let bytes = FileInfo(dllPath).Length
            let project = XDocument.Load(projectPath)

            let packageReferences =
                project.Descendants()
                |> Seq.filter (fun element ->
                    element.Name.LocalName =
                        "PackageReference")
                |> Seq.choose (fun element ->
                    element.Attribute(
                        XName.Get("Include"))
                    |> Option.ofObj
                    |> Option.map (fun attribute ->
                        attribute.Value))
                |> Seq.toList

            Console.WriteLine(
                $"Core assembly: {Math.Round(float bytes / 1024.0, 2)} KiB")
            Console.WriteLine(
                $"Design target: ≤ {designTargetBytes / 1024L} KiB")
            Console.WriteLine(
                $"Hard ceiling: ≤ {hardCeilingBytes / 1024L} KiB")
            Console.WriteLine(
                $"Core PackageReference count: {packageReferences.Length}")

            let mutable exitCode = 0

            if not packageReferences.IsEmpty then
                let packageReferenceText =
                    String.Join(", ", packageReferences)

                Console.Error.WriteLine(
                    $"ENGINE BUDGET CHECK FAILED: Mayordomo.Core must have zero mandatory third-party runtime PackageReferences. Found: {packageReferenceText}")
                exitCode <- 1

            if bytes > hardCeilingBytes then
                Console.Error.WriteLine(
                    "ENGINE BUDGET CHECK FAILED: Core DLL exceeds the hard footprint ceiling.")
                exitCode <- 1
            elif bytes > designTargetBytes then
                Console.Error.WriteLine(
                    "ENGINE BUDGET WARNING: Core DLL exceeds the design target but remains below the hard ceiling.")

            let jsonIndex =
                args
                |> Array.tryFindIndex ((=) "--json")

            match jsonIndex with
            | Some index when index + 1 >= args.Length ->
                Console.Error.WriteLine(
                    "ENGINE BUDGET CHECK FAILED: --json requires an output path.")
                2
            | Some index ->
                let output =
                    Path.GetFullPath(
                        args[index + 1],
                        root)

                Directory.CreateDirectory(
                    Path.GetDirectoryName(output))
                |> ignore

                let metrics =
                    {|
                        assembly = "Mayordomo.Core.dll"
                        bytes = bytes
                        kib =
                            Math.Round(
                                float bytes / 1024.0,
                                2)
                        designTargetBytes =
                            designTargetBytes
                        designTargetKib =
                            designTargetBytes / 1024L
                        hardCeilingBytes =
                            hardCeilingBytes
                        hardCeilingKib =
                            hardCeilingBytes / 1024L
                        mandatoryThirdPartyRuntimePackageDependencies =
                            packageReferences.Length
                        packageReferences =
                            packageReferences
                        withinDesignTarget =
                            bytes <= designTargetBytes
                        withinHardCeiling =
                            bytes <= hardCeilingBytes
                    |}

                File.WriteAllText(
                    output,
                    JsonSerializer.Serialize(
                        metrics,
                        Internal.jsonOptions)
                    + Environment.NewLine)

                Console.WriteLine(
                    $"Metrics written to {Path.GetRelativePath(root, output)}")

                if exitCode = 0 then
                    Console.WriteLine(
                        "ENGINE BUDGET CHECK PASSED")

                exitCode
            | None ->
                if exitCode = 0 then
                    Console.WriteLine(
                        "ENGINE BUDGET CHECK PASSED")

                exitCode

module PublicReadiness =
    let private required =
        [
            "LICENSE"
            "NOTICE.md"
            "README.md"
            "README.es-MX.md"
            "CONTRIBUTING.md"
            "CODE_OF_CONDUCT.md"
            "SECURITY.md"
            "SUPPORT.md"
            "GOVERNANCE.md"
            "DCO.md"
            "CHANGELOG.md"
            "CITATION.cff"
            "AGENTS.md"
            "docs/DEVELOPMENT.md"
            "docs/RELEASING.md"
            "docs/architecture/clean-room-board-game-engine.md"
            "docs/architecture/0001-public-engine-private-game-boundary.md"
            "docs/architecture/0002-performance-and-footprint-budgets.md"
            "docs/performance/README.md"
            "docs/roadmap/README.md"
            "docs/site/index.html"
            "docs/site/styles.css"
            "docs/site/site.js"
            "benchmarks/README.md"
            ".github/CODEOWNERS"
            ".github/pull_request_template.md"
            ".github/workflows/pages.yml"
        ]

    let private forbiddenPaths =
        [
            "src/Mayordomo.Game"
            "src/Mayordomo.Studio"
            "Mayordomo.Game"
            "Mayordomo.Studio"
            "studio"
            "apps/studio"
            "clients/web"
            "clients/unity"
            "clients/avalonia"
            "content/mayordomo"
            "assets/mayordomo"
        ]

    let evaluate () =
        let errors = ResizeArray<string>()

        for path in required do
            if not (Internal.exists(path)) then
                errors.Add(
                    $"Missing open-source/public-readiness file: {path}")

        for path in forbiddenPaths do
            if Internal.exists(path) then
                errors.Add(
                    $"Private product path must not exist in engine repository: {path}")

        if
            Internal.exists("LICENSE")
            && not (
                (Internal.readText("LICENSE")).StartsWith(
                    "MIT License",
                    StringComparison.Ordinal)
            )
        then
            errors.Add(
                "LICENSE is not the expected MIT license text.")

        if
            Internal.exists("Directory.Build.props")
            && not (
                (Internal.readText("Directory.Build.props")).Contains(
                    "<PackageLicenseExpression>MIT</PackageLicenseExpression>",
                    StringComparison.Ordinal)
            )
        then
            errors.Add(
                "Directory.Build.props must declare MIT package licensing.")

        if Internal.exists("README.md") then
            let readme =
                Internal.readText("README.md")

            if
                not (
                    readme.Contains(
                        "open-source-from-day-one",
                        StringComparison.Ordinal)
                )
            then
                errors.Add(
                    "README must declare the engine open source from day one.")

            if
                not (
                    readme.Contains(
                        "ChicoDotNet/Mayordomo.Game",
                        StringComparison.Ordinal)
                )
            then
                errors.Add(
                    "README must document the private product repository boundary.")

            if
                not (
                    readme.Contains(
                        "Mayordomo Engine → Mayordomo.Game",
                        StringComparison.Ordinal)
                    || readme.Contains(
                        "Mayordomo Engine -> Mayordomo.Game",
                        StringComparison.Ordinal)
                )
            then
                errors.Add(
                    "README must explicitly document the forbidden reverse dependency.")

            if
                not (
                    readme.Contains(
                        "≤ 256 KiB",
                        StringComparison.Ordinal)
                    && readme.Contains(
                        "≤ 512 KiB",
                        StringComparison.Ordinal)
                )
            then
                errors.Add(
                    "README must publish the current Core footprint budgets.")

        if
            Internal.exists("NOTICE.md")
            && not (
                (Internal.readText("NOTICE.md")).Contains(
                    "public-safe",
                    StringComparison.Ordinal)
            )
        then
            errors.Add(
                "NOTICE.md must document the public-safe history rule.")

        if
            Internal.exists(".gitignore")
            && not (
                (Internal.readText(".gitignore")).Contains(
                    ".env",
                    StringComparison.Ordinal)
            )
        then
            errors.Add(
                ".gitignore must ignore environment-secret files.")

        let srcRoot = Internal.fullPath("src")

        if Directory.Exists(srcRoot) then
            for sourcePath in Directory.EnumerateFiles(
                srcRoot,
                "*",
                SearchOption.AllDirectories) do
                let extension =
                    Path.GetExtension(sourcePath).ToLowerInvariant()

                if
                    [ ".cs"; ".csproj"; ".props"; ".targets" ]
                    |> List.contains extension
                then
                    let source =
                        File.ReadAllText(sourcePath)

                    let relative =
                        Path.GetRelativePath(
                            Internal.repoRoot(),
                            sourcePath)

                    if
                        Regex.IsMatch(
                            source,
                            "Mayordomo\\.Game|Mayordomo\\.Studio",
                            RegexOptions.IgnoreCase)
                    then
                        errors.Add(
                            $"Engine source references private product identity: {relative}")

                    if
                        Regex.IsMatch(
                            source,
                            "\\b(Alfol[ií]|Devorador|Diezmo|Tithe|Tentaci[oó]n|Ministerio|Corona de)\\b",
                            RegexOptions.IgnoreCase)
                    then
                        errors.Add(
                            $"Engine source contains Mayordomo-specific game vocabulary: {relative}")

        List.ofSeq errors

    let run (_: string array) =
        let errors = evaluate()

        if errors.IsEmpty then
            Console.WriteLine(
                "OPEN-SOURCE / PUBLIC-READINESS STRUCTURAL GATE PASSED")
            Console.WriteLine(
                "This gate supplements, but does not replace, human history/provenance/security review.")
            0
        else
            Internal.printErrors
                "PUBLIC-READINESS CHECK FAILED"
                errors

            1

module PackageAudit =
    let private entryNames (archive: ZipArchive) =
        archive.Entries
        |> Seq.map (fun entry -> entry.FullName)
        |> Seq.toList

    let private findEntry (archive: ZipArchive) name =
        archive.Entries
        |> Seq.tryFind (fun entry ->
            String.Equals(
                entry.FullName,
                name,
                StringComparison.Ordinal))

    let private readTextEntry (archive: ZipArchive) name =
        match findEntry archive name with
        | None ->
            Error($"Archive entry is missing: {name}")
        | Some entry ->
            use stream = entry.Open()
            use reader =
                new StreamReader(
                    stream,
                    Encoding.UTF8)

            Ok(reader.ReadToEnd())

    let private readBytesEntry (archive: ZipArchive) name =
        match findEntry archive name with
        | None ->
            Error($"Archive entry is missing: {name}")
        | Some entry ->
            use stream = entry.Open()
            use memory = new MemoryStream()
            stream.CopyTo(memory)
            Ok(memory.ToArray())

    let private xmlElementValue
        (document: XDocument)
        name
        =
        document.Descendants()
        |> Seq.tryFind (fun element ->
            element.Name.LocalName = name)
        |> Option.map (fun element ->
            element.Value.Trim())

    let evaluate
        packageDirectory
        version
        (sourceShaArg: string)
        =
        let errors = ResizeArray<string>()

        let sourceSha =
            sourceShaArg.Trim().ToLowerInvariant()

        if
            not (
                Regex.IsMatch(
                    sourceSha,
                    "^[0-9a-f]{40}$",
                    RegexOptions.CultureInvariant)
            )
        then
            errors.Add(
                "Source SHA must be a full 40-character hexadecimal commit SHA.")

        let nupkg =
            Path.Combine(
                Path.GetFullPath(packageDirectory),
                $"Mayordomo.Core.{version}.nupkg")

        let snupkg =
            Path.Combine(
                Path.GetFullPath(packageDirectory),
                $"Mayordomo.Core.{version}.snupkg")

        for artifact in [ nupkg; snupkg ] do
            if not (File.Exists(artifact)) then
                errors.Add(
                    $"Expected package artifact does not exist: {artifact}")

        if errors.Count = 0 then
            use package = ZipFile.OpenRead(nupkg)
            use symbols = ZipFile.OpenRead(snupkg)

            let packageEntries =
                entryNames package

            let symbolEntries =
                entryNames symbols

            let requireEntry
                entries
                expected
                message
                =
                if not (List.contains expected entries) then
                    let entriesText =
                        String.Join(", ", entries)

                    errors.Add(
                        $"{message} Archive entries: {entriesText}")

            requireEntry
                packageEntries
                "Mayordomo.Core.nuspec"
                "Primary package must include Mayordomo.Core.nuspec."

            requireEntry
                packageEntries
                "lib/net10.0/Mayordomo.Core.dll"
                "Primary package must include the net10.0 engine assembly."

            requireEntry
                packageEntries
                "README.md"
                "Primary package must include README.md."

            if
                packageEntries
                |> List.exists (fun entry ->
                    entry.EndsWith(
                        ".pdb",
                        StringComparison.OrdinalIgnoreCase))
            then
                errors.Add(
                    "Primary .nupkg must not contain PDB files; symbols belong in .snupkg.")

            if
                packageEntries
                |> List.exists (fun entry ->
                    Regex.IsMatch(
                        entry,
                        "mayordomo\\.game|mayordomo\\.studio",
                        RegexOptions.IgnoreCase))
            then
                errors.Add(
                    "Primary package contains private product identity in an archive path.")

            match
                readTextEntry
                    package
                    "Mayordomo.Core.nuspec"
            with
            | Error error ->
                errors.Add(error)
            | Ok nuspec ->
                try
                    let document =
                        XDocument.Parse(nuspec)

                    let requireValue name expected =
                        match
                            xmlElementValue
                                document
                                name
                        with
                        | Some actual
                            when String.Equals(
                                actual,
                                expected,
                                StringComparison.Ordinal) ->
                            ()
                        | _ ->
                            errors.Add(
                                $"Expected <{name}> value '{expected}' in package nuspec.")

                    requireValue
                        "id"
                        "Mayordomo.Core"

                    requireValue
                        "version"
                        version

                    requireValue
                        "license"
                        "MIT"

                    requireValue
                        "readme"
                        "README.md"

                    match
                        xmlElementValue
                            document
                            "description"
                    with
                    | Some value
                        when not (
                            String.IsNullOrWhiteSpace(value)
                        ) ->
                        ()
                    | _ ->
                        errors.Add(
                            "Package description must be non-empty.")

                    requireValue
                        "projectUrl"
                        "https://github.com/ChicoDotNet/Mayordomo"

                    match
                        document.Descendants()
                        |> Seq.tryFind (fun element ->
                            element.Name.LocalName =
                                "repository")
                    with
                    | None ->
                        errors.Add(
                            "Package nuspec must contain repository metadata.")
                    | Some repository ->
                        let requireAttribute
                            name
                            expected
                            label
                            ignoreCase
                            =
                            match
                                repository.Attribute(
                                    XName.Get(name))
                                |> Option.ofObj
                            with
                            | None ->
                                errors.Add(
                                    $"{label} attribute '{name}' is missing.")
                            | Some attribute ->
                                let comparison =
                                    if ignoreCase then
                                        StringComparison.OrdinalIgnoreCase
                                    else
                                        StringComparison.Ordinal

                                if
                                    not (
                                        String.Equals(
                                            attribute.Value,
                                            expected,
                                            comparison)
                                    )
                                then
                                    errors.Add(
                                        $"{label} mismatch. Expected '{expected}', got '{attribute.Value}'.")

                        requireAttribute
                            "type"
                            "git"
                            "Repository type"
                            false

                        requireAttribute
                            "url"
                            "https://github.com/ChicoDotNet/Mayordomo"
                            "Repository URL"
                            false

                        requireAttribute
                            "commit"
                            sourceSha
                            "Repository commit"
                            true

                    if
                        document.Descendants()
                        |> Seq.exists (fun element ->
                            element.Name.LocalName =
                                "dependency")
                    then
                        errors.Add(
                            "Mayordomo.Core 1.0 must not contain runtime NuGet dependencies.")

                    if
                        Regex.IsMatch(
                            nuspec,
                            "Mayordomo\\.Game|Mayordomo\\.Studio",
                            RegexOptions.IgnoreCase)
                    then
                        errors.Add(
                            "Package nuspec must not reference private product identities.")
                with error ->
                    errors.Add(
                        $"Unable to parse Mayordomo.Core.nuspec: {error.Message}")

            requireEntry
                symbolEntries
                "Mayordomo.Core.nuspec"
                "Symbol package must include its nuspec."

            requireEntry
                symbolEntries
                "lib/net10.0/Mayordomo.Core.pdb"
                "Symbol package must include the portable net10.0 PDB."

            if
                symbolEntries
                |> List.exists (fun entry ->
                    entry.EndsWith(
                        ".dll",
                        StringComparison.OrdinalIgnoreCase))
            then
                errors.Add(
                    "Symbol package must not contain engine DLLs.")

            match
                readBytesEntry
                    symbols
                    "lib/net10.0/Mayordomo.Core.pdb"
            with
            | Error error ->
                errors.Add(error)
            | Ok pdb ->
                if
                    pdb.Length < 4
                    || Encoding.ASCII.GetString(
                        pdb,
                        0,
                        4) <> "BSJB"
                then
                    errors.Add(
                        "Mayordomo.Core.pdb is not a Portable PDB.")

                let repositoryBytes =
                    Encoding.UTF8.GetBytes(
                        "raw.githubusercontent.com/ChicoDotNet/Mayordomo")

                let shaBytes =
                    Encoding.UTF8.GetBytes(
                        sourceSha)

                if
                    not (
                        Internal.bytesContain
                            pdb
                            repositoryBytes
                    )
                then
                    errors.Add(
                        "Portable PDB does not contain Source Link metadata for ChicoDotNet/Mayordomo.")

                if
                    not (
                        Internal.bytesContain
                            pdb
                            shaBytes
                    )
                then
                    errors.Add(
                        "Portable PDB Source Link metadata does not contain the certified source SHA.")

        List.ofSeq errors

    let run (args: string array) =
        if args.Length <> 3 then
            Console.Error.WriteLine(
                "Usage: Mayordomo.Tooling package-audit <package-directory> <version> <source-sha>")
            2
        else
            let errors =
                evaluate
                    args[0]
                    args[1]
                    args[2]

            if errors.IsEmpty then
                Console.WriteLine(
                    "NUGET PACKAGE AUDIT PASSED")
                Console.WriteLine(
                    $"- package: Mayordomo.Core {args[1]}")
                Console.WriteLine(
                    $"- source: {args[2].ToLowerInvariant()}")
                Console.WriteLine(
                    "- runtime NuGet dependencies: 0")
                Console.WriteLine(
                    "- primary symbols: excluded")
                Console.WriteLine(
                    "- symbol package: Portable PDB present")
                Console.WriteLine(
                    "- Source Link: repository + exact source SHA present")
                0
            else
                Internal.printErrors
                    "NUGET PACKAGE AUDIT FAILED"
                    errors

                1

module ConsumerSmoke =
    let private xmlEscape (value: string) =
        SecurityElement.Escape(value)
        |> Option.ofObj
        |> Option.defaultValue value

    let run (args: string array) =
        if args.Length <> 2 then
            Console.Error.WriteLine(
                "Usage: Mayordomo.Tooling consumer-smoke <package-directory> <version>")
            2
        else
            let packageDirectory =
                Path.GetFullPath(args[0])

            let version = args[1]

            let packagePath =
                Path.Combine(
                    packageDirectory,
                    $"Mayordomo.Core.{version}.nupkg")

            if
                not (
                    Directory.Exists(
                        packageDirectory)
                )
            then
                Console.Error.WriteLine(
                    $"Package directory does not exist: {packageDirectory}")
                2
            elif not (File.Exists(packagePath)) then
                Console.Error.WriteLine(
                    $"Consumer smoke package does not exist: {packagePath}")
                1
            else
                let temporaryRoot =
                    Path.Combine(
                        Path.GetTempPath(),
                        "mayordomo-core-consumer-"
                        + Guid.NewGuid().ToString("N"))

                Directory.CreateDirectory(
                    temporaryRoot)
                |> ignore

                try
                    let nugetConfig =
                        $"""<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="{xmlEscape packageDirectory}" />
  </packageSources>
</configuration>
"""

                    File.WriteAllText(
                        Path.Combine(
                            temporaryRoot,
                            "NuGet.Config"),
                        nugetConfig)

                    let project =
                        $"""<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Mayordomo.Core" Version="{version}" />
  </ItemGroup>
</Project>
"""

                    File.WriteAllText(
                        Path.Combine(
                            temporaryRoot,
                            "Consumer.csproj"),
                        project)

                    let program =
                        """using Mayordomo.Core;

var initial = MatchState.Create(
  MatchId.Create("consumer-smoke"),
  randomSeed: 42UL);

var transition = MatchEngine.Execute(
  initial,
  JoinParticipantCommand.Create(
    CommandId.Create("join-001"),
    initial.MatchId,
    initial.Revision,
    ParticipantId.Create("participant-001")));

if (transition.State.Revision != Revision.Create(1))
{
  throw new InvalidOperationException(
    "Installed package did not execute the expected transition.");
}

var replayed = MatchEngine.Reduce(
  initial,
  transition.Events);

if (MatchStateHasher.Compute(transition.State) !=
    MatchStateHasher.Compute(replayed))
{
  throw new InvalidOperationException(
    "Installed package did not replay deterministically.");
}

Console.WriteLine(
  $"Mayordomo.Core consumer smoke passed at revision {transition.State.Revision}.");
"""

                    File.WriteAllText(
                        Path.Combine(
                            temporaryRoot,
                            "Program.cs"),
                        program)

                    let restore =
                        ProcessRunner.run
                            temporaryRoot
                            "dotnet"
                            [
                                "restore"
                                "Consumer.csproj"
                                "--configfile"
                                "NuGet.Config"
                            ]

                    if restore <> 0 then
                        restore
                    else
                        ProcessRunner.run
                            temporaryRoot
                            "dotnet"
                            [
                                "run"
                                "--project"
                                "Consumer.csproj"
                                "--configuration"
                                "Release"
                                "--no-restore"
                            ]
                finally
                    try
                        Directory.Delete(
                            temporaryRoot,
                            true)
                    with _ ->
                        ()

module ReleaseContract =
    let apiBaseline =
        "20B4806BBD144D2B317E701BB8EF3FED656AE9E731E1D48414D53298F199E70A"

    let validate
        (workflow: string)
        (changelog: string)
        (releasing: string)
        =
        let errors = ResizeArray<string>()

        let requiredWorkflowFragments =
            [
                "\"$TOOLING_DLL\" release-version"
                "\"$TOOLING_DLL\" release-contract"
                "\"$TOOLING_DLL\" public-readiness"
                "\"$TOOLING_DLL\" engine-budget"
                "\"$TOOLING_DLL\" package-audit"
                "\"$TOOLING_DLL\" consumer-smoke"
                "\"$TOOLING_DLL\" benchmark-budget"
                "source-sha: ${{ steps.version.outputs.source_sha }}"
                "finalize-release:"
                "if: github.event_name == 'workflow_dispatch' && inputs.publish"
                "PACKAGE_VERSION: ${{ needs.release-candidate.outputs.package-version }}"
                "SOURCE_SHA: ${{ needs.release-candidate.outputs.source-sha }}"
                "sha256sum -c artifacts/nuget/SHA256SUMS.txt"
                "api.nuget.org/v3-flatcontainer/mayordomo.core/$PACKAGE_VERSION"
                "tag=\"v$PACKAGE_VERSION\""
                "git/ref/tags/$tag"
                "-f sha=\"$SOURCE_SHA\""
                "gh release upload \"$tag\" \"${assets[@]}\" --clobber"
                "gh release create \"$tag\" \"${assets[@]}\""
                "--verify-tag"
                "release-manifest.json"
                "stateHashFormatVersion: 2"
                "packageSha256: $packageSha256"
                "symbolPackageSha256: $symbolPackageSha256"
                apiBaseline
                "test \"$tag_sha\" = \"$SOURCE_SHA\""
                "\"Mayordomo.Core.$PACKAGE_VERSION.nupkg\""
                "\"Mayordomo.Core.$PACKAGE_VERSION.snupkg\""
                "\"SHA256SUMS.txt\""
                "\"core-benchmark.json\""
                "required_assets=("
                "for expected in \"${required_assets[@]}\"; do"
                "for attempt in {1..180}; do"
            ]

        for fragment in requiredWorkflowFragments do
            if
                not (
                    workflow.Contains(
                        fragment,
                        StringComparison.Ordinal)
                )
            then
                errors.Add(
                    $"Missing final-release workflow contract: {fragment}")

        if
            workflow.Contains(
                "inputs.build_number",
                StringComparison.Ordinal)
            || workflow.Contains(
                "      build_number:\n",
                StringComparison.Ordinal)
        then
            errors.Add(
                "Release build selection must be dynamic; manual build_number input is forbidden.")

        if
            workflow.Contains(
                ".mjs",
                StringComparison.OrdinalIgnoreCase)
            || workflow.Contains(
                "node scripts/",
                StringComparison.OrdinalIgnoreCase)
        then
            errors.Add(
                "Release workflow must use F# Mayordomo.Tooling instead of JavaScript CI scripts.")

        let releaseCandidateIndex =
            workflow.IndexOf(
                "\n  release-candidate:",
                StringComparison.Ordinal)

        let publishIndex =
            workflow.IndexOf(
                "\n  publish:",
                StringComparison.Ordinal)

        let finalizeIndex =
            workflow.IndexOf(
                "\n  finalize-release:",
                StringComparison.Ordinal)

        if
            releaseCandidateIndex < 0
            || publishIndex < 0
            || finalizeIndex < 0
            || publishIndex <= releaseCandidateIndex
            || finalizeIndex <= publishIndex
        then
            errors.Add(
                "Release candidate, publish, and finalization jobs must exist in that order.")
        else
            let candidateBlock =
                workflow.Substring(
                    releaseCandidateIndex,
                    publishIndex - releaseCandidateIndex)

            let dispatchOnlyCandidateSteps =
                [
                    "Restore engine tests"
                    "Build exact release assembly and engine tests"
                    "Enforce engine footprint"
                    "Test engine"
                    "Pack exact release artifacts"
                    "Verify package and symbol artifacts"
                    "Audit NuGet package contract"
                    "Smoke test installed package"
                    "Restore benchmark harness"
                    "Build benchmark harness"
                    "Run measured performance suite"
                    "Upload certified release candidate"
                ]

            for stepName in dispatchOnlyCandidateSteps do
                let marker =
                    $"- name: {stepName}"

                let stepIndex =
                    candidateBlock.IndexOf(
                        marker,
                        StringComparison.Ordinal)

                if stepIndex < 0 then
                    errors.Add(
                        $"Release candidate is missing required step: {stepName}")
                else
                    let nextStepIndex =
                        candidateBlock.IndexOf(
                            "\n      - name:",
                            stepIndex + marker.Length,
                            StringComparison.Ordinal)

                    let stepEnd =
                        if nextStepIndex < 0 then
                            candidateBlock.Length
                        else
                            nextStepIndex

                    let stepBlock =
                        candidateBlock.Substring(
                            stepIndex,
                            stepEnd - stepIndex)

                    if
                        not (
                            stepBlock.Contains(
                                "if: github.event_name == 'workflow_dispatch'",
                                StringComparison.Ordinal)
                        )
                    then
                        errors.Add(
                            $"Release candidate step '{stepName}' must run only on workflow_dispatch.")

            let publishBlock =
                workflow.Substring(
                    publishIndex,
                    finalizeIndex - publishIndex)

            let finalizeBlock =
                workflow.Substring(
                    finalizeIndex)

            if
                not (
                    publishBlock.Contains(
                        "contents: read",
                        StringComparison.Ordinal)
                )
                || not (
                    publishBlock.Contains(
                        "id-token: write",
                        StringComparison.Ordinal)
                )
            then
                errors.Add(
                    "NuGet publish job must retain contents: read and id-token: write.")

            if
                publishBlock.Contains(
                    "contents: write",
                    StringComparison.Ordinal)
            then
                errors.Add(
                    "NuGet publish job must not receive repository contents write permission.")

            let checkoutIndex =
                publishBlock.IndexOf(
                    "uses: actions/checkout@v7",
                    StringComparison.Ordinal)

            let certifiedRefIndex =
                publishBlock.IndexOf(
                    "ref: ${{ needs.release-candidate.outputs.source-sha }}",
                    StringComparison.Ordinal)

            let setupDotnetIndex =
                publishBlock.IndexOf(
                    "uses: actions/setup-dotnet@v6",
                    StringComparison.Ordinal)

            if
                checkoutIndex < 0
                || certifiedRefIndex < 0
                || setupDotnetIndex < 0
                || checkoutIndex >= setupDotnetIndex
                || certifiedRefIndex >= setupDotnetIndex
                || not (
                    publishBlock.Contains(
                        "persist-credentials: false",
                        StringComparison.Ordinal)
                )
            then
                errors.Add(
                    "NuGet publish job must checkout the certified source before setup-dotnet.")

            let finalizeCheckoutIndex =
                finalizeBlock.IndexOf(
                    "uses: actions/checkout@v7",
                    StringComparison.Ordinal)

            let finalizeCertifiedRefIndex =
                finalizeBlock.IndexOf(
                    "ref: ${{ needs.release-candidate.outputs.source-sha }}",
                    StringComparison.Ordinal)

            let releaseCommandIndex =
                finalizeBlock.IndexOf(
                    "gh release view",
                    StringComparison.Ordinal)

            if
                finalizeCheckoutIndex < 0
                || finalizeCertifiedRefIndex < 0
                || releaseCommandIndex < 0
                || finalizeCheckoutIndex >= releaseCommandIndex
                || finalizeCertifiedRefIndex >= releaseCommandIndex
                || not (
                    finalizeBlock.Contains(
                        "persist-credentials: false",
                        StringComparison.Ordinal)
                )
            then
                errors.Add(
                    "Finalization job must checkout the certified source before GitHub Release commands.")

            if
                not (
                    finalizeBlock.Contains(
                        "contents: write",
                        StringComparison.Ordinal)
                )
            then
                errors.Add(
                    "Finalization job requires contents: write to create tag/release metadata.")

            if
                finalizeBlock.Contains(
                    "id-token: write",
                    StringComparison.Ordinal)
            then
                errors.Add(
                    "Finalization job must not receive OIDC permission.")

            if
                not (
                    finalizeBlock.Contains(
                        "- release-candidate",
                        StringComparison.Ordinal)
                )
                || not (
                    finalizeBlock.Contains(
                        "- publish",
                        StringComparison.Ordinal)
                )
            then
                errors.Add(
                    "Finalization job must depend on candidate certification and NuGet publish.")

            if
                not (
                    finalizeBlock.Contains(
                        "--clobber",
                        StringComparison.Ordinal)
                )
            then
                errors.Add(
                    "GitHub Release asset upload must be repairable on a job-only rerun.")

        if
            Regex.IsMatch(
                changelog,
                "pre-alpha|no supported packaged release",
                RegexOptions.IgnoreCase)
        then
            errors.Add(
                "CHANGELOG still claims the project has no supported package release.")

        let changelogMarkers =
            [
                "## 1.0 release train"
                "Mayordomo.Core"
                "Trusted Publishing"
                "canonical state-hash format v2"
                apiBaseline
            ]

        for marker in changelogMarkers do
            if
                not (
                    changelog.Contains(
                        marker,
                        StringComparison.Ordinal)
                )
            then
                errors.Add(
                    $"CHANGELOG is missing release marker: {marker}")

        let releasingMarkers =
            [
                "finalize-release"
                "same workflow run"
                "Do not dispatch a new publish run"
                "v{packageVersion}"
                "release-manifest.json"
            ]

        for marker in releasingMarkers do
            if
                not (
                    releasing.Contains(
                        marker,
                        StringComparison.Ordinal)
                )
            then
                errors.Add(
                    $"Release documentation is missing recovery marker: {marker}")

        List.ofSeq errors

    let run (_: string array) =
        let errors =
            validate
                (Internal.readText(
                    ".github/workflows/publish-nuget.yml"))
                (Internal.readText(
                    "CHANGELOG.md"))
                (Internal.readText(
                    "docs/RELEASING.md"))

        if errors.IsEmpty then
            Console.WriteLine(
                "FINAL RELEASE CONTRACT CHECK PASSED")
            0
        else
            Internal.printErrors
                "FINAL RELEASE CONTRACT CHECK FAILED"
                errors

            1
