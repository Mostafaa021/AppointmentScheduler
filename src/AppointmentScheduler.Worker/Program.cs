// Wolverine message handlers are added in M5 (spec 008).
var builder = Host.CreateApplicationBuilder(args);

var host = builder.Build();
host.Run();
