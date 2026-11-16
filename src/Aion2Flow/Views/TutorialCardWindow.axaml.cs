using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Cloris.Aion2Flow.Views;

// The tutorial's card (MainWindow.Tutorial): one step's title and text, and Back, Next (Done on the last) and End.
public partial class TutorialCardWindow : Window
{
    public TutorialCardWindow() => InitializeComponent();

    public event Action? PreviousRequested;

    public event Action? NextRequested;

    public event Action? EndRequested;

    // count: "3 / 22"; next: "Next" or, on the last step, "Done".
    public void ShowStep(string count, string title, string text, string next, bool canGoBack)
    {
        StepCount.Text = count;
        StepTitle.Text = title;
        StepText.Text = text;
        NextButton.Content = next;
        BackButton.IsEnabled = canGoBack;
    }

    private void BackClicked(object? sender, RoutedEventArgs e) => PreviousRequested?.Invoke();

    private void NextClicked(object? sender, RoutedEventArgs e) => NextRequested?.Invoke();

    private void EndClicked(object? sender, RoutedEventArgs e) => EndRequested?.Invoke();

    private void DragWindow(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            WindowDrag.Move(this, e);
    }
}
