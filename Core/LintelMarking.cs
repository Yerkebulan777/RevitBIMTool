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
            Dictionary<SizeKey, List<FamilyInstance>> groups = GroupLintels(data);

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

                SizeKey dimensions = new(thickRound, widthRound, heightRound);

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
        private Dictionary<SizeKey, List<FamilyInstance>> GroupLintels(Dictionary<FamilyInstance, LintelData> data)
        {
            Dictionary<SizeKey, List<FamilyInstance>> groups = new Dictionary<SizeKey, List<FamilyInstance>>();

            foreach (var kvp in data)
            {
                FamilyInstance lintel = kvp.Key;
                LintelData lintelData = kvp.Value;

                SizeKey dimensions = new SizeKey(
                    lintelData.Thick,
                    lintelData.Width,
                    lintelData.Height);

                lintelData.Size = dimensions;

                if (!groups.ContainsKey(dimensions))
                {
                    groups[dimensions] = new List<FamilyInstance>();
                }

                groups[dimensions].Add(lintel);
            }

            return groups;
        }

        private void MergeSmallGroups(Dictionary<SizeKey, List<FamilyInstance>> groups, Dictionary<FamilyInstance, LintelData> data)
        {
            // Находим малые и большие группы
            List<SizeKey> smallGroups = groups.Where(g => g.Value.Count < _config.MinCount).Select(g => g.Key).ToList();
            List<SizeKey> largeGroups = groups.Where(g => g.Value.Count >= _config.MinCount).Select(g => g.Key).ToList();

            // Если нет больших групп, нечего объединять
            if (largeGroups.Count == 0)
            {
                return;
            }

            foreach (SizeKey smallDimensions in smallGroups)
            {
                // Если группа уже удалена в процессе объединения, пропускаем

                if (!groups.ContainsKey(smallDimensions))
                {
                    continue;
                }

                // Ищем ближайшую большую группу

                SizeKey? bestMatch = null;

                double minDiff = double.MaxValue;

                foreach (SizeKey largeDimensions in largeGroups)
                {
                    // Вычисляем разницу по каждому параметру
                    double diffThick = Math.Abs(smallDimensions.Thick- largeDimensions.Thick);
                    double diffWidth = Math.Abs(smallDimensions.Width - largeDimensions.Width);
                    double diffHeight = Math.Abs(smallDimensions.Height - largeDimensions.Height);

                    // Проверяем, что разница в пределах порога
                    if (diffThick <= _config.Threshold &&
                        diffWidth <= _config.Threshold &&
                        diffHeight <= _config.Threshold)
                    {
                        // Общая разница
                        double totalDiff = diffThick + diffWidth + diffHeight;

                        if (totalDiff < minDiff)
                        {
                            minDiff = totalDiff;
                            bestMatch = largeDimensions;
                        }
                    }
                }

                // Если нашли подходящую группу, объединяем с ней
                if (bestMatch.HasValue)
                {
                    // Обновляем группу в данных
                    foreach (FamilyInstance lintel in groups[smallDimensions])
                    {
                        if (data.ContainsKey(lintel))
                        {
                            data[lintel].Size = bestMatch.Value;
                        }
                    }

                    // Добавляем элементы в большую группу
                    groups[bestMatch.Value].AddRange(groups[smallDimensions]);

                    // Удаляем малую группу
                    groups.Remove(smallDimensions);
                }
            }
        }

        /// <summary>
        /// Назначает марки перемычкам
        /// </summary>
        /// <param name="groups">Словарь групп перемычек</param>
        /// <param name="data">Данные о перемычках</param>
        private void AssignMarks(Dictionary<SizeKey, List<FamilyInstance>> groups, Dictionary<FamilyInstance, LintelData> data)
        {
            // Сортируем группы
            List<SizeKey> sortedGroups = groups.Keys
                .OrderBy(g => g.Thick)  // По толщине
                .ThenBy(g => g.Width)   // По ширине
                .ThenBy(g => g.Height)  // По высоте
                .ToList();

            // Назначаем марки группам
            for (int i = 0; i < sortedGroups.Count; i++)
            {
                SizeKey group = sortedGroups[i];

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