namespace IterateFiles;

public class UserEventHandler
{
    public void ButtonClickHandler(object sender, RoutedEventArgs e)
    {
        Button clickedButton = (Button)sender;
        string? buttonContent = clickedButton.Content.ToString();

        // Log the Button Click event with the button name
        string logEntry = $"Received '{e.RoutedEvent.Name}' event on the '{buttonContent}' button";
        Window? parentWindow = GetParentControl(sender);
        if (parentWindow is not null) 
        {
            logEntry += $" from the '{parentWindow.GetType().FullName}' window";
        }
        Log.Information(logEntry);
    }

    private static Window? GetParentControl(object control)
    {
        DependencyObject? parentElement = control as DependencyObject;

        while (parentElement != null && !(parentElement is Window))
        {
            parentElement = VisualTreeHelper.GetParent(parentElement);
        }

        if (parentElement is Window window)
        {
            return window;
        }
        return null;
    }
}