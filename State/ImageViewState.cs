using CommunityToolkit.Mvvm.ComponentModel;
using IMV.Views;

namespace IMV.State;

public partial class ImageViewState : ObservableObject
{
    public static ImageViewState Shared { get; } = new();

    [ObservableProperty]
    private int _selectedThumbnailIndex = -1;
}
