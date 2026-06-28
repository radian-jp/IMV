using CommunityToolkit.Mvvm.ComponentModel;
using RadianTools.UI.WPF.Common;

namespace IMV.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private IFolderItem? _selectedItem;

    [ObservableProperty]
    private string _selectedTreePath = "";
}
