using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Database;
using System.Configuration;
using System.Globalization;
using System.Windows;

namespace RevitBIMTool.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class DatabaseTestCommand : IExternalCommand, IExternalCommandAvailability
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

            try
            {
                // Получаем строку подключения
                string connectionString = ConfigurationManager.ConnectionStrings["PrinterDatabase"]?.ConnectionString;

                if (string.IsNullOrEmpty(connectionString))
                {
                    const string errorMsg = "✗ Connection string not found";
                    ShowResult(errorMsg, false);
                    return Result.Failed;
                }

                // Проверка состояния БД
                using DatabaseMonitor healthChecker = new(connectionString);
                string healthReport = healthChecker.CheckDatabaseHealth();

                try
                {
                    // Дополнительная проверка схемы через SchemaManager
                    using SchemaManager schemaManager = new(connectionString);

                    if (schemaManager.ValidateSchema())
                    {
                        healthReport += "\n✓ Дополнительная валидация схемы пройдена успешно";
                    }
                    else
                    {
                        healthReport += "\n⚠️  Дополнительная валидация схемы выявила проблемы";
                    }
                }
                catch (Exception ex)
                {
                    healthReport += $"\n⚠️  Ошибка дополнительной валидации: {ex.Message}";
                }

                // Определяем успешность по содержанию отчета
                bool isSuccess = healthReport.Contains("РАБОТАЕТ КОРРЕКТНО");

                ShowResult(healthReport, isSuccess);

                return isSuccess ? Result.Succeeded : Result.Failed;
            }
            catch (Exception ex)
            {
                string errorReport = $"💥 КРИТИЧЕСКАЯ ОШИБКА\n\nИсключение: {ex.Message}";

                if (ex.InnerException != null)
                {
                    errorReport += $"\n\nВнутренняя ошибка: {ex.InnerException.Message}";
                }

                ShowResult(errorReport, false);
                Clipboard.SetText(ex.ToString());
                return Result.Failed;
            }
        }


        private static void ShowResult(string reportText, bool isSuccess)
        {
            string title = isSuccess ? "База данных принтеров - ОК ✓" : "База данных принтеров - ПРОБЛЕМЫ ⚠️";
            TaskDialogIcon icon = isSuccess ? TaskDialogIcon.TaskDialogIconInformation : TaskDialogIcon.TaskDialogIconWarning;

            TaskDialog dialog = new("Database Health Check")
            {
                MainInstruction = title,
                MainContent = reportText,
                MainIcon = icon,
                FooterText = "Отчет скопирован в буфер обмена"
            };

            _ = dialog.Show();
            Clipboard.SetText(reportText);
        }


        public bool IsCommandAvailable(UIApplication applicationData, CategorySet selectedCategories)
        {
            return true; // Команда всегда доступна для административных целей
        }


    }
}