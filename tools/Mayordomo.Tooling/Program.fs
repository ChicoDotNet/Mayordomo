namespace Mayordomo.Tooling

module Program =
    [<EntryPoint>]
    let main argv =
        if argv.Length = 0 then
            eprintfn "Usage: Mayordomo.Tooling <command> [arguments]"
            2
        else
            let command = argv[0]
            let args = argv[1..]

            match command with
            | "branch-name" -> BranchPolicy.run args
            | "release-version" -> ReleaseVersion.run args
            | "benchmark-budget" -> BenchmarkBudget.run args
            | "engine-budget" -> EngineBudget.run args
            | "public-readiness" -> PublicReadiness.run args
            | "package-audit" -> PackageAudit.run args
            | "consumer-smoke" -> ConsumerSmoke.run args
            | "release-contract" -> ReleaseContract.run args
            | _ ->
                eprintfn $"Unknown Mayordomo.Tooling command: {command}"
                2
