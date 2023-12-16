namespace IterateFiles;

public partial class App : Application
{
    public static IHost? AppHost { get; private set; }
    readonly string appName = AppDomain.CurrentDomain.FriendlyName;

    public App()
    {
        AppHost = Host.CreateDefaultBuilder()
        .ConfigureServices((hostContext, services) =>
        {
            services.AddSingleton<MainWindow>();
            services.AddTransient<MainWindowModel>();
        })
        .Build();
        AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(MyUnhandledExceptionHandler);
    }

    void App_SessionEnding(object sender, SessionEndingCancelEventArgs e)
    {
        Log.Debug($"Session ending event received [{e.ReasonSessionEnding}]");
        e.Cancel = false;
    }

    private static void MyUnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs args)
    {
        Exception ex = (Exception)args.ExceptionObject;
        Log.Error(ex, "UnhandledException: {message}", ex.Message);
        MessageBox.Show($"UnhandledException found: {ex.GetType().FullName}\nMessage: {ex.Message}\nMethod: {ex.TargetSite?.DeclaringType?.FullName}\nRuntime terminating: {args.IsTerminating}\nStacktrace: {ex.StackTrace}", "UnhandledException", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Debug()
        .WriteTo.File($"{appName}_.log",
            rollingInterval: RollingInterval.Day,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{ThreadId}] [{Level:u4}] {Message:lj}{NewLine}{Exception}")
        .Enrich.WithThreadId()
        .CreateLogger();
        
        await AppHost!.StartAsync();
        var startupForm = AppHost.Services.GetRequiredService<MainWindow>();
        startupForm.Show();
        Log.Information("Application started");
        if (SynchronizationContext.Current != null)
        {
            TaskScheduler taskScheduler = TaskScheduler.FromCurrentSynchronizationContext();
            if (taskScheduler != null)
            {
                // The current thread is the UI thread
                Log.Debug($"The current thread with ID {Environment.CurrentManagedThreadId} is the UI thread");
            }
        } else {
            Log.Debug($"The current thread with ID {Environment.CurrentManagedThreadId} is not the UI thread");
        }
        
        Log.Debug("The number of processors on this computer is {processorCount}", Environment.ProcessorCount);

        UserEventHandler userEventHandler = new();

        // Register the Button Click event using RegisterClassHandler
        EventManager.RegisterClassHandler(typeof(Button), Button.ClickEvent, new RoutedEventHandler(userEventHandler.ButtonClickHandler));
    }
    protected override async void OnExit(ExitEventArgs e)
    {
        Log.Information("Closing application event received");
        Log.Information("Application closed");
        await AppHost!.StopAsync();
        base.OnExit(e);
    }
}