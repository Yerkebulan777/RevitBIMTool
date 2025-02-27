using Autodesk.Revit.DB;
using RevitBIMTool.Model;
using RevitBIMTool.Models;
using RevitBIMTool.Utils;
using RevitBIMTool.Utils.Common;


namespace RevitBIMTool.Core
{
    /// <summary>
    /// Основной класс для маркировки перемычек
    /// </summary>
    public class LintelMarker
    {
        private readonly Document _doc;
        private readonly MarkConfig _config;

        /// <summary>
        /// Создает экземпляр маркировщика перемычек с конфигурацией по умолчанию
        /// </summary>
        /// <param name="doc">Документ Revit</param>
        public LintelMarker(Document doc)
        {
            _config = new MarkConfig();
            _doc = doc;
        }

        /// <summary>
        /// Создает экземпляр маркировщика с указанной конфигурацией
        /// </summary>
        /// <param name="doc">Документ Revit</param>
        /// <param name="config">Конфигурация маркировки</param>
        public LintelMarker(Document doc, MarkConfig config)
        {
            _config = config;
            _doc = doc;
        }

        /// <summary>
        /// Находит все перемычки в модели на основе наименования семейства
        /// </summary>
        /// <returns>Список перемычек</returns>
        public List<FamilyInstance> FindByFamilyName(string familyName)
        {
            BuiltInCategory bic = BuiltInCategory.OST_StructuralFraming;

            StringComparison comp = StringComparison.CurrentCultureIgnoreCase;

            IList<Element> instances = new FilteredElementCollector(_doc).OfCategory(bic).OfClass(typeof(FamilyInstance)).ToElements();

            List<FamilyInstance> lintels = instances.OfType<FamilyInstance>()
                .Where(instance => instance.Symbol != null)
                .Where(instance => instance.Symbol.FamilyName.Equals(familyName, comp)).ToList();

            return lintels;
        }

        /// <summary>
        /// Маркирует перемычки с унификацией похожих элементов
        /// </summary>
        /// <param name="lintels">Список перемычек</param>
        public void MarkLintels(List<FamilyInstance> lintels)
        {
            if (lintels.Count == 0)
            {
                return;
            }

            // Получаем данные о перемычках
            Dictionary<FamilyInstance, LintelData> data = GetLintelData(lintels);

            // Группируем перемычки
            Dictionary<string, List<FamilyInstance>> groups = GroupLintels(data);

            // Объединяем малочисленные группы
            MergeSmallGroups(groups, data);

            // Назначаем марки
            AssignMarks(groups, data);

            // Применяем марки в Revit
            using Transaction t = new(_doc, "Маркировка перемычек");
            _ = t.Start();

            foreach (FamilyInstance lintel in lintels)
            {
                if (data.ContainsKey(lintel) && data[lintel].Mark != null)
                {
                    string mark = data[lintel].Mark;
                    LintelUtils.SetMark(lintel, _config.MarkParam, mark);
                }
            }

            _ = t.Commit();
        }

        /// <summary>
        /// Получает данные о перемычках
        /// </summary>
        /// <param name="lintels">Список перемычек</param>
        /// <returns>Словарь с данными о перемычках</returns>
        private Dictionary<FamilyInstance, LintelData> GetLintelData(List<FamilyInstance> lintels)
        {
            Dictionary<FamilyInstance, LintelData> result = [];

            foreach (FamilyInstance lintel in lintels)
            {
                double thickRound = UnitManager.FootToRoundedMm(LintelUtils.GetParamValue(lintel, _config.ThicknessParam), _config.RoundBase);

                double heightRound = UnitManager.FootToRoundedMm(LintelUtils.GetParamValue(lintel, _config.HeightParam), _config.RoundBase);

                double widthRound = UnitManager.FootToRoundedMm(LintelUtils.GetParamValue(lintel, _config.WidthParam), _config.RoundBase);

                Dimensions dimensions = new(thickRound, widthRound, heightRound);

                // Сохраняем данные
                LintelData data = new()
                {
                    Thick = thickRound,
                    Height = heightRound,
                    Width = widthRound,
                    Size = dimensions,
                };

                result[lintel] = data;
            }

            return result;
        }

        /// <summary>
        /// Группирует перемычки по размерам
        /// </summary>
        /// <param name="data">Данные о перемычках</param>
        /// <returns>Словарь групп перемычек</returns>
        private Dictionary<string, List<FamilyInstance>> GroupLintels(Dictionary<FamilyInstance, LintelData> data)
        {
            Dictionary<string, List<FamilyInstance>> groups = [];

            foreach (var kvp in data)
            {
                FamilyInstance lintel = kvp.Key;
                LintelData lintelData = kvp.Value;
                string group = lintelData.Group;

                if (!groups.ContainsKey(group))
                {
                    groups[group] = [];
                }

                groups[group].Add(lintel);
            }

            return groups;
        }

        /// <summary>
        /// Объединяет малочисленные группы с похожими большими группами
        /// </summary>
        /// <param name="groups">Словарь групп перемычек</param>
        /// <param name="data">Данные о перемычках</param>
        private void MergeSmallGroups(Dictionary<string, List<FamilyInstance>> groups, Dictionary<FamilyInstance, LintelData> data)
        {
            // Находим малые и большие группы
            List<string> smallGroups = groups.Where(g => g.Value.Count < _config.MinCount).Select(g => g.Key).ToList();
            List<string> largeGroups = groups.Where(g => g.Value.Count >= _config.MinCount).Select(g => g.Key).ToList();

            foreach (string small in smallGroups)
            {
                // Если группа уже удалена в процессе объединения, пропускаем
                if (!groups.ContainsKey(small))
                {
                    continue;
                }

                // Разбиваем идентификатор группы на размеры
                string[] parts = small.Split('_');

                double smallThick = double.Parse(parts[0]);
                double smallWidth = double.Parse(parts[1]);
                double smallHeight = double.Parse(parts[2]);

                // Ищем ближайшую большую группу
                string bestMatch = null;
                double minDiff = double.MaxValue;

                foreach (string largeGroup in largeGroups)
                {
                    // Разбиваем идентификатор большой группы
                    parts = largeGroup.Split('_');
                    double largeThickness = double.Parse(parts[0]);
                    double largeWidth = double.Parse(parts[1]);
                    double largeHeight = double.Parse(parts[2]);

                    // Вычисляем разницу по каждому параметру
                    double diffThickness = Math.Abs(smallThick - largeThickness);
                    double diffWidth = Math.Abs(smallWidth - largeWidth);
                    double diffHeight = Math.Abs(smallHeight - largeHeight);

                    // Проверяем, что разница в пределах порога
                    if (diffThickness <= _config.Threshold &&
                        diffWidth <= _config.Threshold &&
                        diffHeight <= _config.Threshold)
                    {
                        // Общая разница
                        double totalDiff = diffThickness + diffWidth + diffHeight;

                        if (totalDiff < minDiff)
                        {
                            minDiff = totalDiff;
                            bestMatch = largeGroup;
                        }
                    }
                }

                // Если нашли подходящую группу, объединяем с ней
                if (bestMatch != null)
                {
                    // Обновляем группу в данных
                    foreach (FamilyInstance lintel in groups[small])
                    {
                        if (data.ContainsKey(lintel))
                        {
                            data[lintel].Group = bestMatch;
                        }
                    }

                    // Добавляем элементы в большую группу
                    groups[bestMatch].AddRange(groups[small]);

                    // Удаляем малую группу
                    _ = groups.Remove(small);
                }
            }
        }

        /// <summary>
        /// Назначает марки перемычкам
        /// </summary>
        /// <param name="groups">Словарь групп перемычек</param>
        /// <param name="data">Данные о перемычках</param>
        private void AssignMarks(
            Dictionary<string, List<FamilyInstance>> groups, Dictionary<FamilyInstance, LintelData> data)
        {
            // Сортируем группы
            List<string> sortedGroups = groups.Keys
                .OrderBy(g => double.Parse(g.Split('_')[0]))  // По толщине
                .ThenBy(g => double.Parse(g.Split('_')[1]))   // По ширине
                .ThenBy(g => double.Parse(g.Split('_')[2]))   // По высоте
                .ToList();

            // Назначаем марки группам
            for (int i = 0; i < sortedGroups.Count; i++)
            {
                string group = sortedGroups[i];
                string mark = $"{_config.Prefix}{i + 1}";

                // Сохраняем марку для каждой перемычки в группе
                foreach (FamilyInstance lintel in groups[group])
                {
                    if (data.ContainsKey(lintel))
                    {
                        data[lintel].Mark = mark;
                    }
                }
            }
        }
    }
}