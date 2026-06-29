using Avalonia.Controls;
using Avalonia.Interactivity;
using DayZ.MaskEditor.App.Services;
using DayZ.MaskEditor.App.ViewModels;

namespace DayZ.MaskEditor.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (DataContext is MainViewModel vm)
        {
            vm.Dialogs = new DialogService(this);
            Canvas.Document = vm.Document;
            vm.Canvas = Canvas;
        }
    }
}
