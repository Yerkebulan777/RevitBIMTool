using Autodesk.Revit.DB;
using System.Text;


namespace RevitBIMTool.Utils;
public sealed class WarningSwallower : IFailuresPreprocessor
{
    private IList<FailureResolutionType> resolutionList;
    private readonly StringBuilder warningText = new StringBuilder();
    public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
    {
        foreach (FailureMessageAccessor failure in failuresAccessor.GetFailureMessages())
        {
            if (failure.GetSeverity() == FailureSeverity.None) { continue; }
            resolutionList = failuresAccessor.GetAttemptedResolutionTypes(failure);
            if (resolutionList.Count > 1)
            {
                warningText.AppendLine("Cannot resolve failures");
                return FailureProcessingResult.ProceedWithRollBack;
            }
            else
            {
                FailureResolutionType resolution = resolutionList.FirstOrDefault();
                warningText.AppendLine($"Fail: {failure.GetDescriptionText()} {resolution}");
                if (resolution == FailureResolutionType.Invalid)
                {
                    return FailureProcessingResult.ProceedWithRollBack;
                }
                else if (resolution == FailureResolutionType.DeleteElements)
                {
                    ICollection<ElementId> ids = failure.GetFailingElementIds();
                    failuresAccessor.DeleteElements(ids.ToList());
                }
                else if (resolution == FailureResolutionType.Others)
                {
                    failuresAccessor.DeleteWarning(failure);
                }
                else { failuresAccessor.ResolveFailure(failure); }

                return FailureProcessingResult.ProceedWithCommit;
            }
        }
        resolutionList?.Clear();
        return FailureProcessingResult.Continue;
    }


    public string GetWarningMessage()
    {
        return "Post Processing Failures: " + warningText.ToString();
    }
}
