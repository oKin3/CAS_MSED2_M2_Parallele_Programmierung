namespace IterateFiles.View;

public partial class MainWindow : Window
{
    public MainWindow(MainWindowModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}

