using Sgol.Cv04Demo;

if (!DemoOptions.TryParse(args, out var options))
{
    Console.Error.WriteLine("CV04_ARGUMENTS_INVALID");
    return 2;
}

return await Cv04DemoApplication.RunAsync(options!);
