using Sgol.Cv05Demo;

if (!DemoOptions.TryParse(args, out var options))
{
    Console.Error.WriteLine("CV05_ARGUMENTS_INVALID");
    return 2;
}

return await Cv05DemoApplication.RunAsync(options!);
