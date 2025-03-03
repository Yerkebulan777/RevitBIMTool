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
    public partial class LintelMarker
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

            IList<Element> instances = new FilteredElementCollector(_doc)
                .OfCategory(bic)
                .OfClass(typeof(FamilyInstance))
                .ToElements();

            List<FamilyInstance> lintels = instances
                .OfType<FamilyInstance>()
                .Where(instance => instance.Symbol != null)
                .Where(instance => instance.Symbol.FamilyName.Equals(familyName, comp))
                .ToList();

            return lintels;
        }

        /// <summary>
        /// Маркирует перемычки с унификацией похожих элементов
        /// </summary>
        /// <param name="lintels">Список перемычек</param>
        public void MarkLintels(List<FamilyInstance> lintels)
        {
            if (lintels.Count != 0)
            {
                List<LintelData> data = GetLintelData(lintels);

                Dictionary<SizeKey, List<LintelData>> groups = GroupLintels(data);

                //MergeSmallGroups(groups, data);

                //AssignMarks(groups, data);

                //using Transaction t = new(_doc, "Маркировка перемычек");

                //_ = t.Start();

                //foreach (FamilyInstance lintel in lintels)
                //{
                //    if (data.ContainsKey(lintel) && data[lintel].Mark != null)
                //    {
                //        string mark = data[lintel].Mark;

                //        LintelUtils.SetMark(lintel, _config.MarkParam, mark);

                //        LintelData lintelData = data[lintel];

                //        string typeName = $"{mark} {lintelData.Thick}x{lintelData.Width}x{lintelData.Height}";

                //        LintelUtils.SetTypeName(lintel, typeName);
                //    }
                //}

                //_ = t.Commit();
            }
        }

        /// <summary>
        /// Получает данные о перемычках
        /// </summary>
        /// <param name="lintels">Список перемычек</param>
        /// <returns>Словарь с данными о перемычках</returns>
        private List<LintelData> GetLintelData(List<FamilyInstance> lintels)
        {
            List<LintelData> lintelDataList = [];

            foreach (FamilyInstance lintel in lintels)
            {
                double thickRound = UnitManager.FootToRoundedMm(LintelUtils.GetParamValue(lintel, _config.ThickParam), _config.RoundBase);

                double heightRound = UnitManager.FootToRoundedMm(LintelUtils.GetParamValue(lintel, _config.HeightParam), _config.RoundBase);

                double widthRound = UnitManager.FootToRoundedMm(LintelUtils.GetParamValue(lintel, _config.WidthParam), _config.RoundBase);

                SizeKey dimensions = new(thickRound, widthRound, heightRound);

                LintelData data = new(lintel)
                {
                    Thick = thickRound,
                    Height = heightRound,
                    Width = widthRound,
                    Size = dimensions,
                };

                lintelDataList.Add(data);
            }

            return lintelDataList;
        }

        /// <summary>
        /// Группирует перемычки по размерам
        /// </summary>
        /// <param name="lintelDataList">Данные о перемычках</param>
        /// <returns>Словарь групп перемычек</returns>
        private Dictionary<SizeKey, List<LintelData>> GroupLintels(List<LintelData> lintelDataList)
        {
            Dictionary<SizeKey, List<LintelData>> groups = lintelDataList
                .GroupBy(ld => new SizeKey(ld.Thick, ld.Height, ld.Width))
                .ToDictionary(g => g.Key, g => g.ToList());

            //Dictionary<SizeKey, List<LintelData>> groups = [];

            //foreach (LintelData lintelData in lintelDataList)
            //{
            //    FamilyInstance lintel = lintelData.Instance;

            //    SizeKey dimensions = new(lintelData.Thick, lintelData.Width, lintelData.Height);

            //    lintelData.Size = dimensions; // Устанавливаем размер ???

            //    if (!groups.ContainsKey(dimensions))
            //    {
            //        groups[dimensions] = [];
            //    }

            //    groups[dimensions].Add(lintelData);
            //}

            return groups;
        }

        /// <summary>
        /// Объединяет малочисленные группы с подходящими группами
        /// </summary>
        /// <param name="groups">Словарь групп перемычек</param>
        /// <param name="data">Данные о перемычках</param>
        protected void MergeSmallGroups(Dictionary<SizeKey, List<LintelData>> groups, int threshold)
        {
            // Шаг 1: Подготовка данных для группировки
            Dictionary<SizeKey, int> groupSizes = groups.ToDictionary(g => g.Key, g => g.Value.Count);
            List<SizeKey> smallGroups = FindSmallGroups(groupSizes, threshold);
            List<SizeKey> allGroups = groupSizes.Keys.ToList();

            // Нечего объединять, если нет малых групп или всего одна группа
            if (smallGroups.Count != 0 && allGroups.Count > 1)
            {
                // Шаг 2: Создаем и заполняем структуру для отслеживания объединений
                UnionSize unionFind = new(allGroups);

                // Шаг 3: Ищем и применяем лучшие объединения
                FindAndMergeGroups(smallGroups, allGroups, unionFind, groupSizes);

                // Шаг 4: Применяем результаты объединений к данным
                ApplyGroupMerges(groups, data, unionFind);
            }
        }

        /// <summary>
        /// Находит малые группы, которые нужно объединить
        /// </summary>
        private List<SizeKey> FindSmallGroups(Dictionary<SizeKey, int> groupSizes, int minCount)
        {
            return groupSizes.Where(g => g.Value < minCount).Select(g => g.Key).ToList();
        }

        /// <summary>
        /// Ищет и выполняет объединение групп на основе их сходства
        /// </summary>
        private void FindAndMergeGroups(List<SizeKey> smallGroups, List<SizeKey> allGroups, UnionSize unionFind, Dictionary<SizeKey, int> groupSizes)
        {
            // Сначала находим лучшие совпадения для всех малых групп
            List<GroupMatch> bestMatches = FindBestMatches(smallGroups, allGroups);

            // Объединяем группы в порядке качества совпадения (от лучшего к худшему)
            foreach (GroupMatch match in bestMatches.OrderBy(m => m.Score))
            {
                SizeKey smallKey = match.Source;
                SizeKey targetKey = match.Target;

                // Получаем текущие корневые группы
                SizeKey smallRoot = unionFind.FindRoot(smallKey);
                SizeKey targetRoot = unionFind.FindRoot(targetKey);

                // Объединяем только если группы еще не объединены 
                // и малая группа все еще мала после предыдущих объединений
                int currentSizeOfSmallRoot = CalculateCurrentGroupSize(smallRoot, groupSizes, unionFind);

                if (!smallRoot.Equals(targetRoot) && currentSizeOfSmallRoot < _config.MinCount)
                {
                    unionFind.Union(smallKey, targetKey, groupSizes);
                }
            }
        }

        /// <summary>
        /// Рассчитывает текущий размер группы с учетом всех выполненных объединений
        /// </summary>
        private int CalculateCurrentGroupSize(SizeKey rootKey, Dictionary<SizeKey, int> groupSizes, UnionSize unionFind)
        {
            // Считаем суммарный размер всех групп, объединенных с этой
            int totalSize = 0;

            foreach (SizeKey key in groupSizes.Keys)
            {
                if (unionFind.FindRoot(key).Equals(rootKey))
                {
                    totalSize += groupSizes[key];
                }
            }

            return totalSize;
        }

        /// <summary>
        /// Находит лучшие совпадения для малых групп
        /// </summary>
        private List<GroupMatch> FindBestMatches(List<SizeKey> smallGroups, List<SizeKey> allGroups)
        {
            List<GroupMatch> matches = [];

            foreach (SizeKey smallKey in smallGroups)
            {
                double bestScore = double.MaxValue;
                SizeKey? bestTarget = null;

                foreach (SizeKey targetKey in allGroups)
                {
                    if (smallKey.Equals(targetKey))
                    {
                        continue;
                    }

                    // Проверяем попадание в допуски
                    if (IsWithinTolerances(smallKey, targetKey))
                    {
                        double score = CalculateWeightedDifference(smallKey, targetKey);

                        if (score < bestScore)
                        {
                            bestScore = score;
                            bestTarget = targetKey;
                        }
                    }
                }

                if (bestTarget.HasValue)
                {
                    matches.Add(new GroupMatch(smallKey, bestTarget.Value, bestScore));
                }
            }

            return matches;
        }

        /// <summary>
        /// Проверяет, находятся ли размеры в пределах допусков
        /// </summary>
        private bool IsWithinTolerances(SizeKey source, SizeKey target)
        {
            double diffThick = Math.Abs(source.Thick - target.Thick);
            double diffWidth = Math.Abs(source.Width - target.Width);
            double diffHeight = Math.Abs(source.Height - target.Height);

            // Проверяем индивидуальные допуски
            bool withinIndividualTolerances =
                diffThick <= _config.ThickTolerance &&
                diffWidth <= _config.WidthTolerance &&
                diffHeight <= _config.HeightTolerance;

            // Проверяем общий допуск
            double totalDeviation = diffThick + diffWidth + diffHeight;
            bool withinTotalDeviation = totalDeviation <= _config.MaxTotalDeviation;

            return withinIndividualTolerances && withinTotalDeviation;
        }

        /// <summary>
        /// Применяет результаты объединения к данным перемычек
        /// </summary>
        private void ApplyGroupMerges(
            Dictionary<SizeKey, List<FamilyInstance>> groups,
            Dictionary<FamilyInstance, LintelData> data,
            UnionSize unionFind)
        {
            Dictionary<SizeKey, List<FamilyInstance>> newGroups = [];

            // Для каждой исходной группы
            foreach (KeyValuePair<SizeKey, List<FamilyInstance>> entry in groups)
            {
                SizeKey originalKey = entry.Key;
                SizeKey rootKey = unionFind.FindRoot(originalKey);

                // Создаем новую группу, если еще не существует
                if (!newGroups.ContainsKey(rootKey))
                {
                    newGroups[rootKey] = [];
                }

                // Обновляем размеры в данных и добавляем в новую группу
                foreach (FamilyInstance lintel in entry.Value)
                {
                    if (data.ContainsKey(lintel))
                    {
                        data[lintel].Size = rootKey;
                    }

                    newGroups[rootKey].Add(lintel);
                }
            }

            // Заменяем старые группы на новые
            groups.Clear();

            foreach (KeyValuePair<SizeKey, List<FamilyInstance>> entry in newGroups)
            {
                groups[entry.Key] = entry.Value;
            }
        }


        /// <summary>
        /// Вычисляет взвешенную разницу между двумя ключами размеров
        /// </summary>
        /// <param name="source">Исходный ключ</param>
        /// <param name="target">Целевой ключ</param>
        /// <returns>Взвешенная разница</returns>
        private double CalculateWeightedDifference(SizeKey source, SizeKey target)
        {
            double totalDiff = 0;

            double weightFactor = 10.0;

            for (int i = 0; i < _config.GroupingOrder.Count; i++)
            {
                double weight = Math.Pow(weightFactor, _config.GroupingOrder.Count - i);

                switch (_config.GroupingOrder[i])
                {
                    case GroupingParameter.Thick:
                        totalDiff += Math.Abs(source.Thick - target.Thick) * weight;
                        break;
                    case GroupingParameter.Width:
                        totalDiff += Math.Abs(source.Width - target.Width) * weight;
                        break;
                    case GroupingParameter.Height:
                        totalDiff += Math.Abs(source.Height - target.Height) * weight;
                        break;
                }
            }

            return totalDiff;
        }

        /// <summary>
        /// Назначает марки перемычкам
        /// </summary>
        /// <param name="groups">Словарь групп перемычек</param>
        /// <param name="data">Данные о перемычках</param>
        private void AssignMarks(Dictionary<SizeKey, List<FamilyInstance>> groups, Dictionary<FamilyInstance, LintelData> data)
        {
            // Создаем сортировщик групп на основе конфигурации
            List<SizeKey> sortedGroups = SortGroupsByConfiguration(groups.Keys);

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

        /// <summary>
        /// Сортирует группы по заданному в конфигурации порядку
        /// </summary>
        /// <param name="groups">Группы для сортировки</param>
        /// <returns>Отсортированный список групп</returns>
        private List<SizeKey> SortGroupsByConfiguration(IEnumerable<SizeKey> groups)
        {
            IOrderedEnumerable<SizeKey> orderedGroups = null;

            // Применяем сортировку согласно указанному порядку
            for (int i = 0; i < _config.GroupingOrder.Count; i++)
            {
                GroupingParameter parameter = _config.GroupingOrder[i];

                switch (parameter)
                {
                    case GroupingParameter.Thick:
                        orderedGroups = groups.OrderBy(g => g.Thick);
                        break;
                    case GroupingParameter.Width:
                        orderedGroups = groups.OrderBy(g => g.Width);
                        break;
                    case GroupingParameter.Height:
                        orderedGroups = groups.OrderBy(g => g.Height);
                        break;
                }
            }

            return orderedGroups.ToList();
        }
    }
}