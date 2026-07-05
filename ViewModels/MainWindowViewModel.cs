namespace IMV.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using IMV.State;
using RadianTools.UI.WPF.Common;

public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private IFolderItem? _selectedFolder;

    [ObservableProperty]
    private string _selectedTreePath = "";

    [ObservableProperty]
    private ImageViewState _viewState = ImageViewState.Shared;
}
