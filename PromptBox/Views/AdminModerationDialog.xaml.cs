using PromptBox.Models;
using PromptBox.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;

namespace PromptBox.Views;

/// <summary>
/// Admin dialog for moderating community template submissions
/// </summary>
public partial class AdminModerationDialog : Window, INotifyPropertyChanged
{
    private readonly IPromptCommunityService _communityService;
    private ObservableCollection<PromptTemplate> _pendingSubmissions = new();
    private PromptTemplate? _selectedSubmission;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<PromptTemplate> PendingSubmissions
    {
        get => _pendingSubmissions;
        set { _pendingSubmissions = value; OnPropertyChanged(); OnPropertyChanged(nameof(PendingCount)); }
    }

    public PromptTemplate? SelectedSubmission
    {
        get => _selectedSubmission;
        set { _selectedSubmission = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasSelection)); }
    }

    public int PendingCount => PendingSubmissions.Count;
    public bool HasSelection => SelectedSubmission != null;

    public AdminModerationDialog(IPromptCommunityService communityService)
    {
        _communityService = communityService;
        InitializeComponent();
        DataContext = this;
        _ = LoadPendingSubmissionsAsync();
    }

    private async Task LoadPendingSubmissionsAsync()
    {
        var submissions = await _communityService.GetPendingSubmissionsAsync();
        PendingSubmissions = new ObservableCollection<PromptTemplate>(submissions);
        OnPropertyChanged(nameof(PendingCount));
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await LoadPendingSubmissionsAsync();
    }

    private async void Approve_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedSubmission == null) return;

        var result = await _communityService.ApproveSubmissionAsync(SelectedSubmission.Id);
        if (result)
        {
            MessageBox.Show($"Template '{SelectedSubmission.Title}' has been approved and is now visible to all users.", 
                "Approved", MessageBoxButton.OK, MessageBoxImage.Information);
            PendingSubmissions.Remove(SelectedSubmission);
            SelectedSubmission = null;
            OnPropertyChanged(nameof(PendingCount));
        }
        else
        {
            MessageBox.Show("Failed to approve submission. Please try again.", 
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void Reject_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedSubmission == null) return;

        var confirmResult = MessageBox.Show(
            $"Are you sure you want to reject '{SelectedSubmission.Title}'?\n\nThis action cannot be undone.",
            "Confirm Rejection", 
            MessageBoxButton.YesNo, 
            MessageBoxImage.Warning);

        if (confirmResult != MessageBoxResult.Yes) return;

        var result = await _communityService.RejectSubmissionAsync(SelectedSubmission.Id);
        if (result)
        {
            MessageBox.Show($"Template '{SelectedSubmission.Title}' has been rejected.", 
                "Rejected", MessageBoxButton.OK, MessageBoxImage.Information);
            PendingSubmissions.Remove(SelectedSubmission);
            SelectedSubmission = null;
            OnPropertyChanged(nameof(PendingCount));
        }
        else
        {
            MessageBox.Show("Failed to reject submission. Please try again.", 
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
