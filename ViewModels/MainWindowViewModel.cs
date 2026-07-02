using CommunityToolkit.Mvvm.ComponentModel;
using IMV.State;
using RadianTools.UI.WPF.Common;

namespace IMV.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private IFolderItem? _selectedFolder;

    [ObservableProperty]
    private string _selectedTreePath = "";

    [ObservableProperty]
    private ImageViewState _viewState = ImageViewState.Shared;
}
