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

                    // Создаем имя типа на основе марки и размеров
                    LintelData lintelData = data[lintel];
                    string typeName = $"{mark} {lintelData.Thick}x{lintelData.Width}x{lintelData.Height}";
                    LintelUtils.SetTypeName(lintel, typeName);
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
                double thickRound = UnitManager.FootToRoundedMm(LintelUtils.GetParamValue(lintel, _config.ThickParam), _config.RoundBase);

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
            Dictionary<SizeKey, List<FamilyInstance>> groups = [];

            foreach (KeyValuePair<FamilyInstance, LintelData> kvp in data)
            {
                FamilyInstance lintel = kvp.Key;
                LintelData lintelData = kvp.Value;

                SizeKey dimensions = new(lintelData.Thick, lintelData.Width, lintelData.Height);

                lintelData.Size = dimensions;

                if (!groups.ContainsKey(dimensions))
                {
                    groups[dimensions] = [];
                }

                groups[dimensions].Add(lintel);
            }

            return groups;
        }

        /// <summary>
        /// Объединяет малочисленные группы с подходящими группами
        /// </summary>
        /// <param name="groups">Словарь групп перемычек</param>
        /// <param name="data">Данные о перемычках</param>
        private void MergeSmallGroups(Dictionary<SizeKey, List<FamilyInstance>> groups, Dictionary<FamilyInstance, LintelData> data)
        {
            // Шаг 1: Подготовка данных для группировки
            Dictionary<SizeKey, int> groupSizes = PrepareGroupSizes(groups);
            List<SizeKey> smallGroups = FindSmallGroups(groupSizes, _config.MinCount);
            List<SizeKey> allGroups = groupSizes.Keys.ToList();

            // Нечего объединять, если нет малых групп или всего одна группа
            if (smallGroups.Count == 0 || allGroups.Count <= 1)
                return;

            // Шаг 2: Создаем и заполняем структуру для отслеживания объединений
            UnionFindStructure unionFind = new UnionFindStructure(allGroups);

            // Шаг 3: Ищем и применяем лучшие объединения
            FindAndMergeGroups(smallGroups, allGroups, unionFind, groupSizes);

            // Шаг 4: Применяем результаты объединений к данным
            ApplyGroupMerges(groups, data, unionFind);
        }

        /// <summary>
        /// Подготавливает словарь с размерами групп
        /// </summary>
        private Dictionary<SizeKey, int> PrepareGroupSizes(Dictionary<SizeKey, List<FamilyInstance>> groups)
        {
            return groups.ToDictionary(g => g.Key, g => g.Value.Count);
        }

        /// <summary>
        /// Находит малые группы, которые нужно объединить
        /// </summary>
        private List<SizeKey> FindSmallGroups(Dictionary<SizeKey, int> groupSizes, int minCount)
        {
            return groupSizes.Where(g => g.Value < minCount)
                            .Select(g => g.Key)
                            .ToList();
        }

        /// <summary>
        /// Ищет и выполняет объединение групп на основе их сходства
        /// </summary>
        private void FindAndMergeGroups(
            List<SizeKey> smallGroups,
            List<SizeKey> allGroups,
            UnionFindStructure unionFind,
            Dictionary<SizeKey, int> groupSizes)
        {
            // Сначала находим лучшие совпадения для всех малых групп
            var bestMatches = FindBestMatches(smallGroups, allGroups);

            // Объединяем группы в порядке качества совпадения (от лучшего к худшему)
            foreach (var match in bestMatches.OrderBy(m => m.Score))
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
        private int CalculateCurrentGroupSize(SizeKey rootKey, Dictionary<SizeKey, int> groupSizes, UnionFindStructure unionFind)
        {
            // Считаем суммарный размер всех групп, объединенных с этой
            int totalSize = 0;
            foreach (var key in groupSizes.Keys)
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
            List<GroupMatch> matches = new List<GroupMatch>();

            foreach (var smallKey in smallGroups)
            {
                double bestScore = double.MaxValue;
                SizeKey? bestTarget = null;

                foreach (var targetKey in allGroups)
                {
                    if (smallKey.Equals(targetKey))
                        continue;

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
            UnionFindStructure unionFind)
        {
            Dictionary<SizeKey, List<FamilyInstance>> newGroups = new Dictionary<SizeKey, List<FamilyInstance>>();

            // Для каждой исходной группы
            foreach (var entry in groups)
            {
                SizeKey originalKey = entry.Key;
                SizeKey rootKey = unionFind.FindRoot(originalKey);

                // Создаем новую группу, если еще не существует
                if (!newGroups.ContainsKey(rootKey))
                {
                    newGroups[rootKey] = new List<FamilyInstance>();
                }

                // Обновляем размеры в данных и добавляем в новую группу
                foreach (var lintel in entry.Value)
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
            foreach (var entry in newGroups)
            {
                groups[entry.Key] = entry.Value;
            }
        }

        /// <summary>
        /// Класс для хранения информации о совпадении групп
        /// </summary>
        private class GroupMatch
        {
            public SizeKey Source { get; }
            public SizeKey Target { get; }
            public double Score { get; }

            public GroupMatch(SizeKey source, SizeKey target, double score)
            {
                Source = source;
                Target = target;
                Score = score;
            }
        }

        /// <summary>
        /// Вспомогательный класс для работы со структурой Union-Find
        /// </summary>
        private class UnionFindStructure
        {
            private Dictionary<SizeKey, SizeKey> parent;

            public UnionFindStructure(List<SizeKey> keys)
            {
                parent = keys.ToDictionary(k => k, k => k);
            }

            /// <summary>
            /// Находит корневой элемент для указанного ключа
            /// </summary>
            public SizeKey FindRoot(SizeKey key)
            {
                if (!parent[key].Equals(key))
                    parent[key] = FindRoot(parent[key]); // Сжатие пути
                return parent[key];
            }

            /// <summary>
            /// Объединяет две группы
            /// </summary>
            public void Union(SizeKey key1, SizeKey key2, Dictionary<SizeKey, int> groupSizes)
            {
                SizeKey root1 = FindRoot(key1);
                SizeKey root2 = FindRoot(key2);

                if (!root1.Equals(root2))
                {
                    // Всегда делаем корнем большую группу
                    if (groupSizes[root1] < groupSizes[root2])
                    {
                        parent[root1] = root2;
                    }
                    else
                    {
                        parent[root2] = root1;
                    }
                }
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
            // Веса параметров в зависимости от порядка в конфигурации
            double weightFactor = 10.0;

            double totalDiff = 0;

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