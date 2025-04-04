using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.Extensions.DependencyInjection;
using RevEng.Common;
using RevEng.Common.Dab;
using RevEng.Core;
using RevEng.Core.Abstractions.Model;
#if NET8_0 && !CORE90
using Microsoft.Data.SqlClient;
using RevEng.Core.DacpacReport;
#endif
using RevEng.Core.Diagram;

[assembly: CLSCompliant(true)]
[assembly: SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task", Justification = "Reviewed")]

namespace EfReveng
{
    internal static class Program
    {
        public static async System.Threading.Tasks.Task<int> Main(string[] args)
        {
            try
            {
                Console.OutputEncoding = Encoding.UTF8;

                ArgumentNullException.ThrowIfNull(args);

                if (args.Length > 0)
                {
                    if ((args.Length == 3 || args.Length == 4)
                        && int.TryParse(args[1], out int dbTypeInt)
                        && bool.TryParse(args[0], out bool mergeDacpacs))
                    {
                        SchemaInfo[] schemas = null;
                        if (args.Length == 4)
                        {
                            schemas = args[3].Split(',').Select(s => new SchemaInfo { Name = s }).ToArray();
                        }

                        var reverseEngineerCommandOptions = new ReverseEngineerCommandOptions
                        {
                            ConnectionString = args[2].ApplyDatabaseType((DatabaseType)dbTypeInt),
                            DatabaseType = (DatabaseType)dbTypeInt,
                            MergeDacpacs = mergeDacpacs,
                        };

                        var provider = new ServiceCollection().AddEfpt(reverseEngineerCommandOptions, new List<string>(), new List<string>(), new List<string>()).BuildServiceProvider();
                        var procedureModelFactory = provider.GetRequiredService<IProcedureModelFactory>();
                        var functionModelFactory = provider.GetRequiredService<IFunctionModelFactory>();
                        var databaseModelFactory = provider.GetRequiredService<IDatabaseModelFactory>();
                        var builder = new TableListBuilder(reverseEngineerCommandOptions, procedureModelFactory, functionModelFactory, databaseModelFactory, schemas);

                        var buildResult = builder.GetTableModels();

                        buildResult.AddRange(builder.GetProcedures());

                        buildResult.AddRange(builder.GetFunctions());

                        await Console.Out.WriteLineAsync("Result:");
                        await Console.Out.WriteLineAsync(buildResult.Write());

                        return 0;
                    }

                    // dgml <options file> <connection string> [schemas]
                    if ((args.Length == 3 || args.Length == 4)
                        && (args[0] == "dgml")
                        && new FileInfo(args[1]).Exists)
                        {
                        var schemas = Enumerable.Empty<string>().ToList();
                        if (args.Length == 4)
                        {
                            schemas = args[3].Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s).ToList();
                        }

                        var dabOptions = DataApiBuilderOptionsExtensions.TryRead(args[1]);

                        if (dabOptions == null)
                        {
                            await Console.Out.WriteLineAsync("Error:");
                            await Console.Out.WriteLineAsync("Could not read options");
                            return 1;
                        }

                        dabOptions.ConnectionString = args[2];

                        var builder = new DiagramBuilder(dabOptions, schemas);

                        var buildResult = string.Empty;

                        buildResult = builder.GetDgmlFileName();

                        await Console.Out.WriteLineAsync("Result:");
                        await Console.Out.WriteLineAsync(buildResult);

                        return 0;
                    }

#if NET8_0 && !CORE90
                    if (args.Length == 2
                        && args[0] == "dacpacreport"
                        && new FileInfo(args[1]).Exists)
                    {
                        var builder = new DacpacReportBuilder(new FileInfo(args[1]));

                        var buildResult = builder.BuildReport();

                        await Console.Out.WriteLineAsync("Result:");
                        await Console.Out.WriteLineAsync(buildResult);

                        return 0;
                    }

                    if (args.Length == 2
                        && args[0] == "dacpacreportextract")
                    {
                        var extractor = new DacpacExtractor(new SqlConnectionStringBuilder(args[1]));

                        var dacpacPath = extractor.ExtractDacpac();

                        var builder = new DacpacReportBuilder(dacpacPath);

                        var buildResult = builder.BuildReport();

                        await Console.Out.WriteLineAsync("Result:");
                        await Console.Out.WriteLineAsync(buildResult);

                        return 0;
                    }
#endif
#if NET8_0
                    // erdiagram <options file> <connection string>
                    if (args.Length == 3
                        && args[0] == "erdiagram"
                        && new FileInfo(args[1]).Exists)
                    {
                        var dabOptions = DataApiBuilderOptionsExtensions.TryRead(args[1]);

                        if (dabOptions == null)
                        {
                            await Console.Out.WriteLineAsync("Error:");
                            await Console.Out.WriteLineAsync("Could not read options");
                            return 1;
                        }

                        dabOptions.ConnectionString = args[2];

                        var builder = new ErDiagramBuilder(dabOptions);

                        var buildResult = builder.GetErDiagramFileName();

                        await Console.Out.WriteLineAsync("Result:");
                        await Console.Out.WriteLineAsync(buildResult);

                        return 0;
                    }

                    if (args.Length == 3
                        && args[0] == "dabbuilder"
                        && new FileInfo(args[1]).Exists)
                    {
                        var dabOptions = DataApiBuilderOptionsExtensions.TryRead(args[1]);

                        if (dabOptions == null)
                        {
                            await Console.Out.WriteLineAsync("Error:");
                            await Console.Out.WriteLineAsync("Could not read options");
                            return 1;
                        }

                        dabOptions.ConnectionString = args[2];

                        var builder = new DabBuilder(dabOptions);

                        var buildResult = builder.GetDabConfigCmdFile();

                        await Console.Out.WriteLineAsync("Result:");
                        await Console.Out.WriteLineAsync(buildResult);

                        return 0;
                    }
#endif
                    if (!File.Exists(args[0]))
                    {
                        await Console.Out.WriteLineAsync("Error:");
                        await Console.Out.WriteLineAsync($"Could not open options file: {args[0]}");
                        return 1;
                    }

                    var options = ReverseEngineerOptionsExtensions.TryDeserialize(await File.ReadAllTextAsync(args[0], Encoding.UTF8));

                    if (options == null)
                    {
                        await Console.Out.WriteLineAsync("Error:");
                        await Console.Out.WriteLineAsync("Could not read options");
                        return 1;
                    }

                    var result = ReverseEngineerRunner.GenerateFiles(options);

                    await Console.Out.WriteLineAsync("Result:");
                    await Console.Out.WriteLineAsync(result.Write());
                }
                else
                {
                    await Console.Out.WriteLineAsync("Error:");
                    await Console.Out.WriteLineAsync("Invalid command line");
                    return 1;
                }

                return 0;
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
            {
                await Console.Out.WriteLineAsync("Error:");
                await Console.Out.WriteLineAsync(ex.Demystify().ToString());
                return 1;
            }
#pragma warning restore CA1031 // Do not catch general exception types
        }
    }
}