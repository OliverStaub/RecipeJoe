using RecipeJoe.Sweep;

return await Cli.RunAsync(args, Console.Out, Console.Error, Environment.GetEnvironmentVariable, CancellationToken.None);
