namespace IMV.Common;

using RadianTools.UI.WPF.Imaging;

public class IMVImageFactory
{
    public static IImageFactory Shared = new ImageFactoryGroup(
        new RsImageFactory(),
        new MFImageFactory(30.0)
    );
}
