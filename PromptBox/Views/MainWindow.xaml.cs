using MaterialDesignThemes.Wpf;
using PromptBox.ViewModels;
using System.Windows;

namespace PromptBox.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.SnackbarMessageQueue = MainSnackbar.MessageQueue!;
    }
}
