using Autodesk.Revit.UI;
using RevitBIMTool.Commands;
using RevitBIMTool.Utils;
using System.Diagnostics;
using System.Windows.Media.Imaging;


namespace RevitBIMTool.Core;
public static class SetupUIPanel
{
    private static RibbonPanel ribbonPanel;
    private static readonly string appName = "Timas BIM Tool";
    private static readonly string ribbonPanelName = "Automation";


    [STAThread]
    public static void Initialize(UIControlledApplication uicontrol)
    {
        try
        {
            uicontrol.CreateRibbonTab(appName);
            ribbonPanel = uicontrol.CreateRibbonPanel(appName, ribbonPanelName);
            ribbonPanel = uicontrol.GetRibbonPanels(appName).FirstOrDefault();
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }


        if (ribbonPanel != null)
        {
            if (ribbonPanel.CreatePushButton(typeof(ExportToPdfCommand), "ExportToNWC to PDF") is PushButton button01)
            {
                button01.AvailabilityClassName = typeof(ExportToPdfCommand).FullName;
                button01.SetImage("/RevitBIMTool;component/Resources/Icons/RibbonIcon16.png");
                button01.SetLargeImage("/RevitBIMTool;component/Resources/Icons/RibbonIcon32.png");
                ribbonPanel.AddSeparator();
            }

            if (ribbonPanel.CreatePushButton(typeof(ExportToDWGCommand), "ExportToNWC to DWG") is PushButton button02)
            {
                button02.AvailabilityClassName = typeof(ExportToDWGCommand).FullName;
                button02.SetImage("/RevitBIMTool;component/Resources/Icons/RibbonIcon16.png");
                button02.SetLargeImage("/RevitBIMTool;component/Resources/Icons/RibbonIcon32.png");
                ribbonPanel.AddSeparator();
            }

            if (ribbonPanel.CreatePushButton(typeof(ExportToNWCCommand), "ExportToNWC to NWC") is PushButton button03)
            {
                button03.AvailabilityClassName = typeof(ExportToNWCCommand).FullName;
                button03.SetImage("/RevitBIMTool;component/Resources/Icons/RibbonIcon16.png");
                button03.SetLargeImage("/RevitBIMTool;component/Resources/Icons/RibbonIcon32.png");
                ribbonPanel.AddSeparator();
            }

            if (ribbonPanel.CreatePushButton(typeof(TestCommand), "Test") is PushButton button)
            {
                button.AvailabilityClassName = typeof(TestCommand).FullName;
                button.SetImage("/RevitBIMTool;component/Resources/Icons/RibbonIcon16.png");
                button.SetLargeImage("/RevitBIMTool;component/Resources/Icons/RibbonIcon32.png");
                ribbonPanel.AddSeparator();
            }

        }


    }


    public static void SetImage(this RibbonButton button, string uri)
    {
        button.Image = new BitmapImage(new Uri(uri, UriKind.RelativeOrAbsolute));
    }


    public static void SetLargeImage(this RibbonButton button, string uri)
    {
        button.LargeImage = new BitmapImage(new Uri(uri, UriKind.RelativeOrAbsolute));
    }

}