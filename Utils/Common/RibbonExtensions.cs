using Autodesk.Revit.UI;
using System.Reflection;

#if WINDOWS
using System.Windows.Media.Imaging;
#endif

using RibbonButton = Autodesk.Revit.UI.RibbonButton;


namespace RevitBIMTool.Utils.Common;
public static class RibbonExtensions
{
    public static RibbonPanel CreatePanel(this UIControlledApplication application, string panelName, string tabName)
    {
        RibbonPanel resultPanel = null;
        application.CreateRibbonTab(tabName);
        foreach (RibbonPanel ribbonPanel in application.GetRibbonPanels(tabName))
        {
            if (ribbonPanel.Name.Equals(panelName))
            {
                resultPanel = ribbonPanel;
                break;
            }
        }

        return resultPanel ?? application.CreateRibbonPanel(tabName, panelName);
    }


    public static PushButton CreatePushButton(this RibbonPanel panel, Type command, string buttonText)
    {
        PushButtonData itemData = new(command.Name, buttonText, command.Assembly.Location, command.FullName);
        return (PushButton)panel.AddItem(itemData);
    }


    public static PulldownButton AddPullDownButton(this RibbonPanel panel, string internalName, string buttonText)
    {
        PulldownButtonData itemData = new(internalName, buttonText);
        return (PulldownButton)panel.AddItem(itemData);
    }


    public static SplitButton AddSplitButton(this RibbonPanel panel, string internalName, string buttonText)
    {
        SplitButtonData itemData = new(internalName, buttonText);
        return (SplitButton)panel.AddItem(itemData);
    }


    public static RadioButtonGroup AddRadioButtonGroup(this RibbonPanel panel, string internalName)
    {
        RadioButtonGroupData itemData = new(internalName);
        return (RadioButtonGroup)panel.AddItem(itemData);
    }


    public static ComboBox AddComboBox(this RibbonPanel panel, string internalName)
    {
        ComboBoxData itemData = new(internalName);
        return (ComboBox)panel.AddItem(itemData);
    }


    public static TextBox AddTextBox(this RibbonPanel panel, string internalName)
    {
        TextBoxData itemData = new(internalName);
        return (TextBox)panel.AddItem(itemData);
    }


    public static PushButton AddPushButton(this PulldownButton pullDownButton, Type command, string buttonText)
    {
        PushButtonData buttonData = new(command.FullName, buttonText, Assembly.GetAssembly(command).Location, command.FullName);
        return pullDownButton.AddPushButton(buttonData);
    }


    public static PushButton AddPushButton<TCommand>(this PulldownButton pullDownButton, string buttonText) where TCommand : IExternalCommand, new()
    {
        Type typeFromHandle = typeof(TCommand);
        PushButtonData buttonData = new(typeFromHandle.FullName, buttonText, Assembly.GetAssembly(typeFromHandle).Location, typeFromHandle.FullName);
        return pullDownButton.AddPushButton(buttonData);
    }


    public static void SetImage(this RibbonButton button, string uri)
    {
#if WINDOWS
        button.Image = new BitmapImage(new Uri(uri, UriKind.RelativeOrAbsolute));
#endif
    }


    public static void SetLargeImage(this RibbonButton button, string uri)
    {
#if WINDOWS
        button.LargeImage = new BitmapImage(new Uri(uri, UriKind.RelativeOrAbsolute));
#endif
    }


    public static void SetAvailabilityController<T>(this PushButton button) where T : IExternalCommandAvailability, new()
    {
        button.AvailabilityClassName = typeof(T).FullName;
    }
}
