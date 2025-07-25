using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;
using Serilog;
using System.Text;


namespace RevitBIMTool.Utils.Common;
internal sealed class FailuresPreprocessor : IFailuresPreprocessor
{
    public string Output;
    public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
    {
        FailureProcessingResult result = FailuresHanding.ProcessFailures(failuresAccessor, out Output);
        return result;
    }
}


public static class FailuresHanding
{
    private static string output;

    public static string ElementIdsToSemicolonDelimitedText(IEnumerable<ElementId> elementIds)
    {
        return string.Join("; ", elementIds.Select(elementId => elementId.IntegerValue.ToString()));
    }


    public static string ReportFailureWarning(FailureMessageAccessor failure, FailureDefinitionRegistry failureReg)
    {
        StringBuilder builder = new();
        FailureSeverity failureSeverity = failure.GetSeverity();
        builder = builder.AppendLine(failure.GetDescriptionText());

        FailureDefinitionAccessor failureDefinition = failureReg.FindFailureDefinition(failure.GetFailureDefinitionId());

        if (failureSeverity is FailureSeverity.Error or FailureSeverity.Warning)
        {
            ICollection<ElementId> failingElementIds = failure.GetFailingElementIds();
            if (failingElementIds.Count > 0)
            {
                builder = builder.AppendLine("Failing element ids: " + ElementIdsToSemicolonDelimitedText(failingElementIds));
            }
            ICollection<ElementId> additionalElementIds = failure.GetAdditionalElementIds();
            if (additionalElementIds.Count > 0)
            {
                builder = builder.AppendLine("Additional element ids: " + ElementIdsToSemicolonDelimitedText(additionalElementIds));
            }
        }
        if (failureSeverity == FailureSeverity.Error)
        {
            if (failure.HasResolutions())
            {
                builder = builder.AppendLine("Applicable resolution types:\t");
                FailureResolutionType defaultResolutionType = failureDefinition.GetDefaultResolutionType();
                foreach (FailureResolutionType resolutionType in failureDefinition.GetApplicableResolutionTypes())
                {
                    if (defaultResolutionType.Equals(resolutionType))
                    {
                        string caption = failureDefinition.GetResolutionCaption(resolutionType);
                        builder = builder.AppendLine($"{resolutionType} {caption}");
                    }
                }
            }
            else
            {
                builder = builder.AppendLine("WARNING: no resolutions available");
            }
        }

        return builder.ToString();
    }


    public static FailureProcessingResult ProcessFailures(FailuresAccessor failuresAccessor, out string output)
    {
        FailureProcessingResult result = FailureProcessingResult.Continue;
        FailureDefinitionRegistry failureReg = Autodesk.Revit.ApplicationServices.Application.GetFailureDefinitionRegistry();
        IList<FailureMessageAccessor> failures = failuresAccessor.GetFailureMessages();
        output = string.Empty;
        if (failures.Any())
        {
            try
            {
                foreach (FailureMessageAccessor failure in failures)
                {
                    output += "\n" + ReportFailureWarning(failure, failureReg);
                    FailureSeverity failureSeverity = failure.GetSeverity();
                    if (failureSeverity == FailureSeverity.Warning)
                    {
                        failuresAccessor.DeleteWarning(failure);
                    }
                    else if (failureSeverity == FailureSeverity.Error && failure.HasResolutions())
                    {
                        if (failure.HasResolutionOfType(FailureResolutionType.UnlockConstraints))
                        {
                            failure.SetCurrentResolutionType(FailureResolutionType.UnlockConstraints);
                        }
                        else if (failure.HasResolutionOfType(FailureResolutionType.DetachElements))
                        {
                            failure.SetCurrentResolutionType(FailureResolutionType.DetachElements);
                        }
                        else if (failure.HasResolutionOfType(FailureResolutionType.SkipElements))
                        {
                            failure.SetCurrentResolutionType(FailureResolutionType.SkipElements);
                        }
                        failuresAccessor.ResolveFailure(failure);
                        result = FailureProcessingResult.ProceedWithCommit;
                    }
                    else
                    {
                        result = FailureProcessingResult.ProceedWithRollBack;
                    }
                }
            }
            catch (Exception)
            {
                result = FailureProcessingResult.Continue;
            }
        }
        return result;
    }


    public static void SetTransactionFailureOptions(Transaction transaction)
    {
        FailureHandlingOptions failureOptions = transaction.GetFailureHandlingOptions();
        failureOptions = failureOptions.SetFailuresPreprocessor(new FailuresPreprocessor());
        failureOptions = failureOptions.SetForcedModalHandling(true);
        failureOptions = failureOptions.SetClearAfterRollback(true);
        transaction.SetFailureHandlingOptions(failureOptions);
    }


    public static void SetFailuresAccessorFailureOptions(FailuresAccessor failuresAccessor)
    {
        FailureHandlingOptions failureOptions = failuresAccessor.GetFailureHandlingOptions();
        failureOptions = failureOptions.SetForcedModalHandling(true);
        failureOptions = failureOptions.SetClearAfterRollback(true);
        failuresAccessor.SetFailureHandlingOptions(failureOptions);
    }


    private static void FailuresProcessingHandler(object sender, FailuresProcessingEventArgs args)
    {
        FailuresAccessor failuresAccessor = args.GetFailuresAccessor();
        FailureProcessingResult result = ProcessFailures(failuresAccessor, out output);
        SetFailuresAccessorFailureOptions(failuresAccessor);
        args.SetProcessingResult(result);
    }


    public static string WithFailuresProcessingHandler(Autodesk.Revit.ApplicationServices.Application app, Func<string> action)
    {
        string result = default;

        app.FailuresProcessing += new EventHandler<FailuresProcessingEventArgs>(FailuresProcessingHandler);

        try
        {
            result = action();

            if (!string.IsNullOrWhiteSpace(output))
            {
                result += $"\n FailuresProcessingHandler: {output}";
            }
        }
        catch (Exception ex)
        {
            result = ex.Message;
            Log.Fatal(result);
        }
        finally
        {
            app.FailuresProcessing -= new EventHandler<FailuresProcessingEventArgs>(FailuresProcessingHandler);
        }

        return result;
    }

}
