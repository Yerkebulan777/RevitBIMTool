using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using System.Text;


namespace RevitBIMTool.Utils.Common;
public static class RevitDialogHanding
{
    public const int IDOK = 1;
    public const int IDCANCEL = 2;
    public const int IDYES = 6;
    public const int IDNO = 7;
    public const int IDCLOSE = 8;


    public static string HandleDialogShowingEvent(object sender, DialogBoxShowingEventArgs args)
    {
        int dialogResult = IDOK;
        StringBuilder msg = new();
        try
        {
            string dialogId = args.DialogId;
            msg = msg.AppendLine($"DialogId: {dialogId}");
            if (args is TaskDialogShowingEventArgs taskEventArgs)
            {
                msg = msg.AppendLine("\tMessage: " + taskEventArgs.Message);
                if (taskEventArgs.DialogId == "TaskDialog_Missing_Third_Party_Updater")
                {
                    dialogResult = 1001;
                }
                if (taskEventArgs.DialogId == "TaskDialog_Location_Position_Changed")
                {
                    dialogResult = 1002;
                }
            }
            else if (args is MessageBoxShowingEventArgs messageEventArgs)
            {
                msg = msg.AppendLine("\tMessage: " + messageEventArgs.Message);
                msg = msg.AppendLine("\tDialogType: " + messageEventArgs.DialogType);
            }

            if (args.OverrideResult(dialogResult))
            {
                msg = msg.AppendLine("Send key: " + dialogResult);
            }
        }
        catch (Exception ex)
        {
            msg = msg.AppendLine("Caught exception in dialog event handler!");
            msg = msg.AppendLine("SendExceptionAsync message: " + ex.Message);
        }

        return msg.ToString();
    }


    public static string WithDialogBoxShowingHandler(UIApplication uiapp, Func<string> action)
    {
        string result = default;

        void DialogShowingEventHandler(object sender, DialogBoxShowingEventArgs e)
        {
            string output = HandleDialogShowingEvent(sender, e);
            if (!string.IsNullOrWhiteSpace(output))
            {
                result += "\n DialogEventHandler: " + output;
            }
        }

        uiapp.DialogBoxShowing += DialogShowingEventHandler;
        try
        {
            result = action();
        }
        finally
        {
            uiapp.DialogBoxShowing -= DialogShowingEventHandler;
        }

        return result;
    }



}
