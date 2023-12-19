namespace IterateFiles.ViewModel;

public partial class MainWindowModel : BaseViewModel
{
    [ObservableProperty]
    private string fileSizeFilter = "1";
    
    [ObservableProperty]
    private string currentMode = string.Empty;
    
    [ObservableProperty]
    private string currentWork = string.Empty;

    [ObservableProperty]
    private string currentFile = string.Empty;

    [ObservableProperty]
    private string currentFilesCount = string.Empty;

    [ObservableProperty]
    private string currentDirectoriesCount = string.Empty;

    [ObservableProperty]
    private string result = string.Empty;

    [ObservableProperty]
    private bool notInProcess = true;

    private int fileCount = 0;
    private int directoriesCount = 0;
    private long maxFileSize = 1L * 1024 * 1024 * 1024;
    private readonly string filesystem = "C:\\";
    private readonly string formatTime = "{0:D2}:{1:D2}:{2:D2}.{3:D3}";
    public ObservableCollection<FileModel> Items { get; } = new ObservableCollection<FileModel>();

    [RelayCommand]
    private void Clear()
    {
        Items.Clear();
        CurrentWork = string.Empty;
        CurrentFile = string.Empty;
        CurrentFilesCount = string.Empty;
        CurrentDirectoriesCount = string.Empty;
        Result = string.Empty;
        fileCount = 0;
        directoriesCount = 0;
    }

    [RelayCommand]
    private async void Start() 
    {
        await SearchFilesAsync(ButtonExecutionType.NormalExecution);
    }

    [RelayCommand]
    private async void StartWithTasks()
    {
        await SearchFilesAsync(ButtonExecutionType.MultipleTasks);
    }

    [RelayCommand]
    private async void StartWithParallelForEach() 
    {
        await SearchFilesAsync(ButtonExecutionType.ParallelForEach);
    }

    private async Task SearchFilesAsync(ButtonExecutionType executionType)
    {
        Log.Information("Start process for {executionType}", Enum.GetName(executionType));
        NotInProcess = false;
        Clear();
        long.TryParse(FileSizeFilter, out long filterInGigabyte);
        maxFileSize = 1024 * 1024 * 1024 * filterInGigabyte;
        Log.Debug("Used file size filter: {fileSizeFilter}", maxFileSize);
        CurrentWork = $"Please wait...";
        var stopwatch = Stopwatch.StartNew();
        await Task.Run(async () =>
        {
            switch (executionType)
            {
                case ButtonExecutionType.NormalExecution:
                    CurrentMode = "StartWithSingleTask";
                    await EnumerateAllDirectoriesAsync(filesystem);
                    break;
                case ButtonExecutionType.MultipleTasks:
                    CurrentMode = "StartWithMultipleTasks";
                    await EnumerateAllDirectoriesWithMultipleTasksAsync(filesystem);
                    break;
                case ButtonExecutionType.ParallelForEach:
                    CurrentMode = "StartWithParallelForEachTasks";
                    await EnumerateAllDirectoriesWithParallelForEachAsync(filesystem);
                    break;
            }
        });
        TimeSpan elapsedTimeAll = stopwatch.Elapsed;

        string formattedTimeAll = string.Format(formatTime,
            (int)elapsedTimeAll.TotalHours,
            elapsedTimeAll.Minutes,
            elapsedTimeAll.Seconds,
            elapsedTimeAll.Milliseconds);

        Log.Information("Processed {0} files in {1}", fileCount, formattedTimeAll);
        Result = $"Processed {fileCount} files in {formattedTimeAll} [hh:mm:ss.ms]";
        NotInProcess = true;
    }

    private async Task EnumerateAllDirectoriesAsync(string path)
    {
        Log.Debug("Process {method}", nameof(EnumerateAllDirectoriesAsync));
        var options = new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = true };
        var directories = Directory.EnumerateDirectories(path, "*", options);
        if (directories is null)
        {
            return;
        }

        var enumerator = directories.GetEnumerator();
        while (true)
        {
            try
            {
                if (!enumerator.MoveNext())
                {
                    break;
                }

                var filePath = enumerator.Current;
                await EnumerateFilesFromDirectoryAsync(filePath);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception in access file: {message}", ex.Message);
                // move to the next item
                try
                {
                    enumerator.MoveNext();
                }
                catch (Exception)
                {
                    // Exception occurs while calling the enumerator.MoveNext() method
                    // Ignore the exception
                }
            }
        }
    }

    private async Task EnumerateAllDirectoriesWithMultipleTasksAsync(string path)
    {
        Log.Debug("Process {method}", nameof(EnumerateAllDirectoriesWithMultipleTasksAsync));
        var options = new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = true };
        var directories = Directory.EnumerateDirectories(path, "*", options);
        if (directories is null)
        {
            return;
        }

        var tasks = directories.Select(async directory =>
        {
            try
            {
                await EnumerateFilesFromDirectoryMultipleAsync(directory);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception in access file: {message}", ex.Message);
            }
        });

        await Task.WhenAll(tasks);
    }

    private async Task EnumerateAllDirectoriesWithParallelForEachAsync(string path)
    {
        Log.Debug("Process {method}", nameof(EnumerateAllDirectoriesWithParallelForEachAsync));
        var options = new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = true };
        var directories = Directory.EnumerateDirectories(path, "*", options);
        if (directories is null)
        {
            return;
        }

        var tasks = new List<Task>();

        try
        {
            Parallel.ForEach(directories, async (directory) =>
            {
                var tcs = new TaskCompletionSource<bool>();
                try
                {
                    await EnumerateFilesFromDirectoryWithParallelForEachAsync(directory);
                    tcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Exception in access file: {message}", ex.Message);
                    tcs.SetException(ex);
                }
                if (tcs.Task != null)
                {
                    tasks.Add(tcs.Task);
                }
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Exception in access directory: {message}", ex.Message);
        }

        await Task.WhenAll(tasks);
    }


    private async Task EnumerateFilesFromDirectoryAsync(string directory)
    {
        Log.Debug("Process {method}", nameof(EnumerateFilesFromDirectoryAsync));
        Interlocked.Increment(ref directoriesCount);
        CurrentDirectoriesCount = directoriesCount.ToString();
        var options = new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = false };
        var tempFiles = new DirectoryInfo(directory);
        var enumerator = tempFiles.EnumerateFiles("*.*", options).Where(file => file.Length > maxFileSize).GetEnumerator();
        while (true)
        {
            try
            {
                if (!enumerator.MoveNext())
                {
                    break;
                }

                var file = enumerator.Current;
                var fileModel = CreateFileModel(file);
                CurrentFile = file.Name;
                Interlocked.Increment(ref fileCount);
                Log.Debug("FileCount '{fileCount}'", fileCount);
                CurrentFilesCount = fileCount.ToString();
                await Application.Current.Dispatcher.InvokeAsync(() => Items.Add(fileModel));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception in access file: {message}", ex.Message);
                // move to the next item
                try
                {
                    enumerator.MoveNext();
                }
                catch (Exception)
                {
                    // Exception occurs while calling the enumerator.MoveNext() method
                    // Ignore the exception
                }
            }
        }
    }

    private async Task EnumerateFilesFromDirectoryMultipleAsync(string directory)
    {
        Log.Debug("Process {method}", nameof(EnumerateFilesFromDirectoryMultipleAsync));
        Interlocked.Increment(ref directoriesCount);
        CurrentDirectoriesCount = directoriesCount.ToString();
        var options = new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = true };
        var tempFiles = new DirectoryInfo(directory);
        var files = tempFiles.EnumerateFiles("*.*", options).Where(file => file.Length > maxFileSize);

        var tasks = files.Select(async file =>
        {
            try
            {
                var fileModel = CreateFileModel(file);
                CurrentFile = file.Name;
                Interlocked.Increment(ref fileCount);
                Log.Debug("FileCount '{fileCount}'", fileCount);
                CurrentFilesCount = fileCount.ToString();
                await Application.Current.Dispatcher.InvokeAsync(() => Items.Add(fileModel));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception in access file: {message}", ex.Message);
            }
        });

        await Task.WhenAll(tasks);
    }

    private async Task EnumerateFilesFromDirectoryWithParallelForEachAsync(string directory)
    {
        Log.Debug("Process {method}", nameof(EnumerateFilesFromDirectoryWithParallelForEachAsync));
        Interlocked.Increment(ref directoriesCount);
        CurrentDirectoriesCount = directoriesCount.ToString();
        var options = new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = false };
        var tempFiles = new DirectoryInfo(directory);
        var files = tempFiles.EnumerateFiles("*.*", options).Where(file => file.Length > maxFileSize);

        var tasks = new List<Task>();

        try
        {
            Parallel.ForEach(files, async (file) =>
            {
                var tcs = new TaskCompletionSource<bool>();

                try
                {
                    var fileModel = CreateFileModel(file);
                    CurrentFile = file.Name;
                    Interlocked.Increment(ref fileCount);
                    Log.Debug("FileCount '{fileCount}'", fileCount);
                    CurrentFilesCount = fileCount.ToString();
                    await Application.Current.Dispatcher.InvokeAsync(() => Items.Add(fileModel));
                    tcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Exception in access file: {message}", ex.Message);
                    tcs.SetException(ex);
                }
                if (tcs.Task != null)
                {
                    tasks.Add(tcs.Task);
                }
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Exception in access directory: {message}", ex.Message);
        }

        await Task.WhenAll(tasks);
    }

    private static FileModel CreateFileModel(FileInfo fileInfo)
    {
        return new FileModel
        {
            Name = fileInfo.Name,
            Path = fileInfo.FullName,
            Size = fileInfo.Length,
            LastModified = fileInfo.LastWriteTime
        };
    }
}